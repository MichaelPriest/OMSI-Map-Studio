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
  type OmsiSceneryObjectGeometry,
  type OmsiSceneryObjectMetadata
} from "./bridge/desktopBridge";
import { Viewport } from "./editor/Viewport";

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

const describeMesh = (
  mesh: {
    declaredPath: string;
    fileExists: boolean;
    o3d: {
      isValid: boolean;
      version: number | null;
      isEncrypted: boolean;
    } | null;
    structure: {
      isParsed: boolean;
      vertexCount: number;
      triangleCount: number;
      materialCount: number;
      boneCount: number;
    } | null;
  }
) => {
  if (!mesh.fileExists) {
    return `${mesh.declaredPath} · ausente`;
  }

  if (!mesh.o3d) {
    return `${mesh.declaredPath} · encontrado`;
  }

  if (!mesh.o3d.isValid) {
    return `${mesh.declaredPath} · cabeçalho O3D inválido`;
  }

  const version =
    mesh.o3d.version === null
      ? "versão desconhecida"
      : `v${mesh.o3d.version}`;

  const structure =
    mesh.structure?.isParsed
      ? ` · ${mesh.structure.vertexCount} vértices · ${mesh.structure.triangleCount} triângulos · ${mesh.structure.materialCount} materiais`
      : "";

  return `${mesh.declaredPath} · O3D ${version}${mesh.o3d.isEncrypted ? " · criptografado" : ""}${structure}`;
};

