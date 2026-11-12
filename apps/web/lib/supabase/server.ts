import {createServerClient} from "@supabase/ssr";
import {cookies} from "next/headers";

export async function getSupabaseServerClient(){
  const cookieStore=await cookies();
  const url=process.env.NEXT_PUBLIC_SUPABASE_URL;
  const anonKey=process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
  if(!url||!anonKey) throw new Error("Supabase public configuration is missing.");
  return createServerClient(url,anonKey,{cookies:{
    getAll:()=>cookieStore.getAll(),
    setAll:(values)=>{try{for(const {name,value,options} of values) cookieStore.set(name,value,options);}catch{}}
  }});
}
