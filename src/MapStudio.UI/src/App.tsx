import {
  useCallback,
  useEffect,
  useMemo,
  useState
} from "react";
import {
  isDesktopBridgeAvailable,
  loadMapContent,
  loadSceneryObjectGeometry,
  loadSceneryObjectMetadata,
  selectMap,
  selectOmsiRoot,
  subscribeToHost,
  type OmsiMap,
  type OmsiPlacedObject,
  type OmsiPlacedSpline,
  type OmsiSceneryObjectGeometry,
  type OmsiSceneryObjectMetadata
} from "./bridge/desktopBridge";
import { Viewport } from "./editor/Viewport";

type AppView =
  | "home"
  | "omsi"
  | "map"
  | "editor"
  | "tools"
  | "settings";

type InspectorTab =
  | "general"
  | "transform"
  | "geometry"
  | "materials";

const errorMessages: Record<string, string> = {
  invalidMessage:
    "A interface enviou uma mensagem inválida para o host.",
  invalidOmsiRoot:
    "A pasta selecionada não parece ser a raiz do OMSI 2: a pasta maps não foi encontrada.",
  omsiRootRequired:
    "Selecione primeiro a pasta raiz do OMSI 2.",
  mapOutsideOmsiMaps:
    "Escolha uma pasta de mapa que esteja dentro da pasta maps do OMSI selecionado.",
  invalidMapFolder:
    "A pasta selecionada não contém global.cfg e não parece ser um mapa do OMSI.",
  accessDenied:
    "O Windows bloqueou o acesso aos arquivos selecionados.",
  ioError:
    "Não foi possível ler os arquivos selecionados.",
  unknownMap:
    "O mapa solicitado não é o mapa atualmente aberto.",
  unknownSceneryObject:
    "O objeto solicitado não pertence ao mapa aberto.",
  invalidSceneryObjectPath:
    "A referência do objeto não pôde ser resolvida com segurança na pasta Sceneryobjects.",
  mapOpenError:
    "Não foi possível abrir esse mapa.",
  unexpectedHostError:
    "O host desktop encontrou um erro inesperado."
};

const appVersion = "0.1.0-alpha.3-dev";

const formatNumber = (value: number) =>
  value.toLocaleString("pt-BR", {
    maximumFractionDigits: 3
  });

const getObjectName = (path: string) =>
  path.split(/[\\/]/).filter(Boolean).at(-1) ?? path;

const clamp01 = (value: number) =>
  Math.min(1, Math.max(0, value));

const toRgb = (
  red: number,
  green: number,
  blue: number
) =>
  `rgb(${Math.round(clamp01(red) * 255)} ${Math.round(clamp01(green) * 255)} ${Math.round(clamp01(blue) * 255)})`;

