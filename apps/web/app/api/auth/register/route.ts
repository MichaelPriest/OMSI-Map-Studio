import {NextResponse} from "next/server";
import {getSupabaseServerClient} from "@/lib/supabase/server";
export async function POST(request:Request){
  const form=await request.formData();
  const supabase=await getSupabaseServerClient();
  const {error}=await supabase.auth.signUp({
    email:String(form.get("email")??"").trim(),
    password:String(form.get("password")??""),
    options:{data:{display_name:String(form.get("name")??"").trim()}}
  });
  if(error) return NextResponse.redirect(new URL("/cadastro?erro=registro",request.url),303);
  return NextResponse.redirect(new URL("/conta",request.url),303);
}
