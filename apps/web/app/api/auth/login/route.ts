import {NextResponse} from "next/server";
import {getSupabaseServerClient} from "@/lib/supabase/server";
export async function POST(request:Request){
  const form=await request.formData();
  const supabase=await getSupabaseServerClient();
  const {error}=await supabase.auth.signInWithPassword({email:String(form.get("email")??"").trim(),password:String(form.get("password")??"")});
  if(error) return NextResponse.redirect(new URL("/login?erro=credenciais",request.url),303);
  return NextResponse.redirect(new URL("/conta",request.url),303);
}
