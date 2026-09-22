export default function PricingPage(){
  const price=process.env.NEXT_PUBLIC_ALPHA_PRICE_LABEL??"Preço de lançamento a definir";
  return <main className="shell page">
    <div className="eyebrow">Founders Alpha</div><h2>Assinatura OMSI Map Studio</h2>
    <p className="lead" style={{margin:"0 0 28px",textAlign:"left"}}>Acesso ao editor Alpha, atualizações enquanto a assinatura estiver ativa e até 2 computadores ativados.</p>
    <div className="card" style={{maxWidth:560}}>
      <span className="badge">PUBLIC ALPHA</span><h3 style={{fontSize:30,marginTop:14}}>{price}</h3>
      <p className="muted">Cobrança via Stripe. O preço final e a periodicidade são configurados no Stripe.</p>
      <form action="/api/checkout" method="post"><button className="btn good" type="submit">Assinar e liberar o Alpha</button></form>
    </div>
  </main>;
}
