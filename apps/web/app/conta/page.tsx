import {redirect} from "next/navigation";
import {getSupabaseServerClient} from "@/lib/supabase/server";

export default async function AccountPage(){
  const supabase=await getSupabaseServerClient();
  const {data:{user}}=await supabase.auth.getUser();
  if(!user) redirect("/login");

  const [{data:subscription},{data:license}]=await Promise.all([
    supabase.from("subscriptions").select("*").eq("user_id",user.id).order("updated_at",{ascending:false}).limit(1).maybeSingle(),
    supabase.from("licenses").select("id,serial,status,max_devices,created_at").eq("user_id",user.id).eq("status","active").maybeSingle()
  ]);

  const {data:devices}=license?.id
    ? await supabase.from("devices").select("device_name,device_id,last_seen_at,revoked_at").eq("license_id",license.id)
    : {data:[]};

  const active=Boolean(subscription&&["active","trialing","past_due"].includes(subscription.status));

  return <main className="shell page">
    <div className="eyebrow">Área do assinante</div>
    <h2>Minha conta</h2>

    <div className="grid" style={{paddingBottom:28}}>
      <section className="card">
        <h3>Assinatura</h3>
        <p className={active?"":"danger"}>{subscription?.status??"Sem assinatura ativa"}</p>
        <div className="actions" style={{justifyContent:"flex-start",marginTop:12}}>
          <a className="btn primary" href="/api/download/latest?channel=alpha">Baixar versão Alpha</a>
          <form action="/api/billing/portal" method="post">
            <button className="btn" type="submit">Gerenciar cobrança</button>
          </form>
        </div>
      </section>

      <section className="card">
        <h3>Serial</h3>
        <p className="serial">{license?.serial??"Será gerado após confirmação do pagamento"}</p>
        <p className="muted">Até {license?.max_devices??2} computadores.</p>
      </section>

      <section className="card">
        <h3>Conta</h3>
        <p>{user.email}</p>
        <form action="/api/auth/logout" method="post">
          <button className="btn" type="submit">Sair</button>
        </form>
      </section>
    </div>

    <section className="card">
      <h3>Computadores ativados</h3>
      <table className="table">
        <thead><tr><th>Dispositivo</th><th>ID</th><th>Último acesso</th><th></th></tr></thead>
        <tbody>
          {(devices??[]).map(device=>
            <tr key={device.device_id}>
              <td>{device.device_name}</td>
              <td className="serial">{device.device_id}</td>
              <td>{device.last_seen_at?new Date(device.last_seen_at).toLocaleString("pt-BR"):"—"}</td>
              <td>
                {!device.revoked_at
                  ? <form action="/api/license/revoke-device" method="post">
                      <input type="hidden" name="device_id" value={device.device_id}/>
                      <button className="btn" type="submit">Revogar</button>
                    </form>
                  : <form action="/api/license/reactivate-device" method="post">
                      <input type="hidden" name="device_id" value={device.device_id}/>
                      <button className="btn" type="submit">Reativar</button>
                    </form>
                }
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </section>
  </main>;
}
