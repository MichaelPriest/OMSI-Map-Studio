import {NextResponse} from "next/server";
import {getSupabaseServerClient} from "@/lib/supabase/server";

export async function POST(request:Request){
  const supabase=await getSupabaseServerClient();
  const {data:{user}}=await supabase.auth.getUser();

  if(!user){
    return NextResponse.redirect(new URL("/login",request.url),303);
  }

  const {data:profile}=await supabase
    .from("profiles")
    .select("role")
    .eq("id",user.id)
    .maybeSingle();

  if(profile?.role!=="admin"){
    return NextResponse.json({error:"Acesso negado."},{status:403});
  }

  const form=await request.formData();

  const payload={
    version:String(form.get("version")??"").trim(),
    channel:String(form.get("channel")??"alpha")==="stable"?"stable":"alpha",
    minimum_version:String(form.get("minimum_version")??"").trim()||null,
    download_url:String(form.get("download_url")??"").trim(),
    sha256:String(form.get("sha256")??"").trim()||null,
    notes:String(form.get("notes")??"").trim(),
    mandatory:form.get("mandatory")==="true",
    published:true,
    published_at:new Date().toISOString(),
    created_by:user.id
  };

  const {error}=await supabase.from("releases").insert(payload);

  if(error){
    return NextResponse.json({error:error.message},{status:400});
  }

  return NextResponse.redirect(new URL("/admin",request.url),303);
}
