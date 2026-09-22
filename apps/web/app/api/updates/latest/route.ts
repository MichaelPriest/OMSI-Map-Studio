import {NextResponse} from "next/server";
import {getSupabaseAdminClient} from "@/lib/supabase/admin";

export async function GET(request:Request){
  const channel=new URL(request.url).searchParams.get("channel")==="stable"
    ?"stable"
    :"alpha";

  const admin=getSupabaseAdminClient();

  const {data,error}=await admin
    .from("releases")
    .select("version,channel,notes,minimum_version,mandatory,published_at,sha256")
    .eq("channel",channel)
    .eq("published",true)
    .order("published_at",{ascending:false})
    .limit(1)
    .maybeSingle();

  if(error){
    return NextResponse.json({error:"Falha ao consultar atualização."},{status:500});
  }

  if(!data){
    return NextResponse.json({channel,update:null});
  }

  return NextResponse.json({
    channel,
    update:{
      version:data.version,
      notes:data.notes,
      minimumVersion:data.minimum_version,
      mandatory:data.mandatory,
      publishedAt:data.published_at,
      sha256:data.sha256,
      downloadEndpoint:`/api/download/latest?channel=${channel}`
    }
  });
}
