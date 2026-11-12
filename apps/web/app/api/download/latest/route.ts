import {NextResponse} from "next/server";
import {getSupabaseServerClient} from "@/lib/supabase/server";
import {getSupabaseAdminClient} from "@/lib/supabase/admin";

export async function GET(request:Request){
  const supabase=await getSupabaseServerClient();
  const {data:{user}}=await supabase.auth.getUser();

  if(!user){
    return NextResponse.redirect(new URL("/login",request.url));
  }

  const {data:entitlement}=await supabase
    .from("entitlements")
    .select("enabled")
    .eq("user_id",user.id)
    .eq("entitlement_key","editor.core")
    .maybeSingle();

  if(!entitlement?.enabled){
    return NextResponse.redirect(new URL("/precos",request.url));
  }

  const channel=new URL(request.url).searchParams.get("channel")==="stable"
    ?"stable"
    :"alpha";

  const admin=getSupabaseAdminClient();

  const {data:release}=await admin
    .from("releases")
    .select("download_url")
    .eq("channel",channel)
    .eq("published",true)
    .order("published_at",{ascending:false})
    .limit(1)
    .maybeSingle();

  if(!release?.download_url){
    return NextResponse.json({error:"Download ainda não publicado."},{status:404});
  }

  return NextResponse.redirect(release.download_url);
}
