import {NextResponse} from "next/server";
import {getSupabaseAdminClient} from "@/lib/supabase/admin";
import {signEntitlement} from "@/lib/license";

type ActivationBody={
  serial?:string;
  deviceId?:string;
  deviceName?:string;
};

export async function POST(request:Request){
  const body=await request.json().catch(()=>null) as ActivationBody|null;
  const serial=body?.serial?.trim().toUpperCase();
  const deviceId=body?.deviceId?.trim();

  if(!serial||!deviceId){
    return NextResponse.json({error:"serial e deviceId são obrigatórios."},{status:400});
  }

  const admin=getSupabaseAdminClient();

  const {data:license}=await admin
    .from("licenses")
    .select("*")
    .eq("serial",serial)
    .eq("status","active")
    .maybeSingle();

  if(!license){
    return NextResponse.json({error:"Licença inválida."},{status:404});
  }

  const {data:subscription}=await admin
    .from("subscriptions")
    .select("*")
    .eq("user_id",license.user_id)
    .order("updated_at",{ascending:false})
    .limit(1)
    .maybeSingle();

  if(!subscription||!["active","trialing","past_due"].includes(subscription.status)){
    return NextResponse.json({error:"Assinatura inativa."},{status:403});
  }

  const {data:knownDevice}=await admin
    .from("devices")
    .select("id,revoked_at")
    .eq("license_id",license.id)
    .eq("device_id",deviceId)
    .maybeSingle();

  if(knownDevice?.revoked_at){
    return NextResponse.json(
      {error:"Este dispositivo foi revogado. Reative-o na área do assinante antes de usar o serial novamente."},
      {status:403}
    );
  }

  const {data:activeDevices}=await admin
    .from("devices")
    .select("id,device_id")
    .eq("license_id",license.id)
    .is("revoked_at",null);

  const existing=activeDevices?.find(device=>device.device_id===deviceId);

  if(!existing&&(activeDevices?.length??0)>=license.max_devices){
    return NextResponse.json({error:"Limite de dispositivos atingido."},{status:409});
  }

  const {error:deviceError}=await admin
    .from("devices")
    .upsert({
      license_id:license.id,
      device_id:deviceId,
      device_name:body?.deviceName?.slice(0,120)||"Windows PC",
      last_seen_at:new Date().toISOString()
    },{onConflict:"license_id,device_id"});

  if(deviceError){
    return NextResponse.json({error:"Falha ao ativar dispositivo."},{status:500});
  }

  const {data:entitlements}=await admin
    .from("entitlements")
    .select("entitlement_key,enabled,expires_at")
    .eq("user_id",license.user_id)
    .eq("enabled",true);

  const now=Date.now();
  const offlineDays=Math.max(1,Number(process.env.LICENSE_OFFLINE_DAYS??"7"));
  const subscriptionExpiry=new Date(subscription.current_period_end).getTime();
  const offlineUntil=new Date(
    Math.min(subscriptionExpiry,now+offlineDays*86400000)
  ).toISOString();

  const deviceCount=existing
    ? activeDevices?.length??1
    : (activeDevices?.length??0)+1;

  const payload={
    issuer:"omsi-map-studio",
    licenseId:license.id,
    deviceId,
    entitlements:(entitlements??[]).map(e=>e.entitlement_key),
    subscriptionStatus:subscription.status,
    subscriptionExpiresAt:subscription.current_period_end,
    offlineUntil,
    issuedAt:new Date(now).toISOString(),
    planId:process.env.PUBLIC_ALPHA_PLAN_ID?.trim()||"founders-alpha",
    deviceCount,
    maxDevices:license.max_devices
  };

  return NextResponse.json({
    entitlementToken:signEntitlement(payload),
    payload
  });
}
