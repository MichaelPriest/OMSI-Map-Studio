import {NextResponse} from "next/server";
import {getStripe} from "@/lib/stripe";
import {getSupabaseServerClient} from "@/lib/supabase/server";
import {getSupabaseAdminClient} from "@/lib/supabase/admin";

export async function POST(request:Request){
  const supabase=await getSupabaseServerClient();
  const {data:{user}}=await supabase.auth.getUser();

  if(!user){
    return NextResponse.redirect(new URL("/login?next=/precos",request.url),303);
  }

  const price=process.env.STRIPE_PRICE_PUBLIC_ALPHA;
  if(!price){
    return NextResponse.json({error:"Plano Stripe ainda não configurado."},{status:503});
  }

  const admin=getSupabaseAdminClient();
  const stripe=getStripe();

  const {data:existing}=await admin
    .from("customers")
    .select("stripe_customer_id")
    .eq("user_id",user.id)
    .maybeSingle();

  let customerId=existing?.stripe_customer_id as string|undefined;

  if(!customerId){
    const customer=await stripe.customers.create({
      email:user.email,
      metadata:{user_id:user.id}
    });

    customerId=customer.id;

    const {error}=await admin.from("customers").upsert({
      user_id:user.id,
      stripe_customer_id:customerId
    });

    if(error){
      return NextResponse.json({error:"Falha ao vincular cliente."},{status:500});
    }
  }

  const baseUrl=process.env.NEXT_PUBLIC_SITE_URL??new URL(request.url).origin;

  const session=await stripe.checkout.sessions.create({
    mode:"subscription",
    customer:customerId,
    client_reference_id:user.id,
    line_items:[{price,quantity:1}],
    success_url:`${baseUrl}/conta?checkout=sucesso`,
    cancel_url:`${baseUrl}/precos?checkout=cancelado`,
    metadata:{
      user_id:user.id,
      product:"omsi-map-studio-public-alpha"
    },
    subscription_data:{
      metadata:{user_id:user.id}
    }
  });

  if(!session.url){
    return NextResponse.json({error:"Stripe não retornou URL de Checkout."},{status:502});
  }

  return NextResponse.redirect(session.url,303);
}