export function App() {
  const bridgeAvailable = useMemo(
    () => isDesktopBridgeAvailable(),
    []
  );

  const [view, setView] =
    useState<AppView>("home");

  const [inspectorTab, setInspectorTab] =
    useState<InspectorTab>("general");

  const [rootPath, setRootPath] =
    useState<string>();

  const [selectedMap, setSelectedMap] =
    useState<OmsiMap>();

  const [objects, setObjects] =
    useState<OmsiPlacedObject[]>([]);

  const [splines, setSplines] =
    useState<OmsiPlacedSpline[]>([]);

  const [
    sceneryMetadataByPath,
    setSceneryMetadataByPath
  ] = useState<
    Record<string, OmsiSceneryObjectMetadata>
  >({});

  const [
    geometryByPath,
    setGeometryByPath
  ] = useState<
    Record<string, OmsiSceneryObjectGeometry>
  >({});

  const [selectedObject, setSelectedObject] =
    useState<OmsiPlacedObject>();

  const [selectingRoot, setSelectingRoot] =
    useState(false);

  const [selectingMap, setSelectingMap] =
    useState(false);

  const [
    loadingMapContent,
    setLoadingMapContent
  ] = useState(false);

  const [
    loadingMetadataFor,
    setLoadingMetadataFor
  ] = useState<string>();

  const [
    loadingGeometryFor,
    setLoadingGeometryFor
  ] = useState<string>();

  const [error, setError] =
    useState<string>();

  useEffect(
    () =>
      subscribeToHost((message) => {
        if (
          message.type ===
          "omsiRootSelected"
        ) {
          setRootPath(message.rootPath);
          setSelectedMap(undefined);
          setObjects([]);
          setSplines([]);
          setSelectedObject(undefined);
          setSceneryMetadataByPath({});
          setGeometryByPath({});
          setSelectingRoot(false);
          setSelectingMap(false);
          setLoadingMapContent(false);
          setError(undefined);
          setView("map");
          return;
        }

        if (message.type === "mapOpened") {
          setSelectedMap(message.map);
          setObjects([]);
          setSplines([]);
          setSelectedObject(undefined);
          setSceneryMetadataByPath({});
          setGeometryByPath({});
          setSelectingMap(false);
          setLoadingMapContent(false);
          setInspectorTab("general");
          setError(undefined);
          setView("editor");
          return;
        }

        if (
          message.type ===
          "selectionCancelled"
        ) {
          if (message.target === "omsi") {
            setSelectingRoot(false);
          } else {
            setSelectingMap(false);
          }

          return;
        }

        if (
          message.type ===
          "mapContentLoaded"
        ) {
          setObjects(message.objects);
          setSplines(message.splines);

          setSelectedMap((current) =>
            current?.directoryName ===
            message.directoryName
              ? {
                  ...current,
                  tiles: message.tiles
                }
              : current
          );

          setLoadingMapContent(false);
          return;
        }

        if (
          message.type ===
          "sceneryObjectMetadataLoaded"
        ) {
          setSceneryMetadataByPath(
            (current) => ({
              ...current,
              [message.sceneryObjectPath]:
                message.metadata
            })
          );

          setLoadingMetadataFor(
            (current) =>
              current ===
              message.sceneryObjectPath
                ? undefined
                : current
          );

          return;
        }

        if (
          message.type ===
          "sceneryObjectGeometryLoaded"
        ) {
          setGeometryByPath((current) => ({
            ...current,
            [message.sceneryObjectPath]:
              message.geometry
          }));

          setLoadingGeometryFor((current) =>
            current ===
            message.sceneryObjectPath
              ? undefined
              : current
          );

          return;
        }

        if (message.type === "hostError") {
          setSelectingRoot(false);
          setSelectingMap(false);
          setLoadingMapContent(false);
          setLoadingMetadataFor(undefined);
          setLoadingGeometryFor(undefined);

          setError(
            errorMessages[message.code] ??
              "O host desktop encontrou um erro inesperado."
          );
        }
      }),
    []
  );

  useEffect(() => {
    if (
      !bridgeAvailable ||
      !selectedMap ||
      selectedMap.tiles.every(
        (tile) => tile.detailsLoaded
      ) ||
      loadingMapContent
    ) {
      return;
    }

    setLoadingMapContent(true);

    loadMapContent(
      selectedMap.directoryName
    );
  }, [
    bridgeAvailable,
    loadingMapContent,
    selectedMap
  ]);

  useEffect(() => {
    const sceneryObjectPath =
      selectedObject?.sceneryObjectPath;

    if (
      !bridgeAvailable ||
      !sceneryObjectPath ||
      Object.hasOwn(
        sceneryMetadataByPath,
        sceneryObjectPath
      ) ||
      loadingMetadataFor ===
        sceneryObjectPath
    ) {
      return;
    }

    setLoadingMetadataFor(
      sceneryObjectPath
    );

    loadSceneryObjectMetadata(
      sceneryObjectPath
    );
  }, [
    bridgeAvailable,
    loadingMetadataFor,
    sceneryMetadataByPath,
    selectedObject
  ]);

  useEffect(() => {
    const sceneryObjectPath =
      selectedObject?.sceneryObjectPath;

    if (
      !bridgeAvailable ||
      !selectedMap ||
      selectedMap.usesWorldCoordinates ||
      !sceneryObjectPath ||
      Object.hasOwn(
        geometryByPath,
        sceneryObjectPath
      ) ||
      loadingGeometryFor ===
        sceneryObjectPath
    ) {
      return;
    }

    setLoadingGeometryFor(
      sceneryObjectPath
    );

    loadSceneryObjectGeometry(
      sceneryObjectPath
    );
  }, [
    bridgeAvailable,
    geometryByPath,
    loadingGeometryFor,
    selectedMap,
    selectedObject
  ]);

  const selectedStats = useMemo(() => {
    if (
      !selectedMap ||
      !selectedMap.tiles.every(
        (tile) => tile.detailsLoaded
      )
    ) {
      return undefined;
    }

    return selectedMap.tiles.reduce(
      (stats, tile) => ({
        objects:
          stats.objects +
          tile.objectCount,
        splines:
          stats.splines +
          tile.splineCount,
        attachments:
          stats.attachments +
          tile.splineAttachmentCount,
        missingTiles:
          stats.missingTiles +
          (tile.fileExists ? 0 : 1)
      }),
      {
        objects: 0,
        splines: 0,
        attachments: 0,
        missingTiles: 0
      }
    );
  }, [selectedMap]);

  const selectedMetadata =
    selectedObject
      ? sceneryMetadataByPath[
          selectedObject.sceneryObjectPath
        ]
      : undefined;

  const selectedGeometry =
    selectedObject
      ? geometryByPath[
          selectedObject.sceneryObjectPath
        ]
      : undefined;

  const geometryStats = useMemo(() => {
    if (!selectedGeometry) {
      return undefined;
    }

    return selectedGeometry.meshes.reduce(
      (stats, mesh) => ({
        loadedMeshes:
          stats.loadedMeshes +
          (mesh.geometry.isLoaded ? 1 : 0),
        vertices:
          stats.vertices +
          Math.floor(
            mesh.geometry.positions.length / 3
          ),
        triangles:
          stats.triangles +
          Math.floor(
            mesh.geometry.indices.length / 3
          ),
        materials:
          stats.materials +
          mesh.geometry.materials.length
      }),
      {
        loadedMeshes: 0,
        vertices: 0,
        triangles: 0,
        materials: 0
      }
    );
  }, [selectedGeometry]);

  const materialRows = useMemo(() => {
    if (!selectedGeometry) {
      return [];
    }

    return selectedGeometry.meshes.flatMap(
      (mesh) =>
        mesh.geometry.materials.map(
          (material, index) => ({
            mesh: getObjectName(
              mesh.declaredPath
            ),
            index,
            material
          })
        )
    );
  }, [selectedGeometry]);

  const selectedDisplayName =
    selectedMetadata?.friendlyName ??
    (selectedObject
      ? getObjectName(
          selectedObject.sceneryObjectPath
        )
      : undefined);

  const selectedObjectGlobal =
    selectedMap &&
    selectedObject &&
    !selectedMap.usesWorldCoordinates
      ? {
          x:
            selectedObject.tileX * 300 +
            selectedObject.x,
          y: selectedObject.z,
          z:
            selectedObject.tileY * 300 +
            selectedObject.y
        }
      : undefined;

  const handleObjectSelection =
    useCallback(
      (
        placedObject:
          | OmsiPlacedObject
          | undefined
      ) => {
        setSelectedObject(
          placedObject
        );

        if (placedObject) {
          setInspectorTab("general");
        }

        setError(undefined);
      },
      []
    );

  const handleOpenOmsi = () => {
    if (!bridgeAvailable) {
      setError(
        "Abra esta interface pelo aplicativo desktop OMSI Map Studio."
      );
      return;
    }

    setSelectingRoot(true);
    setError(undefined);
    selectOmsiRoot();
  };

  const handleOpenMap = () => {
    if (!rootPath) {
      setError(
        "Selecione primeiro a pasta raiz do OMSI 2."
      );
      setView("omsi");
      return;
    }

    setSelectingMap(true);
    setError(undefined);
    selectMap();
  };

  const busy =
    selectingRoot ||
    selectingMap ||
    loadingMapContent;

  const renderNav = () => (
    <aside className="studio-sidebar">
      <div className="sidebar-brand">
        <div className="brand-mark">O</div>
        <div>
          <strong>OMSI Map Studio</strong>
          <span>Editor moderno para OMSI 2</span>
        </div>
      </div>

      <nav
        className="sidebar-nav"
        aria-label="Navegação principal"
      >
        <button
          type="button"
          className={
            view === "home"
              ? "nav-item active"
              : "nav-item"
          }
          onClick={() => setView("home")}
        >
          <span className="nav-icon">⌂</span>
          Início
        </button>

        <button
          type="button"
          className={
            view === "omsi"
              ? "nav-item active"
              : "nav-item"
          }
          onClick={() => setView("omsi")}
        >
          <span className="nav-icon">▣</span>
          Abrir OMSI
          {rootPath && (
            <span className="nav-check">✓</span>
          )}
        </button>

        <button
          type="button"
          className={
            view === "map"
              ? "nav-item active"
              : "nav-item"
          }
          onClick={() => setView("map")}
        >
          <span className="nav-icon">▰</span>
          Abrir mapa
          {selectedMap && (
            <span className="nav-check">✓</span>
          )}
        </button>

        <button
          type="button"
          className={
            view === "editor"
              ? "nav-item active"
              : "nav-item"
          }
          onClick={() => setView("editor")}
          disabled={!selectedMap}
        >
          <span className="nav-icon">◇</span>
          Explorador
        </button>

        <button
          type="button"
          className={
            view === "tools"
              ? "nav-item active"
              : "nav-item"
          }
          onClick={() => setView("tools")}
        >
          <span className="nav-icon">⌘</span>
          Ferramentas
        </button>

        <button
          type="button"
          className={
            view === "settings"
              ? "nav-item active"
              : "nav-item"
          }
          onClick={() => setView("settings")}
        >
          <span className="nav-icon">⚙</span>
          Configurações
        </button>
      </nav>

      <div className="sidebar-footer">
        <span>v{appVersion}</span>
        <span>Somente leitura</span>
      </div>
    </aside>
  );

  const renderHome = () => (
    <section className="page-shell">
      <div className="page-title">
        <span className="eyebrow">
          OMSI MAP STUDIO
        </span>
        <h1>Editor de mapas moderno para OMSI 2</h1>
        <p>
          Abra sua instalação, escolha manualmente
          o mapa e explore os dados reais sem
          modificar os arquivos do jogo.
        </p>
      </div>

      <div className="home-grid">
        <article className="home-card accent-card">
          <span className="card-kicker">
            1 · Ambiente
          </span>
          <h2>Abrir OMSI</h2>
          <p>
            Selecione a pasta principal do OMSI 2.
            O editor usará essa raiz para resolver
            mapas, objetos, splines e texturas.
          </p>
          <button
            type="button"
            className="primary-button"
            onClick={() => setView("omsi")}
          >
            {rootPath
              ? "OMSI conectado"
              : "Selecionar OMSI"}
          </button>
        </article>

        <article className="home-card">
          <span className="card-kicker">
            2 · Projeto
          </span>
          <h2>Abrir mapa</h2>
          <p>
            Escolha manualmente uma pasta dentro
            de <code>maps</code>. Nenhum mapa é
            carregado automaticamente.
          </p>
          <button
            type="button"
            onClick={() => setView("map")}
            disabled={!rootPath}
          >
            {selectedMap
              ? selectedMap.displayName
              : "Escolher mapa"}
          </button>
        </article>

        <article className="home-card">
          <span className="card-kicker">
            3 · Editor
          </span>
          <h2>Explorar mapa</h2>
          <p>
            Visualize tiles, objetos reais,
            geometria O3D e propriedades do
            elemento selecionado.
          </p>
          <button
            type="button"
            onClick={() => setView("editor")}
            disabled={!selectedMap}
          >
            Abrir editor
          </button>
        </article>
      </div>

      <div className="status-strip">
        <div>
          <span>OMSI</span>
          <strong>
            {rootPath
              ? "Conectado"
              : "Não selecionado"}
          </strong>
        </div>
        <div>
          <span>Mapa</span>
          <strong>
            {selectedMap?.displayName ??
              "Nenhum aberto"}
          </strong>
        </div>
        <div>
          <span>Modo</span>
          <strong>Somente leitura</strong>
        </div>
      </div>
    </section>
  );

  const renderOmsiPage = () => (
    <section className="page-shell">
      <div className="page-title compact">
        <span className="eyebrow">
          CONFIGURAÇÃO DO AMBIENTE
        </span>
        <h1>Abrir OMSI</h1>
        <p>
          Selecione a pasta principal da sua
          instalação do OMSI 2.
        </p>
      </div>

      <div className="setup-grid">
        <article className="setup-card">
          <div className="folder-illustration">
            ▰
          </div>
          <h2>Selecionar pasta do OMSI 2</h2>
          <p>
            Escolha a pasta que contém
            <code> maps </code>,
            <code> Sceneryobjects </code> e
            <code> Splines</code>.
          </p>

          <div className="path-field">
            <span>
              {rootPath ??
                "Nenhuma pasta selecionada"}
            </span>
          </div>

          <button
            type="button"
            className="primary-button wide"
            onClick={handleOpenOmsi}
            disabled={busy}
          >
            {selectingRoot
              ? "Selecionando..."
              : rootPath
                ? "Trocar pasta do OMSI"
                : "Abrir OMSI"}
          </button>
        </article>

        <aside className="info-card">
          <h3>Informações</h3>
          <p>
            A instalação serve apenas como base
            para localizar os recursos reais do
            jogo. Nenhum mapa é aberto nesta etapa.
          </p>

          <div
            className={
              rootPath
                ? "status-message success"
                : "status-message"
            }
          >
            <strong>
              {rootPath
                ? "✓ Pasta encontrada"
                : "○ Aguardando seleção"}
            </strong>
            <span>
              {rootPath
                ? rootPath
                : "Selecione sua instalação do OMSI 2."}
            </span>
          </div>
        </aside>
      </div>
    </section>
  );

  const renderMapPage = () => (
    <section className="page-shell">
      <div className="page-title compact">
        <span className="eyebrow">
          PROJETO
        </span>
        <h1>Abrir mapa</h1>
        <p>
          Escolha manualmente a pasta do mapa que
          deseja visualizar.
        </p>
      </div>

      {!rootPath ? (
        <article className="setup-card centered">
          <h2>Selecione o OMSI primeiro</h2>
          <p>
            Antes de abrir um mapa, precisamos
            conhecer a pasta raiz da instalação.
          </p>
          <button
            type="button"
            className="primary-button"
            onClick={() => setView("omsi")}
          >
            Ir para Abrir OMSI
          </button>
        </article>
      ) : (
        <div className="setup-grid">
          <article className="setup-card">
            <div className="folder-illustration">
              ▱
            </div>
            <h2>Selecionar pasta do mapa</h2>
            <p>
              O seletor será aberto diretamente em
              <code> {rootPath}\\maps</code>.
            </p>

            <div className="path-field">
              <span>
                {selectedMap?.directoryPath ??
                  "Nenhum mapa selecionado"}
              </span>
            </div>

            <button
              type="button"
              className="primary-button wide"
              onClick={handleOpenMap}
              disabled={busy}
            >
              {selectingMap
                ? "Selecionando..."
                : selectedMap
                  ? "Abrir outro mapa"
                  : "Abrir mapa"}
            </button>
          </article>

          <aside className="info-card">
            <h3>Orientações</h3>
            <p>
              Escolha uma pasta de mapa dentro de
              <code> maps</code>. A pasta precisa
              conter <code>global.cfg</code>.
            </p>

            {selectedMap ? (
              <div className="selected-map-card">
                <strong>
                  {selectedMap.displayName}
                </strong>
                <span>
                  {selectedMap.directoryName}
                </span>
                <span>
                  {selectedMap.tiles.length} tiles
                </span>
                <button
                  type="button"
                  onClick={() => setView("editor")}
                >
                  Abrir no editor →
                </button>
              </div>
            ) : (
              <div className="status-message">
                <strong>
                  Nenhum mapa aberto
                </strong>
                <span>
                  A instalação não será varrida
                  automaticamente.
                </span>
              </div>
            )}
          </aside>
        </div>
      )}
    </section>
  );

  const renderMapInspector = () => (
    <>
      <div className="inspector-hero">
        <div className="object-symbol">M</div>
        <div>
          <strong>
            {selectedMap?.displayName}
          </strong>
          <span>
            {selectedMap?.directoryName}
          </span>
        </div>
      </div>

      <dl className="property-list dense">
        <div>
          <dt>Tiles</dt>
          <dd>
            {selectedMap?.tiles.length ?? 0}
          </dd>
        </div>
        <div>
          <dt>Objetos</dt>
          <dd>
            {selectedStats?.objects ??
              (loadingMapContent
                ? "Carregando..."
                : objects.length)}
          </dd>
        </div>
        <div>
          <dt>Splines</dt>
          <dd>
            {selectedStats?.splines ??
              (loadingMapContent
                ? "Carregando..."
                : "—")}
          </dd>
        </div>
        <div>
          <dt>Sistema</dt>
          <dd>
            {selectedMap?.usesWorldCoordinates
              ? "Coordenadas mundiais"
              : "Cartesiano"}
          </dd>
        </div>
        <div>
          <dt>Tiles ausentes</dt>
          <dd>
            {selectedStats?.missingTiles ?? "—"}
          </dd>
        </div>
      </dl>
    </>
  );

  const renderObjectInspector = () => {
    if (!selectedObject) {
      return renderMapInspector();
    }

    return (
      <>
        <div className="inspector-hero">
          <div className="object-symbol">O</div>
          <div>
            <strong>
              {selectedDisplayName}
            </strong>
            <span>
              Objeto #{selectedObject.objectId}
            </span>
          </div>
        </div>

        <div className="inspector-tabs">
          <button
            type="button"
            className={
              inspectorTab === "general"
                ? "active"
                : ""
            }
            onClick={() =>
              setInspectorTab("general")
            }
          >
            Geral
          </button>
          <button
            type="button"
            className={
              inspectorTab === "transform"
                ? "active"
                : ""
            }
            onClick={() =>
              setInspectorTab("transform")
            }
          >
            Transformação
          </button>
          <button
            type="button"
            className={
              inspectorTab === "geometry"
                ? "active"
                : ""
            }
            onClick={() =>
              setInspectorTab("geometry")
            }
          >
            Geometria
          </button>
          <button
            type="button"
            className={
              inspectorTab === "materials"
                ? "active"
                : ""
            }
            onClick={() =>
              setInspectorTab("materials")
            }
          >
            Materiais
          </button>
        </div>

        {inspectorTab === "general" && (
          <dl className="property-list dense">
            <div>
              <dt>Arquivo</dt>
              <dd>
                {getObjectName(
                  selectedObject.sceneryObjectPath
                )}
              </dd>
            </div>
            <div>
              <dt>Caminho</dt>
              <dd>
                {selectedObject.sceneryObjectPath}
              </dd>
            </div>
            <div>
              <dt>ID</dt>
              <dd>{selectedObject.objectId}</dd>
            </div>
            <div>
              <dt>Grupos</dt>
              <dd>
                {selectedMetadata?.groups.length
                  ? selectedMetadata.groups.join(" › ")
                  : loadingMetadataFor
                    ? "Carregando..."
                    : "Não informado"}
              </dd>
            </div>
          </dl>
        )}

        {inspectorTab === "transform" && (
          <dl className="property-list dense">
            <div>
              <dt>Tile</dt>
              <dd>
                {selectedObject.tileX},{" "}
                {selectedObject.tileY}
              </dd>
            </div>
            <div>
              <dt>Posição local X / Y / Z</dt>
              <dd>
                {formatNumber(selectedObject.x)} /{" "}
                {formatNumber(selectedObject.y)} /{" "}
                {formatNumber(selectedObject.z)}
              </dd>
            </div>
            {selectedObjectGlobal && (
              <div>
                <dt>
                  Posição global X / Y / Z
                </dt>
                <dd>
                  {formatNumber(
                    selectedObjectGlobal.x
                  )}{" / "}
                  {formatNumber(
                    selectedObjectGlobal.y
                  )}{" / "}
                  {formatNumber(
                    selectedObjectGlobal.z
                  )}
                </dd>
              </div>
            )}
            <div>
              <dt>Rotação</dt>
              <dd>
                {formatNumber(
                  selectedObject.rotation
                )}°
              </dd>
            </div>
            <div>
              <dt>Pitch / Bank</dt>
              <dd>
                {formatNumber(
                  selectedObject.pitch
                )}° /{" "}
                {formatNumber(
                  selectedObject.bank
                )}°
              </dd>
            </div>
          </dl>
        )}

        {inspectorTab === "geometry" && (
          <div className="inspector-stack">
            <dl className="property-list dense">
              <div>
                <dt>Meshes carregados</dt>
                <dd>
                  {geometryStats?.loadedMeshes ??
                    (loadingGeometryFor
                      ? "Carregando..."
                      : 0)}
                </dd>
              </div>
              <div>
                <dt>Vértices</dt>
                <dd>
                  {geometryStats?.vertices ?? 0}
                </dd>
              </div>
              <div>
                <dt>Triângulos</dt>
                <dd>
                  {geometryStats?.triangles ?? 0}
                </dd>
              </div>
              <div>
                <dt>Materiais</dt>
                <dd>
                  {geometryStats?.materials ?? 0}
                </dd>
              </div>
            </dl>

            <div className="mesh-list">
              {selectedMetadata?.meshes.map(
                (mesh) => (
                  <div
                    className="mesh-row"
                    key={mesh.declaredPath}
                  >
                    <strong>
                      {getObjectName(
                        mesh.declaredPath
                      )}
                    </strong>
                    <span>
                      {mesh.fileExists
                        ? "Encontrado"
                        : "Ausente"}
                    </span>
                  </div>
                )
              )}
            </div>
          </div>
        )}

        {inspectorTab === "materials" && (
          <div className="material-list">
            {materialRows.length === 0 ? (
              <div className="inspector-empty">
                {loadingGeometryFor
                  ? "Carregando materiais..."
                  : "Nenhum material O3D disponível."}
              </div>
            ) : (
              materialRows.map((row) => (
                <div
                  className="material-row"
                  key={`${row.mesh}-${row.index}`}
                >
                  <span
                    className="material-swatch"
                    style={{
                      background: toRgb(
                        row.material.diffuseR,
                        row.material.diffuseG,
                        row.material.diffuseB
                      )
                    }}
                  />
                  <div>
                    <strong>
                      Material {row.index + 1}
                    </strong>
                    <span>{row.mesh}</span>
                    <small>
                      {row.material.textureName ??
                        "Sem textura declarada"}
                    </small>
                  </div>
                </div>
              ))
            )}
          </div>
        )}
      </>
    );
  };

  const renderEditor = () => {
    if (!selectedMap) {
      return renderMapPage();
    }

    return (
      <section className="map-editor">
        <div className="editor-titlebar">
          <div>
            <strong>
              OMSI Map Studio —{" "}
              {selectedMap.displayName}
            </strong>
            <span className="map-open-indicator">
              ● Mapa aberto
            </span>
          </div>
        </div>

        <div className="editor-menubar">
          {[
            "Arquivo",
            "Editar",
            "Visualizar",
            "Objetos",
            "Terreno",
            "Splines",
            "Mapa",
            "Ferramentas",
            "Ajuda"
          ].map((item) => (
            <button
              key={item}
              type="button"
              disabled
            >
              {item}
            </button>
          ))}
        </div>

        <div className="editor-toolbar">
          <button
            type="button"
            className="tool active"
            title="Seleção"
          >
            ↖
          </button>
          <button
            type="button"
            className="tool"
            disabled
            title="Mover"
          >
            ✥
          </button>
          <button
            type="button"
            className="tool"
            disabled
            title="Rotacionar"
          >
            ⟳
          </button>
          <button
            type="button"
            className="tool"
            disabled
            title="Escala"
          >
            ◫
          </button>

          <span className="toolbar-separator" />

          <span className="toolbar-chip">
            Global
          </span>
          <span className="toolbar-chip">
            Perspectiva
          </span>

          <span className="toolbar-spacer" />

          <button
            type="button"
            className="secondary-action"
            onClick={handleOpenMap}
            disabled={busy}
          >
            Abrir outro mapa
          </button>
        </div>

        <div className="editor-grid">
          <aside className="map-explorer">
            <div className="explorer-tabs">
              <button
                type="button"
                className="active"
              >
                Explorador
              </button>
              <button
                type="button"
                disabled
              >
                Camadas
              </button>
              <button
                type="button"
                disabled
              >
                Favoritos
              </button>
            </div>

            <div className="explorer-tree">
              <div className="tree-root">
                <span>▾</span>
                <strong>
                  {selectedMap.displayName}
                </strong>
              </div>

              <div className="tree-node active">
                <span>▣</span>
                Objetos
                <strong>
                  {selectedStats?.objects ??
                    objects.length}
                </strong>
              </div>

              <div className="tree-node">
                <span>⌇</span>
                Splines
                <strong>
                  {selectedStats?.splines ??
                    splines.length}
                </strong>
              </div>

              <div className="tree-node disabled">
                <span>▧</span>
                Terreno
                <small>em desenvolvimento</small>
              </div>

              <div className="tree-node disabled">
                <span>◩</span>
                Texturas
                <small>em desenvolvimento</small>
              </div>

              <div className="tree-node disabled">
                <span>◎</span>
                Rotas
                <small>em desenvolvimento</small>
              </div>

              <div className="tree-node">
                <span>□</span>
                Tiles
                <strong>
                  {selectedMap.tiles.length}
                </strong>
              </div>
            </div>

            <div className="explorer-search">
              <input
                type="search"
                placeholder="Buscar no mapa..."
                disabled
              />
            </div>
          </aside>

          <section className="editor-viewport">
            <Viewport
              tiles={selectedMap.tiles}
              objects={objects}
              splines={splines}
              usesWorldCoordinates={
                selectedMap.usesWorldCoordinates
              }
              selectedObject={selectedObject}
              selectedGeometry={selectedGeometry}
              onSelectObject={
                handleObjectSelection
              }
            />

            <div className="viewport-toolbar">
              <span>Perspectiva</span>
              <span>Iluminação</span>
            </div>

            <div className="viewport-layers">
              <label>
                <input
                  type="checkbox"
                  checked
                  readOnly
                />
                Grelha
              </label>
              <label>
                <input
                  type="checkbox"
                  checked
                  readOnly
                />
                Objetos
              </label>
              <label>
                <input
                  type="checkbox"
                  checked
                  readOnly
                />
                Splines
              </label>
              <label className="muted">
                <input
                  type="checkbox"
                  disabled
                />
                Terreno
              </label>
            </div>

            {loadingMapContent && (
              <div className="viewport-loading">
                Lendo tiles, objetos e splines...
              </div>
            )}
          </section>

          <aside className="object-inspector">
            <div className="inspector-heading">
              <strong>Inspetor</strong>
              <span>
                {selectedObject
                  ? "Objeto selecionado"
                  : "Mapa aberto"}
              </span>
            </div>

            {renderObjectInspector()}
          </aside>
        </div>

        <footer className="editor-statusbar">
          <span>
            {error
              ? "Erro"
              : loadingMapContent
                ? "Carregando mapa..."
                : loadingMetadataFor
                  ? "Lendo SCO..."
                  : loadingGeometryFor
                    ? "Lendo geometria..."
                    : "Pronto"}
          </span>

          <span>
            Objetos:{" "}
            {selectedStats?.objects ??
              objects.length}
            <b>·</b>
            Splines:{" "}
            {selectedStats?.splines ??
              splines.length}
            <b>·</b>
            Tiles: {selectedMap.tiles.length}
          </span>
        </footer>
      </section>
    );
  };

  const renderPlaceholder = (
    title: string,
    description: string
  ) => (
    <section className="page-shell">
      <div className="page-title compact">
        <span className="eyebrow">
          EM DESENVOLVIMENTO
        </span>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>

      <article className="placeholder-card">
        <strong>
          Ainda não disponível nesta alpha
        </strong>
        <span>
          A interface já reserva este espaço,
          mas nenhum estado fictício será usado.
        </span>
      </article>
    </section>
  );

  return (
    <main className="studio-shell">
      {renderNav()}

      <section className="studio-main">
        <header className="studio-topbar">
          <div className="topbar-tagline">
            <span>CRIAR</span>
            <b>·</b>
            <span>EDITAR</span>
            <b>·</b>
            <span>EXPLORAR</span>
          </div>

          <div className="topbar-state">
            {selectedMap ? (
              <>
                <span className="state-dot" />
                {selectedMap.displayName}
              </>
            ) : rootPath ? (
              "OMSI conectado"
            ) : (
              "Nenhum projeto aberto"
            )}
          </div>
        </header>

        {error && view !== "editor" && (
          <div className="global-error">
            {error}
          </div>
        )}

        <div className="studio-content">
          {view === "home" &&
            renderHome()}
          {view === "omsi" &&
            renderOmsiPage()}
          {view === "map" &&
            renderMapPage()}
          {view === "editor" &&
            renderEditor()}
          {view === "tools" &&
            renderPlaceholder(
              "Ferramentas",
              "Ferramentas de construção e diagnóstico serão ativadas conforme o Core ganhar suporte real."
            )}
          {view === "settings" &&
            renderPlaceholder(
              "Configurações",
              "Preferências do editor, idioma e opções visuais ainda serão implementadas."
            )}
        </div>
      </section>
    </main>
  );
}
