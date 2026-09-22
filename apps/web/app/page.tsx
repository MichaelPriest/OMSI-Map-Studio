import Link from "next/link";

export default function Home(){
  return <main>
    <section className="shell hero">
      <div className="eyebrow">Public Alpha · Windows</div>
      <h1>Construa mapas OMSI em um editor moderno.</h1>
      <p className="lead">Crie e edite mapas com viewport nativo, ferramentas visuais, biblioteca de assets, terreno, splines, paths de tráfego e transporte público.</p>
      <div className="actions"><Link className="btn primary" href="/precos">Participar do Alpha</Link><Link className="btn" href="/login">Área do assinante</Link></div>
    </section>
    <section id="recursos" className="shell grid">
      <article className="card"><span className="badge">Editor nativo</span><h3>WinUI + Direct3D 11</h3><p>Viewport 3D nativo, seleção, gizmos, terreno, objetos e splines reais.</p></article>
      <article className="card"><span className="badge">Construção</span><h3>Fluxo city-builder</h3><p>Biblioteca visual, road builder, snapping e ferramentas procedurais.</p></article>
      <article className="card"><span className="badge">Operação</span><h3>Paths e transporte</h3><p>CAR/HUM/RAIL/AIR, Tracks, Trips, Stops, StationLinks e Timetable.</p></article>
    </section>
    <section className="shell card" style={{marginBottom:72}}><h2>Alpha público, com desenvolvimento contínuo.</h2><p className="muted">Alguns mapas e recursos ainda podem apresentar incompatibilidades. Backups e validações continuam sendo prioridade.</p></section>
  </main>;
}
