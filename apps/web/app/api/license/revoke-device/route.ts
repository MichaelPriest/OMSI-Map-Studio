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
    .select("id")
    .eq("user_id",user.id)
    .eq("status","active")
    .maybeSingle();

  if(!license){
    return NextResponse.json({error:"Licença ativa não encontrada."},{status:404});
  }

  const {data:device}=await admin
    .from("devices")
    .select("id")
    .eq("license_id",license.id)
    .eq("device_id",deviceId)
    .is("revoked_at",null)
    .maybeSingle();

  if(!device){
    return NextResponse.json({error:"Dispositivo ativo não encontrado."},{status:404});
  }

  const {error}=await admin
    .from("devices")
    .update({revoked_at:new Date().toISOString()})
    .eq("id",device.id);

  if(error){
    return NextResponse.json({error:"Falha ao revogar dispositivo."},{status:500});
  }

  await admin.from("audit_logs").insert({
    actor_user_id:user.id,
    event_type:"license.device.revoked",
    target_type:"device",
    target_id:device.id,
    details:{device_id:deviceId}
  });

  return NextResponse.redirect(new URL("/conta?dispositivo=revogado",request.url),303);
}
