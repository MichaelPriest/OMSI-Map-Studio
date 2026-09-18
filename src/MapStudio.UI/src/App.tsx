import { useEffect, useMemo, useState } from "react";
import {
  isDesktopBridgeAvailable,
  type OmsiMap,
  selectOmsiRoot,
  subscribeToHost
} from "./bridge/desktopBridge";
import { Viewport } from "./editor/Viewport";

const errorMessages: Record<string, string> = {
  invalidMessage: "A interface enviou uma mensagem inválida para o host.",
  invalidOmsiRoot: "A pasta selecionada não parece ser a raiz do OMSI 2: a pasta maps não foi encontrada.",
  accessDenied: "O Windows bloqueou o acesso a essa instalação do OMSI 2.",
  ioError: "Não foi possível ler os arquivos da instalação selecionada."
};

export function App() {
  const bridgeAvailable = useMemo(() => isDesktopBridgeAvailable(), []);
  const [rootPath, setRootPath] = useState<string>();
  const [maps, setMaps] = useState<OmsiMap[]>([]);
  const [selectedMap, setSelectedMap] = useState<OmsiMap>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(
    () =>
      subscribeToHost((message) => {
        if (message.type === "omsiInstallationLoaded") {
          setRootPath(message.rootPath);
          setMaps(message.maps);
          setSelectedMap(message.maps[0]);
          setLoading(false);
          setError(undefined);
          return;
        }

        if (message.type === "hostError") {
          setLoading(false);
          setError(errorMessages[message.code] ?? "O host desktop encontrou um erro inesperado.");
        }
      }),
    []
  );

  const selectedStats = useMemo(() => {
    if (!selectedMap) {
      return undefined;
    }

    return selectedMap.tiles.reduce(
      (stats, tile) => ({
        objects: stats.objects + tile.objectCount,
        splines: stats.splines + tile.splineCount,
        attachments: stats.attachments + tile.splineAttachmentCount,
        missingTiles: stats.missingTiles + (tile.fileExists ? 0 : 1)
      }),
      { objects: 0, splines: 0, attachments: 0, missingTiles: 0 }
    );
  }, [selectedMap]);

  const handleOpenOmsi = () => {
    if (!bridgeAvailable) {
      setError("Abra esta interface pelo aplicativo desktop OMSI Map Studio para acessar os arquivos locais.");
      return;
    }

    setLoading(true);
    setError(undefined);
    selectOmsiRoot();
  };

  return (
    <main className="editor-shell">
      <header className="topbar">
        <div className="brand">
          <strong>OMSI Map Studio</strong>
          <span>Editor independente para OMSI 2</span>
        </div>

        <nav className="toolbar" aria-label="Ferramentas principais">
          <button type="button" onClick={handleOpenOmsi} disabled={loading}>
            {loading ? "Lendo OMSI..." : rootPath ? "Trocar OMSI" : "Abrir OMSI"}
          </button>
          <button type="button" disabled>Novo mapa</button>
          <button type="button" disabled>Salvar</button>
        </nav>
      </header>

      <aside className="asset-panel">
        <div className="panel-heading">
          <span>Mapas instalados</span>
          <small>{rootPath ?? "Nenhuma instalação carregada"}</small>
        </div>

        {error && <div className="error-panel">{error}</div>}

        {!rootPath && !error && (
          <div className="empty-panel">
            Selecione a pasta raiz do OMSI 2. Os mapas reais encontrados em <code>maps</code> aparecerão aqui.
          </div>
        )}

        {rootPath && maps.length === 0 && (
          <div className="empty-panel">
            Nenhum mapa com <code>global.cfg</code> foi encontrado nessa instalação.
          </div>
        )}

        {maps.length > 0 && (
          <div className="map-list" role="list" aria-label="Mapas do OMSI 2">
            {maps.map((map) => {
              const missingTiles = map.tiles.filter((tile) => !tile.fileExists).length;

              return (
                <button
                  key={map.directoryPath}
                  type="button"
                  className={map.directoryPath === selectedMap?.directoryPath ? "map-card selected" : "map-card"}
                  onClick={() => setSelectedMap(map)}
                >
                  <strong>{map.displayName}</strong>
                  <span>{map.directoryName}</span>
                  <small>
                    {map.tiles.length} tiles
                    {map.usesWorldCoordinates ? " · coordenadas mundiais" : ""}
                    {missingTiles ? ` · ${missingTiles} ausentes` : ""}
                  </small>
                </button>
              );
            })}
          </div>
        )}
      </aside>

      <section className="viewport-panel">
        <Viewport
          tiles={selectedMap?.tiles ?? []}
          usesWorldCoordinates={selectedMap?.usesWorldCoordinates ?? false}
        />
        <div className="viewport-hint">
          <strong>{selectedMap?.displayName ?? "Nenhum mapa carregado"}</strong>
          <span>
            {selectedMap
              ? selectedMap.usesWorldCoordinates
                ? `${selectedMap.tiles.length} tiles · malha esquemática · ${selectedStats?.objects ?? 0} objetos · ${selectedStats?.splines ?? 0} splines`
                : `${selectedMap.tiles.length} tiles · escala cartesiana de 300 m · ${selectedStats?.objects ?? 0} objetos · ${selectedStats?.splines ?? 0} splines`
              : "O grid vazio representa apenas o espaço de edição."}
          </span>
        </div>
      </section>

      <aside className="inspector-panel">
        <div className="panel-heading">
          <span>Propriedades</span>
          <small>{selectedMap ? "Mapa selecionado" : "Nenhuma seleção"}</small>
        </div>

        {selectedMap && selectedStats ? (
          <dl className="property-list">
            <div>
              <dt>Nome</dt>
              <dd>{selectedMap.displayName}</dd>
            </div>
            <div>
              <dt>Pasta</dt>
              <dd>{selectedMap.directoryName}</dd>
            </div>
            <div>
              <dt>Sistema</dt>
              <dd>{selectedMap.usesWorldCoordinates ? "Coordenadas mundiais" : "Coordenadas cartesianas"}</dd>
            </div>
            <div>
              <dt>Tiles</dt>
              <dd>{selectedMap.tiles.length}</dd>
            </div>
            <div>
              <dt>Objetos</dt>
              <dd>{selectedStats.objects}</dd>
            </div>
            <div>
              <dt>Splines</dt>
              <dd>{selectedStats.splines}</dd>
            </div>
            <div>
              <dt>Attachments</dt>
              <dd>{selectedStats.attachments}</dd>
            </div>
            <div>
              <dt>Tiles ausentes</dt>
              <dd>{selectedStats.missingTiles}</dd>
            </div>
          </dl>
        ) : (
          <div className="empty-panel">
            Escolha um mapa para visualizar os tiles encontrados no arquivo <code>global.cfg</code>.
          </div>
        )}
      </aside>

      <footer className="statusbar">
        <span>{error ? "Erro" : loading ? "Carregando..." : "Pronto"}</span>
        <span>{selectedMap ? selectedMap.displayName : "Sem mapa aberto"}</span>
      </footer>
    </main>
  );
}
