import {redirect} from "next/navigation";
import {getSupabaseServerClient} from "@/lib/supabase/server";

export default async function AdminPage(){
  const supabase=await getSupabaseServerClient();
  const {data:{user}}=await supabase.auth.getUser();

  if(!user) redirect("/login");

  const {data:profile}=await supabase
    .from("profiles")
    .select("role")
    .eq("id",user.id)
    .maybeSingle();

  if(profile?.role!=="admin") redirect("/conta");

  const [users,subscriptions,licenses,releases]=await Promise.all([
    supabase.from("profiles").select("id",{count:"exact",head:true}),
    supabase.from("subscriptions").select("id",{count:"exact",head:true}),
    supabase.from("licenses").select("id",{count:"exact",head:true}),
    supabase.from("releases").select("*").order("published_at",{ascending:false}).limit(12)
  ]);

  return <main className="shell page">
    <div className="eyebrow">Admin</div>
    <h2>Painel OMSI Map Studio</h2>

    <div className="kpis">
      <div className="card kpi"><strong>{users.count??0}</strong><span className="muted">Contas</span></div>
      <div className="card kpi"><strong>{subscriptions.count??0}</strong><span className="muted">Assinaturas</span></div>
      <div className="card kpi"><strong>{licenses.count??0}</strong><span className="muted">Licenças</span></div>
      <div className="card kpi"><strong>{releases.data?.length??0}</strong><span className="muted">Releases recentes</span></div>
    </div>

    <section className="card" style={{marginBottom:18}}>
      <h3>Publicar versão</h3>
      <form className="form" action="/api/admin/releases" method="post">
        <label>Versão<input name="version" placeholder="0.2.0-alpha.6" required/></label>
        <label>Canal<select name="channel"><option value="alpha">Alpha</option><option value="stable">Stable</option></select></label>
        <label>Versão mínima<input name="minimum_version" placeholder="0.2.0-alpha.5"/></label>
        <label>URL do instalador<input name="download_url" type="url" required/></label>
        <label>SHA-256<input name="sha256"/></label>
        <label>Notas<textarea name="notes" required/></label>
        <label><input name="mandatory" type="checkbox" value="true"/> Atualização obrigatória</label>
        <button className="btn primary" type="submit">Publicar manifesto</button>
      </form>
    </section>

    <section className="card">
      <h3>Releases</h3>
      <table className="table">
        <thead><tr><th>Versão</th><th>Canal</th><th>Publicada</th><th>Data</th></tr></thead>
        <tbody>
          {(releases.data??[]).map(release=>
            <tr key={release.id}>
              <td>{release.version}</td>
              <td>{release.channel}</td>
              <td>{release.published?"Sim":"Não"}</td>
              <td>{new Date(release.published_at).toLocaleString("pt-BR")}</td>
            </tr>
          )}
        </tbody>
      </table>
    </section>
  </main>;
}