export function App() {
  const bridgeAvailable = useMemo(
    () => isDesktopBridgeAvailable(),
    []
  );

  const [rootPath, setRootPath] =
    useState<string>();

  const [selectedMap, setSelectedMap] =
    useState<OmsiMap>();

  const [objects, setObjects] =
    useState<OmsiPlacedObject[]>([]);

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
          setSelectedObject(undefined);
          setSceneryMetadataByPath({});
          setGeometryByPath({});
          setSelectingRoot(false);
          setSelectingMap(false);
          setLoadingMapContent(false);
          setError(undefined);
          return;
        }

        if (message.type === "mapOpened") {
          setSelectedMap(message.map);
          setObjects([]);
          setSelectedObject(undefined);
          setSceneryMetadataByPath({});
          setGeometryByPath({});
          setSelectingMap(false);
          setLoadingMapContent(false);
          setError(undefined);
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
          )
      }),
      {
        loadedMeshes: 0,
        vertices: 0,
        triangles: 0
      }
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

  return (
    <main className="editor-shell">
      <header className="topbar">
        <div className="brand">
          <strong>
            OMSI Map Studio
            <span className="version-badge">
              v{appVersion}
            </span>
          </strong>
          <span>
            Alpha somente leitura · OMSI 2
          </span>
        </div>

        <nav
          className="toolbar"
          aria-label="Ferramentas principais"
        >
          <button
            type="button"
            onClick={handleOpenOmsi}
            disabled={busy}
          >
            {selectingRoot
              ? "Selecionando OMSI..."
              : rootPath
                ? "Trocar OMSI"
                : "Abrir OMSI"}
          </button>

          <button
            type="button"
            onClick={handleOpenMap}
            disabled={!rootPath || busy}
          >
            {selectingMap
              ? "Selecionando mapa..."
              : "Abrir mapa"}
          </button>

          <button
            type="button"
            disabled
          >
            Novo mapa
          </button>

          <button
            type="button"
            disabled
          >
            Salvar
          </button>
        </nav>
      </header>

      <aside className="asset-panel">
        <div className="panel-heading">
          <span>Projeto</span>
          <small>
            {rootPath
              ? "OMSI conectado"
              : "Nenhum OMSI selecionado"}
          </small>
        </div>

        {error && (
          <div className="error-panel">
            {error}
          </div>
        )}

        {!rootPath ? (
          <div className="empty-panel">
            Clique em <strong>Abrir OMSI</strong>{" "}
            e selecione a pasta raiz da
            instalação. Nenhum mapa será
            carregado automaticamente.
          </div>
        ) : (
          <dl className="property-list">
            <div>
              <dt>Instalação OMSI</dt>
              <dd>{rootPath}</dd>
            </div>

            <div>
              <dt>Mapa aberto</dt>
              <dd>
                {selectedMap
                  ? selectedMap.displayName
                  : "Nenhum"}
              </dd>
            </div>
          </dl>
        )}

        {rootPath && !selectedMap && (
          <div className="empty-panel">
            Agora clique em{" "}
            <strong>Abrir mapa</strong>{" "}
            e escolha uma pasta dentro de{" "}
            <code>maps</code>.
          </div>
        )}

        {selectedMap && (
          <div className="empty-panel">
            <strong>
              {selectedMap.displayName}
            </strong>
            <br />
            {selectedMap.directoryName}
            <br />
            {selectedMap.tiles.length} tiles
            {selectedMap.usesWorldCoordinates
              ? " · coordenadas mundiais"
              : ""}
          </div>
        )}
      </aside>

      <section className="viewport-panel">
        <Viewport
          tiles={
            selectedMap?.tiles ?? []
          }
          objects={
            selectedMap ? objects : []
          }
          usesWorldCoordinates={
            selectedMap
              ?.usesWorldCoordinates ??
            false
          }
          selectedObject={selectedObject}
          selectedGeometry={selectedGeometry}
          onSelectObject={
            handleObjectSelection
          }
        />

        <div className="viewport-hint">
          <strong>
            {selectedMap?.displayName ??
              "Nenhum mapa aberto"}
          </strong>

          <span>
            {!selectedMap
              ? "Use Abrir mapa para escolher o mapa que deseja editar."
              : loadingMapContent
                ? "Lendo tiles, objetos e splines..."
                : selectedMap.usesWorldCoordinates
                  ? `${selectedMap.tiles.length} tiles · visualização esquemática · ${objects.length} objetos lidos`
                  : `${selectedMap.tiles.length} tiles · 300 m · ${objects.length} posições de objetos`}
          </span>

          {selectedObject && (
            <span>
              Selecionado:{" "}
              {selectedDisplayName} #
              {selectedObject.objectId}
            </span>
          )}
        </div>
      </section>

      <aside className="inspector-panel">
        <div className="panel-heading">
          <span>Propriedades</span>
          <small>
            {selectedObject
              ? `Objeto #${selectedObject.objectId}`
              : selectedMap
                ? "Mapa selecionado"
                : "Nenhuma seleção"}
          </small>
        </div>

        {selectedObject ? (
          <>
            <dl className="property-list">
              <div>
                <dt>Objeto</dt>
                <dd>{selectedDisplayName}</dd>
              </div>

              <div>
                <dt>Arquivo SCO</dt>
                <dd>
                  {selectedObject
                    .sceneryObjectPath}
                </dd>
              </div>

              <div>
                <dt>ID</dt>
                <dd>
                  {selectedObject.objectId}
                </dd>
              </div>

              <div>
                <dt>Tile</dt>
                <dd>
                  {selectedObject.tileX},{" "}
                  {selectedObject.tileY}
                </dd>
              </div>

              <div>
                <dt>
                  Posição local X / Y / Z
                </dt>
                <dd>
                  {formatNumber(
                    selectedObject.x
                  )}{" / "}
                  {formatNumber(
                    selectedObject.y
                  )}{" / "}
                  {formatNumber(
                    selectedObject.z
                  )}
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
                <dt>Pitch</dt>
                <dd>
                  {formatNumber(
                    selectedObject.pitch
                  )}°
                </dd>
              </div>

              <div>
                <dt>Bank</dt>
                <dd>
                  {formatNumber(
                    selectedObject.bank
                  )}°
                </dd>
              </div>

              {loadingMetadataFor ===
                selectedObject.sceneryObjectPath && (
                <div>
                  <dt>Metadados SCO</dt>
                  <dd>Carregando...</dd>
                </div>
              )}

              {selectedGeometry && (
                <div>
                  <dt>Geometria O3D</dt>
                  <dd>
                    {geometryStats?.loadedMeshes ?? 0} meshes ·{" "}
                    {geometryStats?.vertices ?? 0} vértices ·{" "}
                    {geometryStats?.triangles ?? 0} triângulos
                  </dd>
                </div>
              )}

              {loadingGeometryFor ===
                selectedObject.sceneryObjectPath && (
                <div>
                  <dt>Geometria O3D</dt>
                  <dd>Carregando preview...</dd>
                </div>
              )}

              {selectedMetadata && (
                <>
                  <div>
                    <dt>Friendly name</dt>
                    <dd>
                      {selectedMetadata
                        .friendlyName ??
                        "Não informado"}
                    </dd>
                  </div>

                  <div>
                    <dt>Grupos</dt>
                    <dd>
                      {selectedMetadata
                        .groups.length
                        ? selectedMetadata
                            .groups
                            .join(" › ")
                        : "Nenhum"}
                    </dd>
                  </div>

                  <div>
                    <dt>Meshes</dt>
                    <dd>
                      {selectedMetadata
                        .meshes.length
                        ? selectedMetadata
                            .meshes
                            .map(describeMesh)
                            .join(", ")
                        : "Nenhum"}
                    </dd>
                  </div>
                </>
              )}
            </dl>

            <div className="empty-panel">
              A visualização é somente
              leitura nesta alpha.
            </div>
          </>
        ) : selectedMap &&
          selectedStats ? (
          <dl className="property-list">
            <div>
              <dt>Nome</dt>
              <dd>
                {selectedMap.displayName}
              </dd>
            </div>

            <div>
              <dt>Pasta</dt>
              <dd>
                {selectedMap.directoryName}
              </dd>
            </div>

            <div>
              <dt>Sistema</dt>
              <dd>
                {selectedMap
                  .usesWorldCoordinates
                  ? "Coordenadas mundiais"
                  : "Coordenadas cartesianas"}
              </dd>
            </div>

            <div>
              <dt>Tiles</dt>
              <dd>
                {selectedMap.tiles.length}
              </dd>
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
              <dd>
                {selectedStats.attachments}
              </dd>
            </div>

            <div>
              <dt>Tiles ausentes</dt>
              <dd>
                {selectedStats.missingTiles}
              </dd>
            </div>
          </dl>
        ) : selectedMap ? (
          <div className="empty-panel">
            Lendo o mapa escolhido...
          </div>
        ) : (
          <div className="empty-panel">
            Nenhum mapa aberto. Use{" "}
            <strong>Abrir mapa</strong>.
          </div>
        )}
      </aside>

      <footer className="statusbar">
        <span>
          {error
            ? "Erro"
            : selectingRoot
              ? "Selecionando OMSI..."
              : selectingMap
                ? "Selecionando mapa..."
                : loadingMapContent
                  ? "Lendo mapa..."
                  : loadingMetadataFor
                    ? "Lendo SCO..."
                    : loadingGeometryFor
                      ? "Lendo geometria..."
                      : "Pronto"}
        </span>

        <span>
          {selectedMap
            ? selectedMap.displayName
            : rootPath
              ? "OMSI pronto · sem mapa aberto"
              : "Sem OMSI selecionado"}
        </span>
      </footer>
    </main>
  );
}
