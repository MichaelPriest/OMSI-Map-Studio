import type Stripe from "stripe";
import {NextResponse} from "next/server";
import {getStripe} from "@/lib/stripe";
import {getSupabaseAdminClient} from "@/lib/supabase/admin";
import {generateLicenseSerial} from "@/lib/license";

async function syncSubscription(subscription:Stripe.Subscription,fallbackUserId?:string){
  const admin=getSupabaseAdminClient();
  let userId=fallbackUserId??subscription.metadata.user_id;

  if(!userId){
    const {data}=await admin
      .from("subscriptions")
      .select("user_id")
      .eq("stripe_subscription_id",subscription.id)
      .maybeSingle();

    userId=data?.user_id;
  }

  if(!userId){
    return;
  }

  const periodEndUnix=Math.max(
    ...subscription.items.data.map(item=>item.current_period_end)
  );

  if(!Number.isFinite(periodEndUnix)){
    throw new Error("Stripe subscription has no current billing period.");
  }

  const periodEnd=new Date(periodEndUnix*1000).toISOString();

  await admin.from("subscriptions").upsert({
    user_id:userId,
    stripe_subscription_id:subscription.id,
    stripe_customer_id:typeof subscription.customer==="string"
      ? subscription.customer
      : subscription.customer.id,
    stripe_price_id:subscription.items.data[0]?.price.id??null,
    status:subscription.status,
    current_period_end:periodEnd,
    cancel_at_period_end:subscription.cancel_at_period_end,
    updated_at:new Date().toISOString()
  },{onConflict:"stripe_subscription_id"});

  const enabled=["active","trialing","past_due"].includes(subscription.status);

  await admin.from("entitlements").upsert({
    user_id:userId,
    entitlement_key:"editor.core",
    enabled,
    source:"stripe",
    expires_at:periodEnd,
    updated_at:new Date().toISOString()
  },{onConflict:"user_id,entitlement_key"});

  if(enabled){
    const {data:existing}=await admin
      .from("licenses")
      .select("id")
      .eq("user_id",userId)
      .eq("status","active")
      .maybeSingle();

    if(!existing){
      for(let attempt=0;attempt<5;attempt++){
        const {error}=await admin.from("licenses").insert({
          user_id:userId,
          serial:generateLicenseSerial(),
          status:"active",
          max_devices:2
        });

        if(!error){
          break;
        }
      }
    }
  }
}

export async function POST(request:Request){
  const secret=process.env.STRIPE_WEBHOOK_SECRET;

  if(!secret){
    return NextResponse.json({error:"Webhook Stripe não configurado."},{status:503});
  }

  const body=await request.text();
  const signature=request.headers.get("stripe-signature");

  if(!signature){
    return NextResponse.json({error:"Assinatura Stripe ausente."},{status:400});
  }

  let event:Stripe.Event;

  try{
    event=getStripe().webhooks.constructEvent(body,signature,secret);
  }catch{
    return NextResponse.json({error:"Webhook inválido."},{status:400});
  }

  if(event.type==="checkout.session.completed"){
    const session=event.data.object as Stripe.Checkout.Session;

    if(typeof session.subscription==="string"){
      const subscription=await getStripe().subscriptions.retrieve(session.subscription);

      await syncSubscription(
        subscription,
        session.client_reference_id??session.metadata?.user_id
      );
    }
  }

  if(
    event.type==="customer.subscription.updated"||
    event.type==="customer.subscription.deleted"
  ){
    await syncSubscription(event.data.object as Stripe.Subscription);
  }

  return NextResponse.json({received:true});
}
