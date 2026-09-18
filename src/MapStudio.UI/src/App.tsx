import { Viewport } from "./editor/Viewport";

export function App() {
  return (
    <main className="editor-shell">
      <header className="topbar">
        <div className="brand">
          <strong>OMSI Map Studio</strong>
          <span>Editor independente para OMSI 2</span>
        </div>
        <nav className="toolbar" aria-label="Ferramentas principais">
          <button type="button">Abrir OMSI</button>
          <button type="button" disabled>Novo mapa</button>
          <button type="button" disabled>Salvar</button>
        </nav>
      </header>

      <aside className="asset-panel">
        <div className="panel-heading">
          <span>Biblioteca</span>
          <small>Nenhuma instalação carregada</small>
        </div>
        <input className="search" type="search" placeholder="Buscar objetos, splines..." disabled />
        <div className="empty-panel">
          Selecione a instalação do OMSI 2 para carregar os recursos reais.
        </div>
      </aside>

      <section className="viewport-panel">
        <Viewport />
        <div className="viewport-hint">
          <strong>Nenhum mapa carregado</strong>
          <span>O grid representa apenas o espaço de edição.</span>
        </div>
      </section>

      <aside className="inspector-panel">
        <div className="panel-heading">
          <span>Propriedades</span>
          <small>Nenhuma seleção</small>
        </div>
        <div className="empty-panel">
          Selecione um objeto, spline ou tile para editar suas propriedades.
        </div>
      </aside>

      <footer className="statusbar">
        <span>Pronto</span>
        <span>Sem mapa aberto</span>
      </footer>
    </main>
  );
}
