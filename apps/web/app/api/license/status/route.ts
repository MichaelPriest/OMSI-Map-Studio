import {NextResponse} from "next/server";
import {getSupabaseServerClient} from "@/lib/supabase/server";

export async function GET(){
  const supabase=await getSupabaseServerClient();
  const {data:{user}}=await supabase.auth.getUser();

  if(!user){
    return NextResponse.json({error:"Não autenticado."},{status:401});
  }

  const [
    {data:subscription},
    {data:license},
    {data:entitlements}
  ]=await Promise.all([
    supabase.from("subscriptions")
      .select("*")
      .eq("user_id",user.id)
      .order("updated_at",{ascending:false})
      .limit(1)
      .maybeSingle(),
    supabase.from("licenses")
      .select("id,serial,status,max_devices,created_at")
      .eq("user_id",user.id)
      .eq("status","active")
      .maybeSingle(),
    supabase.from("entitlements")
      .select("entitlement_key,enabled,expires_at")
      .eq("user_id",user.id)
  ]);

  let devices:unknown[]=[];

  if(license?.id){
    const result=await supabase.from("devices")
      .select("id,device_id,device_name,last_seen_at,revoked_at")
      .eq("license_id",license.id);

    devices=result.data??[];
  }

  return NextResponse.json({
    subscription,
    license,
    entitlements,
    devices
  });
}
