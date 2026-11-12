import {NextResponse} from "next/server";
import {getStripe} from "@/lib/stripe";
import {getSupabaseServerClient} from "@/lib/supabase/server";
import {getSupabaseAdminClient} from "@/lib/supabase/admin";

export async function POST(request:Request){
  const supabase=await getSupabaseServerClient();
  const {data:{user}}=await supabase.auth.getUser();

  if(!user){
    return NextResponse.redirect(new URL("/login",request.url),303);
  }

  const admin=getSupabaseAdminClient();

  const {data:customer}=await admin
    .from("customers")
    .select("stripe_customer_id")
    .eq("user_id",user.id)
    .maybeSingle();

  if(!customer?.stripe_customer_id){
    return NextResponse.redirect(new URL("/precos",request.url),303);
  }

  const baseUrl=process.env.NEXT_PUBLIC_SITE_URL??new URL(request.url).origin;

  const session=await getStripe().billingPortal.sessions.create({
    customer:customer.stripe_customer_id,
    return_url:`${baseUrl}/conta`
  });

  return NextResponse.redirect(session.url,303);
}
