import {NextResponse} from "next/server";
import {getSupabaseServerClient} from "@/lib/supabase/server";
import {getSupabaseAdminClient} from "@/lib/supabase/admin";

export async function POST(request:Request){
  const supabase=await getSupabaseServerClient();
  const {data:{user}}=await supabase.auth.getUser();

  if(!user){
    return NextResponse.redirect(new URL("/login",request.url),303);
  }

  const form=await request.formData();
  const deviceId=String(form.get("device_id")??"").trim();

  if(!deviceId){
    return NextResponse.json({error:"Dispositivo não informado."},{status:400});
  }

  const admin=getSupabaseAdminClient();

  const {data:license}=await admin
    .from("licenses")
    .select("id,max_devices")
    .eq("user_id",user.id)
    .eq("status","active")
    .maybeSingle();

  if(!license){
    return NextResponse.json({error:"Licença ativa não encontrada."},{status:404});
  }

  const [{data:device},{data:activeDevices}]=await Promise.all([
    admin.from("devices")
      .select("id,revoked_at")
      .eq("license_id",license.id)
      .eq("device_id",deviceId)
      .maybeSingle(),
    admin.from("devices")
      .select("id")
      .eq("license_id",license.id)
      .is("revoked_at",null)
  ]);

  if(!device?.revoked_at){
    return NextResponse.json({error:"Dispositivo revogado não encontrado."},{status:404});
  }

  if((activeDevices?.length??0)>=license.max_devices){
    return NextResponse.json({error:"Limite de dispositivos atingido."},{status:409});
  }

  const now=new Date().toISOString();
  const {error}=await admin
    .from("devices")
    .update({
      revoked_at:null,
      last_seen_at:now
    })
    .eq("id",device.id);

  if(error){
    return NextResponse.json({error:"Falha ao reativar dispositivo."},{status:500});
  }

  await admin.from("audit_logs").insert({
    actor_user_id:user.id,
    event_type:"license.device.reactivated",
    target_type:"device",
    target_id:device.id,
    details:{device_id:deviceId}
  });

  return NextResponse.redirect(new URL("/conta?dispositivo=reativado",request.url),303);
}
