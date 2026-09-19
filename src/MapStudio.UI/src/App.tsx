import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState
} from "react";
import {
  deleteObject,
  deleteSpline,
  getGroundTextureAssetKey,
  getTerrainTextureMaskAssetKey,
  getSceneryTextureAssetKey,
  getSplineTextureAssetKey,
  insertObject,
  insertSpline,
  insertSplineFromLibrary,
  isDesktopBridgeAvailable,
  loadMapFull,
  loadMapRegion,
  loadGroundTextureAsset,
  loadTerrainTextureMaskAsset,
  loadSceneryLibrary,
  loadSceneryTextureAsset,
  loadSplineLibrary,
  loadSplineTextureAsset,
  loadSplineProfile,
  loadSceneryObjectGeometry,
  loadSceneryObjectMetadata,
  saveObjectTransforms,
  saveSplineTransforms,
  selectMap,
  selectOmsiRoot,
  setFullScreen,
  subscribeToHost,
  updateSplineLinks,
  type SceneryLibraryEntry,
  type SplineLibraryEntry,
  type OmsiMap,
  type OmsiPlacedObject,
  type OmsiPlacedSpline,
  type OmsiSplineDefinition,
  type OmsiSceneryObjectGeometry,
  type OmsiSceneryObjectMetadata,
  type OmsiTextureAsset
} from "./bridge/desktopBridge";
import { Viewport } from "./editor/Viewport";

type AppView =
  | "home"
  | "omsi"
  | "map"
  | "editor"
  | "tools"
  | "settings";

type MapLoadMode =
  | "full"
  | "performance";

type EditorTool =
  | "select"
  | "move"
  | "rotate";

type ViewportCameraAction = {
  type:
    | "fit"
    | "focus"
    | "perspective"
    | "top";
  token: number;
};

type PreviewTransformHistoryEntry = {
  key: string;
  before: OmsiPlacedObject;
  after: OmsiPlacedObject;
  hadPreviewBefore: boolean;
};

type PendingObjectPlacement = {
  tileX: number;
  tileY: number;
  x: number;
  y: number;
  z: number;
  rotation: number;
  pitch: number;
  bank: number;
};

type PlacementTransformDefaults = Pick<
  PendingObjectPlacement,
  "z" | "rotation" | "pitch" | "bank"
>;

const defaultPlacementTransform:
  PlacementTransformDefaults = {
    z: 0,
    rotation: 0,
    pitch: 0,
    bank: 0
  };

const nearbyObjectPathLimit = 64;
const nearbySplinePathLimit = 12;
const autoObjectTextureLimit = 16;
const autoSplineTextureLimit = 8;
const autoObjectTextureBatch = 4;
const autoSplineTextureBatch = 2;
const autoTextureLimit =
  autoObjectTextureLimit +
  autoSplineTextureLimit;

const maxTextureCacheEntries = 64;
const maxGroundTextureCacheEntries = 32;
const maxTerrainMaskCacheEntries = 96;

type PendingSplinePlacement = {
  targetTileX: number;
  targetTileY: number;
  x: number;
  y: number;
  z: number;
  rotation: number;
  length: number;
  radius: number;
  gradientStart: number;
  gradientEnd: number;
};

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
  unknownSpline:
    "A spline solicitada não pertence à área atualmente carregada.",
  invalidSplinePath:
    "A referência da spline não pôde ser resolvida com segurança na pasta Splines.",
  saveConflict:
    "O arquivo do mapa mudou ou o objeto não corresponde mais à versão aberta. O salvamento foi cancelado para proteger o mapa.",
  saveError:
    "Não foi possível salvar as alterações. O backup criado foi mantido quando possível.",
  objectInsertTemplateUnavailable:
    "Nesta alpha, um objeto novo só pode ser gravado se o mesmo arquivo .sco já existir no mapa. A prévia continua disponível.",
  objectInsertionWorldCoordinatesUnsupported:
    "Inserção de objetos ainda não está disponível em mapas com [worldcoordinates].",
  objectIdExhausted:
    "Não foi possível gerar um novo ID global para o objeto.",
  objectInsertError:
    "Não foi possível inserir o objeto com segurança. Nenhum tile deve ser sobrescrito sem backup.",
  objectDeleteConflict:
    "O objeto mudou no arquivo desde a leitura. A exclusão foi cancelada para proteger o mapa.",
  objectDeleteError:
    "Não foi possível excluir o objeto com segurança. O tile original foi preservado quando possível.",
  splineInsertConflict:
    "A spline de origem mudou no arquivo desde a leitura. A cópia foi cancelada.",
  splineInsertError:
    "Não foi possível inserir a spline com segurança.",
  splineInsertTemplateUnavailable:
    "A prévia é válida, mas este mapa não possui um template neutro explícito do mesmo tipo ([spline] ou [spline_h]) para derivar com segurança header e extras. A gravação foi bloqueada.",
  splineIdExhausted:
    "Não há mais IDs inteiros disponíveis para criar uma nova spline.",
  splineInsertionWorldCoordinatesUnsupported:
    "A colocação de spline em mapas com [worldcoordinates] ainda não é suportada nesta alpha.",
  splineDeleteLinked:
    "Não foi possível validar com segurança os vínculos da spline antes da exclusão.",
  splineDeleteConflict:
    "A spline ou um de seus vizinhos mudou no arquivo. A exclusão foi cancelada sem deixar a cadeia parcialmente alterada.",
  splineDeleteError:
    "Não foi possível excluir a spline com segurança.",
  splineLinkTargetBusy:
    "A ponta escolhida da spline vizinha já está ligada a outra spline.",
  splineLinkTargetMissing:
    "O ID de uma spline vizinha não existe mais no mapa.",
  splineLinkInvalid:
    "Os vínculos são inválidos: uma spline não pode apontar para si mesma nem usar o mesmo vizinho nas duas pontas.",
  splineLinkConflict:
    "A cadeia de splines mudou no disco ou já estava inconsistente. Nenhum vínculo foi gravado.",
  splineLinkError:
    "Não foi possível atualizar os vínculos das splines com segurança.",
  unknownTile:
    "O tile escolhido não pertence ao mapa aberto.",
  invalidTilePath:
    "O arquivo do tile não pôde ser resolvido com segurança.",
  mapOpenError:
    "Não foi possível abrir esse mapa.",
  unexpectedHostError:
    "O host desktop encontrou um erro inesperado."
};

const appVersion = "0.1.0-alpha.3";
const tileStreamRadius = 1;

const formatNumber = (value: number) =>
  value.toLocaleString("pt-BR", {
    maximumFractionDigits: 3
  });

const getObjectName = (path: string) =>
  path.split(/[\\/]/).filter(Boolean).at(-1) ?? path;

const formatFileSize = (
  bytes: number
) => {
  if (bytes >= 1024 * 1024) {
    return `${(
      bytes /
      (1024 * 1024)
    ).toFixed(1)} MiB`;
  }

  if (bytes >= 1024) {
    return `${(
      bytes / 1024
    ).toFixed(1)} KiB`;
  }

  return `${bytes} B`;
};

const normalizeTextureFileName = (
  value: string
) =>
  value
    .replace(/\\/g, "/")
    .split("/")
    .at(-1)
    ?.toLocaleLowerCase("en-US") ??
  value.toLocaleLowerCase("en-US");

const findSceneryMaterialOverride = (
  mesh:
    OmsiSceneryObjectGeometry["meshes"][number],
  materialIndex: number
) => {
  const textureName =
    mesh.geometry.materials[
      materialIndex
    ]?.textureName;

  if (!textureName) {
    return undefined;
  }

  const normalized =
    normalizeTextureFileName(
      textureName
    );

  return mesh.materialOverrides.find(
    (override) =>
      override.materialIndex ===
        materialIndex &&
      normalizeTextureFileName(
        override.textureName
      ) === normalized
  );
};

const getPlacedObjectKey = (
  placedObject: OmsiPlacedObject
) =>
  [
    placedObject.tileX,
    placedObject.tileY,
    placedObject.objectId,
    placedObject.sourceSectionOrdinal,
    placedObject.sceneryObjectPath
  ].join("|");

const sameObjectTransform = (
  left: OmsiPlacedObject,
  right: OmsiPlacedObject
) =>
  left.x === right.x &&
  left.y === right.y &&
  left.z === right.z &&
  left.rotation === right.rotation &&
  left.pitch === right.pitch &&
  left.bank === right.bank;

const getPlacedSplineKey = (
  placedSpline: OmsiPlacedSpline
) =>
  [
    placedSpline.tileX,
    placedSpline.tileY,
    placedSpline.splineId,
    placedSpline.sourceSectionOrdinal,
    placedSpline.splinePath,
    placedSpline.isHeightSpline
  ].join("|");

const sameSplineTransform = (
  left: OmsiPlacedSpline,
  right: OmsiPlacedSpline
) =>
  left.x === right.x &&
  left.y === right.y &&
  left.z === right.z &&
  left.rotation === right.rotation &&
  left.length === right.length &&
  left.radius === right.radius &&
  left.gradientStart ===
    right.gradientStart &&
  left.gradientEnd ===
    right.gradientEnd;

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

  const [mapLoadMode, setMapLoadMode] =
    useState<MapLoadMode>("full");

  const [editorTool, setEditorTool] =
    useState<EditorTool>("select");

  const [showGrid, setShowGrid] =
    useState(true);

  const [showTerrain, setShowTerrain] =
    useState(true);

  const [
    hiddenTerrainLayerIndices,
    setHiddenTerrainLayerIndices
  ] = useState<
    Record<number, true>
  >({});

  const [
    showTerrainPaint,
    setShowTerrainPaint
  ] = useState(true);

  const [showObjects, setShowObjects] =
    useState(true);

  const [showSplines, setShowSplines] =
    useState(true);

  const [
    showSplineProfiles,
    setShowSplineProfiles
  ] = useState(true);

  const [
    nightPreviewEnabled,
    setNightPreviewEnabled
  ] = useState(false);

  const [
    cameraMode,
    setCameraMode
  ] = useState<
    "perspective" | "top"
  >("perspective");

  const [snapEnabled, setSnapEnabled] =
    useState(true);

  const [moveSnap, setMoveSnap] =
    useState(0.5);

  const [rotationSnap, setRotationSnap] =
    useState(5);

  const [
    cameraAction,
    setCameraAction
  ] = useState<ViewportCameraAction>();

  const [
    isFullScreen,
    setIsFullScreen
  ] = useState(false);

  const [
    previewObjectTransforms,
    setPreviewObjectTransforms
  ] = useState<
    Record<string, OmsiPlacedObject>
  >({});

  const [
    previewSplineTransforms,
    setPreviewSplineTransforms
  ] = useState<
    Record<string, OmsiPlacedSpline>
  >({});

  const [
    savingSpline,
    setSavingSpline
  ] = useState(false);

  const [
    savingSplineLinks,
    setSavingSplineLinks
  ] = useState(false);

  const [
    splineLinkPreviousId,
    setSplineLinkPreviousId
  ] = useState(-1);

  const [
    splineLinkNextId,
    setSplineLinkNextId
  ] = useState(-1);

  const [
    undoPreviewStack,
    setUndoPreviewStack
  ] = useState<
    PreviewTransformHistoryEntry[]
  >([]);

  const [
    redoPreviewStack,
    setRedoPreviewStack
  ] = useState<
    PreviewTransformHistoryEntry[]
  >([]);

  const [
    loadingFullMap,
    setLoadingFullMap
  ] = useState(false);

  const [
    fullMapProgress,
    setFullMapProgress
  ] = useState<{
    completed: number;
    total: number;
  }>();

  const [
    loadedFullMapFor,
    setLoadedFullMapFor
  ] = useState<string>();

  const [activeTile, setActiveTile] =
    useState<{
      x: number;
      y: number;
    }>();

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

  const [selectedSpline, setSelectedSpline] =
    useState<OmsiPlacedSpline>();

  const [
    splineProfilesByPath,
    setSplineProfilesByPath
  ] = useState<
    Record<string, OmsiSplineDefinition>
  >({});

  useEffect(() => {
    if (!selectedSpline) {
      setSplineLinkPreviousId(-1);
      setSplineLinkNextId(-1);
      return;
    }

    setSplineLinkPreviousId(
      selectedSpline.previousSplineId
    );
    setSplineLinkNextId(
      selectedSpline.nextSplineId
    );
  }, [
    selectedSpline?.splineId,
    selectedSpline?.previousSplineId,
    selectedSpline?.nextSplineId
  ]);

  const [selectingRoot, setSelectingRoot] =
    useState(false);

  const [selectingMap, setSelectingMap] =
    useState(false);

  const [
    loadingRegionKey,
    setLoadingRegionKey
  ] = useState<string>();

  const [
    loadedRegionKey,
    setLoadedRegionKey
  ] = useState<string>();

  const [
    loadingSplineFor,
    setLoadingSplineFor
  ] = useState<string>();

  const [
    preloadingSplineProfileFor,
    setPreloadingSplineProfileFor
  ] = useState<string>();

  const [
    loadingMetadataFor,
    setLoadingMetadataFor
  ] = useState<string>();

  const [
    loadingGeometryFor,
    setLoadingGeometryFor
  ] = useState<string>();

  const [
    preloadingGeometryFor,
    setPreloadingGeometryFor
  ] = useState<string>();

  const [error, setError] =
    useState<string>();

  const [
    explorerSearch,
    setExplorerSearch
  ] = useState("");

  const [
    explorerPanelTab,
    setExplorerPanelTab
  ] = useState<
    "map" | "library" | "splineLibrary"
  >("map");

  const [
    sceneryLibrary,
    setSceneryLibrary
  ] = useState<
    SceneryLibraryEntry[]
  >([]);

  const [
    librarySearch,
    setLibrarySearch
  ] = useState("");

  const [
    loadingSceneryLibrary,
    setLoadingSceneryLibrary
  ] = useState(false);

  const [
    textureAssetsByKey,
    setTextureAssetsByKey
  ] = useState<
    Record<string, OmsiTextureAsset>
  >({});

  const [
    groundTextureAssetsByKey,
    setGroundTextureAssetsByKey
  ] = useState<
    Record<string, OmsiTextureAsset>
  >({});

  const [
    requestedGroundTextureKeys,
    setRequestedGroundTextureKeys
  ] = useState<
    Record<string, true>
  >({});

  const [
    terrainMaskAssetsByKey,
    setTerrainMaskAssetsByKey
  ] = useState<
    Record<string, OmsiTextureAsset>
  >({});

  const [
    requestedTerrainMaskKeys,
    setRequestedTerrainMaskKeys
  ] = useState<
    Record<string, true>
  >({});

  const [
    requestedTextureKeys,
    setRequestedTextureKeys
  ] = useState<
    Record<string, true>
  >({});

  const textureCacheOrderRef =
    useRef<string[]>([]);

  const groundTextureCacheOrderRef =
    useRef<string[]>([]);

  const terrainMaskCacheOrderRef =
    useRef<string[]>([]);

  const [
    autoPrefetchedTextureKeys,
    setAutoPrefetchedTextureKeys
  ] = useState<
    Record<string, true>
  >({});

  const [
    splineLibrary,
    setSplineLibrary
  ] = useState<
    SplineLibraryEntry[]
  >([]);

  const [
    splineLibrarySearch,
    setSplineLibrarySearch
  ] = useState("");

  const [
    loadingSplineLibrary,
    setLoadingSplineLibrary
  ] = useState(false);

  const [
    splineLibraryPlacementAsset,
    setSplineLibraryPlacementAsset
  ] = useState<
    SplineLibraryEntry
  >();

  const [
    splineLibraryPlacementIsHeight,
    setSplineLibraryPlacementIsHeight
  ] = useState(false);

  const [
    placementAsset,
    setPlacementAsset
  ] = useState<
    SceneryLibraryEntry
  >();

  const [
    pendingPlacement,
    setPendingPlacement
  ] = useState<
    PendingObjectPlacement
  >();

  const [
    placementTransformDefaults,
    setPlacementTransformDefaults
  ] = useState<
    PlacementTransformDefaults
  >(defaultPlacementTransform);

  const [
    insertingObject,
    setInsertingObject
  ] = useState(false);

  const [
    deletingObject,
    setDeletingObject
  ] = useState(false);

  const [
    splinePlacementTemplate,
    setSplinePlacementTemplate
  ] = useState<OmsiPlacedSpline>();

  const [
    pendingSplinePlacement,
    setPendingSplinePlacement
  ] = useState<
    PendingSplinePlacement
  >();

  const [
    insertingSpline,
    setInsertingSpline
  ] = useState(false);

  const [
    deletingSpline,
    setDeletingSpline
  ] = useState(false);

  const [saving, setSaving] =
    useState(false);

  const [saveNotice, setSaveNotice] =
    useState<string>();

  useEffect(
    () =>
      subscribeToHost((message) => {
        if (
          message.type ===
          "fullScreenChanged"
        ) {
          setIsFullScreen(
            message.enabled
          );
          return;
        }

        if (
          message.type ===
          "omsiRootSelected"
        ) {
          setRootPath(message.rootPath);
          setSelectedMap(undefined);
          setHiddenTerrainLayerIndices({});
          setObjects([]);
          setSplines([]);
          setSelectedObject(undefined);
          setSelectedSpline(undefined);
          setSplineProfilesByPath({});
          setSceneryMetadataByPath({});
          setGeometryByPath({});
          setTextureAssetsByKey({});
          setGroundTextureAssetsByKey({});
          setRequestedGroundTextureKeys({});
          setTerrainMaskAssetsByKey({});
          setRequestedTerrainMaskKeys({});
          groundTextureCacheOrderRef.current =
            [];
          terrainMaskCacheOrderRef.current =
            [];
          setRequestedTextureKeys({});
          setAutoPrefetchedTextureKeys({});
          textureCacheOrderRef.current =
            [];
          setPreviewObjectTransforms({});
          setPreviewSplineTransforms({});
          setUndoPreviewStack([]);
          setRedoPreviewStack([]);
          setEditorTool("select");
          setPreloadingGeometryFor(undefined);
          setPreloadingSplineProfileFor(
            undefined
          );
          setSelectingRoot(false);
          setSelectingMap(false);
          setLoadingRegionKey(undefined);
          setLoadingFullMap(false);
          setFullMapProgress(undefined);
          setLoadedFullMapFor(undefined);
          setSaving(false);
          setSaveNotice(undefined);
          setExplorerSearch("");
          setExplorerPanelTab("map");
          setSceneryLibrary([]);
          setLibrarySearch("");
          setLoadingSceneryLibrary(false);
          setSplineLibrary([]);
          setSplineLibrarySearch("");
          setLoadingSplineLibrary(false);
          setSplineLibraryPlacementAsset(
            undefined
          );
          setSplineLibraryPlacementIsHeight(
            false
          );
          setPlacementAsset(undefined);
          setPendingPlacement(undefined);
          setInsertingObject(false);
          setDeletingObject(false);
          setSplinePlacementTemplate(
            undefined
          );
          setPendingSplinePlacement(
            undefined
          );
          setInsertingSpline(false);
          setDeletingSpline(false);
          setSavingSpline(false);
          setSavingSplineLinks(false);
          setError(undefined);
          setView("map");
          return;
        }

        if (message.type === "mapOpened") {
          setSelectedMap(message.map);
          setHiddenTerrainLayerIndices({});
          setShowTerrainPaint(true);
          setGroundTextureAssetsByKey({});
          setRequestedGroundTextureKeys({});
          setTerrainMaskAssetsByKey({});
          setRequestedTerrainMaskKeys({});
          setActiveTile(
            message.initialTile ??
              undefined
          );
          setObjects([]);
          setSplines([]);
          setSelectedObject(undefined);
          setSelectedSpline(undefined);
          setSplineProfilesByPath({});
          setSceneryMetadataByPath({});
          setGeometryByPath({});
          setTextureAssetsByKey({});
          setRequestedTextureKeys({});
          setAutoPrefetchedTextureKeys({});
          textureCacheOrderRef.current =
            [];
          setPreloadingGeometryFor(undefined);
          setPreloadingSplineProfileFor(
            undefined
          );
          setSelectingMap(false);
          setMapLoadMode("full");
          setEditorTool("select");
          setCameraMode("perspective");
          setPreviewObjectTransforms({});
          setPreviewSplineTransforms({});
          setUndoPreviewStack([]);
          setRedoPreviewStack([]);
          setLoadingRegionKey(undefined);
          setLoadedRegionKey(undefined);
          setLoadingFullMap(false);
          setFullMapProgress(undefined);
          setLoadedFullMapFor(undefined);
          setInspectorTab("general");
          setSaving(false);
          setSaveNotice(undefined);
          setExplorerSearch("");
          setExplorerPanelTab("map");
          setPlacementAsset(undefined);
          setPendingPlacement(undefined);
          setInsertingObject(false);
          setDeletingObject(false);
          setSplinePlacementTemplate(
            undefined
          );
          setSplineLibraryPlacementAsset(
            undefined
          );
          setSplineLibraryPlacementIsHeight(
            false
          );
          setPendingSplinePlacement(
            undefined
          );
          setInsertingSpline(false);
          setDeletingSpline(false);
          setSavingSpline(false);
          setSavingSplineLinks(false);
          setError(undefined);
          setView("editor");
          return;
        }

        if (
          message.type ===
          "sceneryLibraryLoaded"
        ) {
          setSceneryLibrary(
            message.entries
          );
          setLoadingSceneryLibrary(
            false
          );
          return;
        }

        if (
          message.type ===
          "splineLibraryLoaded"
        ) {
          setSplineLibrary(
            message.entries
          );
          setLoadingSplineLibrary(
            false
          );
          return;
        }

        if (
          message.type ===
          "mapFullLoadingStarted"
        ) {
          setLoadingFullMap(true);
          setFullMapProgress({
            completed: 0,
            total: message.totalTiles
          });
          return;
        }

        if (
          message.type ===
          "mapFullLoadingProgress"
        ) {
          setLoadingFullMap(true);
          setFullMapProgress({
            completed:
              message.completedTiles,
            total:
              message.totalTiles
          });
          return;
        }

        if (
          message.type ===
          "mapFullLoaded"
        ) {
          setSelectedMap((current) =>
            current?.directoryName ===
            message.directoryName
              ? {
                  ...current,
                  tiles: message.tiles
                }
              : current
          );

          setObjects(
            message.objects
          );

          setSplines(
            message.splines
          );

          setSelectedObject(undefined);
          setSelectedSpline(undefined);
          setLoadingFullMap(false);
          setFullMapProgress(undefined);
          setLoadedFullMapFor(
            message.directoryName
          );
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
          "mapRegionLoaded"
        ) {
          const responseKey =
            `${message.directoryName}:${message.centerX}:${message.centerY}:${message.radius}`;

          setSelectedMap((current) => {
            if (
              current?.directoryName !==
              message.directoryName
            ) {
              return current;
            }

            const loadedByCoordinate =
              new Map(
                message.tiles.map(
                  (tile) => [
                    `${tile.x}:${tile.y}`,
                    tile
                  ]
                )
              );

            return {
              ...current,
              tiles: current.tiles.map(
                (tile) =>
                  loadedByCoordinate.get(
                    `${tile.x}:${tile.y}`
                  ) ?? tile
              )
            };
          });

          setLoadingRegionKey(
            (current) =>
              current === responseKey
                ? undefined
                : current
          );

          setLoadedRegionKey(
            responseKey
          );

          setActiveTile((current) => {
            if (
              current?.x ===
                message.centerX &&
              current?.y ===
                message.centerY
            ) {
              setObjects(
                message.objects
              );
              setSplines(
                message.splines
              );
              setSelectedObject(
                undefined
              );
              setSelectedSpline(
                undefined
              );
            }

            return current;
          });

          return;
        }

        if (
          message.type ===
          "textureAssetLoaded" &&
          message.requestKey.startsWith(
            "terrain-mask|"
          )
        ) {
          setRequestedTerrainMaskKeys(
            (current) => {
              const next = {
                ...current
              };

              delete next[
                message.requestKey
              ];

              return next;
            }
          );

          setTerrainMaskAssetsByKey(
            (current) => {
              const next = {
                ...current,
                [message.requestKey]:
                  message.asset
              };

              const order =
                terrainMaskCacheOrderRef
                  .current
                  .filter(
                    (key) =>
                      key !==
                      message.requestKey
                  );

              order.push(
                message.requestKey
              );

              while (
                order.length >
                maxTerrainMaskCacheEntries
              ) {
                const evicted =
                  order.shift();

                if (evicted) {
                  delete next[evicted];
                }
              }

              terrainMaskCacheOrderRef
                .current =
                  order;

              return next;
            }
          );

          return;
        }

        if (
          message.type ===
          "textureAssetLoaded" &&
          message.requestKey.startsWith(
            "ground|"
          )
        ) {
          setRequestedGroundTextureKeys(
            (current) => {
              if (
                !Object.hasOwn(
                  current,
                  message.requestKey
                )
              ) {
                return current;
              }

              const next = {
                ...current
              };

              delete next[
                message.requestKey
              ];

              return next;
            }
          );

          setGroundTextureAssetsByKey(
            (current) => {
              const next = {
                ...current,
                [message.requestKey]:
                  message.asset
              };

              const order =
                groundTextureCacheOrderRef
                  .current
                  .filter(
                    (key) =>
                      key !==
                      message.requestKey
                  );

              order.push(
                message.requestKey
              );

              while (
                order.length >
                maxGroundTextureCacheEntries
              ) {
                const evicted =
                  order.shift();

                if (evicted) {
                  delete next[evicted];
                }
              }

              groundTextureCacheOrderRef
                .current =
                  order;

              return next;
            }
          );

          return;
        }

        if (
          message.type ===
          "textureAssetLoaded"
        ) {
          setRequestedTextureKeys(
            (current) => {
              if (
                !Object.hasOwn(
                  current,
                  message.requestKey
                )
              ) {
                return current;
              }

              const next = {
                ...current
              };

              delete next[
                message.requestKey
              ];

              return next;
            }
          );

          setTextureAssetsByKey(
            (current) => {
              const next = {
                ...current,
                [message.requestKey]:
                  message.asset
              };

              const order =
                textureCacheOrderRef.current
                  .filter(
                    (key) =>
                      key !==
                      message.requestKey
                  );

              order.push(
                message.requestKey
              );

              while (
                order.length >
                maxTextureCacheEntries
              ) {
                const evicted =
                  order.shift();

                if (evicted) {
                  delete next[evicted];
                }
              }

              textureCacheOrderRef.current =
                order;

              return next;
            }
          );

          return;
        }

        if (
          message.type ===
          "splineProfileLoaded"
        ) {
          setSplineProfilesByPath(
            (current) => ({
              ...current,
              [message.splinePath]:
                message.definition
            })
          );

          setLoadingSplineFor(
            (current) =>
              current ===
              message.splinePath
                ? undefined
                : current
          );

          setPreloadingSplineProfileFor(
            (current) =>
              current ===
              message.splinePath
                ? undefined
                : current
          );

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

          setPreloadingGeometryFor(
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
          "objectInserted"
        ) {
          setInsertingObject(false);
          setPlacementAsset(undefined);
          setPendingPlacement(undefined);
          setEditorTool("select");
          setSaveNotice(
            `Objeto #${message.placedObject.objectId} inserido. Backup: ${message.backupDirectory}`
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);
          setSelectedObject(undefined);
          setSelectedSpline(undefined);
          return;
        }

        if (
          message.type ===
          "splineDeleted"
        ) {
          setDeletingSpline(false);
          setPreviewSplineTransforms({});
          setSelectedSpline(undefined);
          setEditorTool("select");

          setSaveNotice(
            `Spline #${message.splineId} excluída; ${message.unlinkedSplines} vizinha(s) atualizada(s) em ${message.filesSaved} arquivo(s). Backup: ${message.backupDirectory}`
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);

          return;
        }

        if (
          message.type ===
          "splineInserted"
        ) {
          setInsertingSpline(false);
          setSplinePlacementTemplate(
            undefined
          );
          setSplineLibraryPlacementAsset(
            undefined
          );
          setSplineLibraryPlacementIsHeight(
            false
          );
          setPendingSplinePlacement(
            undefined
          );
          setSelectedSpline(undefined);
          setEditorTool("select");

          setSaveNotice(
            `Spline #${message.placedSpline.splineId} inserida desconectada. Backup: ${message.backupDirectory}`
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);

          return;
        }

        if (
          message.type ===
          "splineLinksUpdated"
        ) {
          setSavingSplineLinks(false);
          setSelectedSpline(undefined);
          setEditorTool("select");

          setSaveNotice(
            message.linksUpdated > 0
              ? `Vínculos da spline #${message.splineId} atualizados em ${message.filesSaved} arquivo(s). Backup: ${message.backupDirectory}`
              : `Vínculos da spline #${message.splineId} já estavam atualizados.`
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);

          return;
        }

        if (
          message.type ===
          "splineTransformsSaved"
        ) {
          setSavingSpline(false);
          setPreviewSplineTransforms({});
          setSelectedSpline(undefined);
          setEditorTool("select");

          setSaveNotice(
            `${message.editsSaved} spline(s) salva(s) em ${message.filesSaved} arquivo(s). Backup: ${message.backupDirectory}`
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);

          return;
        }

        if (
          message.type ===
          "objectDeleted"
        ) {
          setDeletingObject(false);
          setPreviewObjectTransforms({});
          setPreviewSplineTransforms({});
          setUndoPreviewStack([]);
          setRedoPreviewStack([]);
          setSelectedObject(undefined);
          setSelectedSpline(undefined);
          setEditorTool("select");

          setSaveNotice(
            `Objeto #${message.objectId} excluído. Backup: ${message.backupDirectory}`
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);

          return;
        }

        if (
          message.type ===
          "objectTransformsSaved"
        ) {
          setSaving(false);
          setPreviewObjectTransforms({});
          setPreviewSplineTransforms({});
          setUndoPreviewStack([]);
          setRedoPreviewStack([]);
          setSelectedObject(undefined);
          setSelectedSpline(undefined);
          setEditorTool("select");

          setSaveNotice(
            `${message.editsSaved} alteração(ões) salva(s) em ${message.filesSaved} arquivo(s). Backup: ${message.backupDirectory}`
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);

          return;
        }

        if (message.type === "hostError") {
          setSelectingRoot(false);
          setSelectingMap(false);
          setLoadingRegionKey(undefined);
          setLoadingFullMap(false);
          setFullMapProgress(undefined);
          setLoadingSplineFor(undefined);
          setPreloadingSplineProfileFor(
            undefined
          );
          setLoadingMetadataFor(undefined);
          setLoadingGeometryFor(undefined);
          setPreloadingGeometryFor(undefined);
          setPreloadingSplineProfileFor(
            undefined
          );
          setLoadingSceneryLibrary(false);
          setLoadingSplineLibrary(false);
          setSaving(false);
          setInsertingObject(false);
          setDeletingObject(false);
          setSavingSpline(false);
          setSavingSplineLinks(false);
          setInsertingSpline(false);
          setDeletingSpline(false);

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
      !selectedMap
    ) {
      return;
    }

    if (mapLoadMode === "full") {
      if (
        loadingFullMap ||
        loadedFullMapFor ===
          selectedMap.directoryName
      ) {
        return;
      }

      setLoadingFullMap(true);
      setFullMapProgress({
        completed: 0,
        total:
          selectedMap.tiles.length
      });

      loadMapFull(
        selectedMap.directoryName
      );

      return;
    }

    if (!activeTile) {
      return;
    }

    const regionKey =
      `${selectedMap.directoryName}:${activeTile.x}:${activeTile.y}:${tileStreamRadius}`;

    if (
      loadingRegionKey ===
        regionKey ||
      loadedRegionKey ===
        regionKey
    ) {
      return;
    }

    setLoadingRegionKey(
      regionKey
    );

    loadMapRegion(
      selectedMap.directoryName,
      activeTile.x,
      activeTile.y,
      tileStreamRadius
    );
  }, [
    activeTile,
    bridgeAvailable,
    loadedFullMapFor,
    loadedRegionKey,
    loadingFullMap,
    loadingRegionKey,
    mapLoadMode,
    selectedMap
  ]);

  useEffect(() => {
    const splinePath =
      selectedSpline?.splinePath;

    if (
      !bridgeAvailable ||
      !splinePath ||
      Object.hasOwn(
        splineProfilesByPath,
        splinePath
      ) ||
      loadingSplineFor ===
        splinePath ||
      preloadingSplineProfileFor ===
        splinePath
    ) {
      return;
    }

    setLoadingSplineFor(
      splinePath
    );

    loadSplineProfile(
      splinePath
    );
  }, [
    bridgeAvailable,
    loadingSplineFor,
    preloadingSplineProfileFor,
    selectedSpline,
    splineProfilesByPath
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
        sceneryObjectPath ||
      preloadingGeometryFor ===
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
    preloadingGeometryFor,
    selectedMap,
    selectedObject
  ]);

  useEffect(() => {
    if (!bridgeAvailable) {
      return;
    }

    const sceneryObjectPath =
      selectedObject
        ?.sceneryObjectPath ??
      placementAsset
        ?.sceneryObjectPath;

    if (!sceneryObjectPath) {
      return;
    }

    const geometry =
      geometryByPath[
        sceneryObjectPath
      ];

    if (!geometry) {
      return;
    }

    const requests:
      Array<{
        key: string;
        meshPath: string;
        textureName: string;
      }> = [];

    const queuedKeys =
      new Set<string>();

    const queueTexture = (
      meshPath: string,
      textureName:
        | string
        | null
        | undefined
    ) => {
      if (!textureName) {
        return;
      }

      const key =
        getSceneryTextureAssetKey(
          sceneryObjectPath,
          meshPath,
          textureName
        );

      if (
        queuedKeys.has(key) ||
        Object.hasOwn(
          textureAssetsByKey,
          key
        ) ||
        Object.hasOwn(
          requestedTextureKeys,
          key
        )
      ) {
        return;
      }

      queuedKeys.add(key);

      requests.push({
        key,
        meshPath,
        textureName
      });
    };

    for (const mesh of
      geometry.meshes) {
      for (const [
        materialIndex,
        material
      ] of mesh.geometry.materials
        .entries()) {
        queueTexture(
          mesh.declaredPath,
          material.textureName
        );

        const materialOverride =
          findSceneryMaterialOverride(
            mesh,
            materialIndex
          );

        queueTexture(
          mesh.declaredPath,
          materialOverride
            ?.bumpMapTextureName
        );

        queueTexture(
          mesh.declaredPath,
          materialOverride
            ?.environmentMapTextureName
        );

        if (nightPreviewEnabled) {
          queueTexture(
            mesh.declaredPath,
            materialOverride
              ?.nightMapTextureName
          );
        }
      }
    }

    if (requests.length === 0) {
      return;
    }

    setRequestedTextureKeys(
      (current) => {
        const next = {
          ...current
        };

        for (const request of
          requests) {
          next[request.key] = true;
        }

        return next;
      }
    );

    for (const request of
      requests) {
      loadSceneryTextureAsset(
        request.key,
        sceneryObjectPath,
        request.meshPath,
        request.textureName
      );
    }
  }, [
    bridgeAvailable,
    geometryByPath,
    nightPreviewEnabled,
    placementAsset,
    requestedTextureKeys,
    selectedObject,
    textureAssetsByKey
  ]);

  useEffect(() => {
    if (!bridgeAvailable) {
      return;
    }

    const placedSpline =
      selectedSpline ??
      splinePlacementTemplate;

    if (!placedSpline) {
      return;
    }

    const definition =
      splineProfilesByPath[
        placedSpline.splinePath
      ];

    if (!definition) {
      return;
    }

    const requests =
      definition.textures
        .filter(
          (textureName) =>
            Boolean(textureName)
        )
        .map((textureName) => ({
          textureName,
          key:
            getSplineTextureAssetKey(
              placedSpline.splinePath,
              textureName
            )
        }))
        .filter(
          (request) =>
            !Object.hasOwn(
              textureAssetsByKey,
              request.key
            ) &&
            !Object.hasOwn(
              requestedTextureKeys,
              request.key
            )
        );

    if (requests.length === 0) {
      return;
    }

    setRequestedTextureKeys(
      (current) => {
        const next = {
          ...current
        };

        for (const request of
          requests) {
          next[request.key] = true;
        }

        return next;
      }
    );

    for (const request of
      requests) {
      loadSplineTextureAsset(
        request.key,
        placedSpline.splinePath,
        request.textureName
      );
    }
  }, [
    bridgeAvailable,
    requestedTextureKeys,
    selectedSpline,
    splinePlacementTemplate,
    splineProfilesByPath,
    textureAssetsByKey
  ]);

  const activeTiles = useMemo(() => {
    if (!selectedMap) {
      return [];
    }

    if (mapLoadMode === "full") {
      return selectedMap.tiles;
    }

    if (!activeTile) {
      return [];
    }

    return selectedMap.tiles.filter(
      (tile) =>
        Math.abs(
          tile.x - activeTile.x
        ) <= tileStreamRadius &&
        Math.abs(
          tile.y - activeTile.y
        ) <= tileStreamRadius
    );
  }, [
    activeTile,
    mapLoadMode,
    selectedMap
  ]);

  const baseGroundTexture =
    selectedMap?.groundTextures[0];

  const baseGroundMainKey =
    selectedMap &&
    baseGroundTexture
      ? getGroundTextureAssetKey(
          selectedMap.directoryName,
          baseGroundTexture
            .mainTexturePath
        )
      : undefined;

  const baseGroundDetailKey =
    selectedMap &&
    baseGroundTexture
      ? getGroundTextureAssetKey(
          selectedMap.directoryName,
          baseGroundTexture
            .detailTexturePath
        )
      : undefined;

  useEffect(() => {
    if (
      !bridgeAvailable ||
      !selectedMap ||
      !baseGroundTexture
    ) {
      return;
    }

    const paths =
      Array.from(
        new Set([
          baseGroundTexture
            .mainTexturePath,
          baseGroundTexture
            .detailTexturePath
        ])
      );

    const requests =
      paths.filter(
        (texturePath) => {
          const key =
            getGroundTextureAssetKey(
              selectedMap.directoryName,
              texturePath
            );

          return (
            !Object.hasOwn(
              groundTextureAssetsByKey,
              key
            ) &&
            !Object.hasOwn(
              requestedGroundTextureKeys,
              key
            )
          );
        }
      );

    if (requests.length === 0) {
      return;
    }

    setRequestedGroundTextureKeys(
      (current) => {
        const next = {
          ...current
        };

        for (const texturePath of
          requests) {
          next[
            getGroundTextureAssetKey(
              selectedMap.directoryName,
              texturePath
            )
          ] = true;
        }

        return next;
      }
    );

    for (const texturePath of
      requests) {
      loadGroundTextureAsset(
        selectedMap.directoryName,
        texturePath
      );
    }
  }, [
    baseGroundTexture,
    bridgeAvailable,
    groundTextureAssetsByKey,
    requestedGroundTextureKeys,
    selectedMap
  ]);

  const terrainOverlayEntries =
    useMemo(() => {
      if (
        !selectedMap ||
        !showTerrainPaint
      ) {
        return [];
      }

      return activeTiles.flatMap(
        (tile) =>
          (
            tile.terrainTextureMasks ??
            []
          ).flatMap((mask) => {
            const groundTexture =
              selectedMap.groundTextures[
                mask.layerIndex
              ];

            if (
              !groundTexture ||
              Object.hasOwn(
                hiddenTerrainLayerIndices,
                mask.layerIndex
              )
            ) {
              return [];
            }

            const textureKey =
              getGroundTextureAssetKey(
                selectedMap.directoryName,
                groundTexture
                  .mainTexturePath
              );

            const maskKey =
              getTerrainTextureMaskAssetKey(
                selectedMap.directoryName,
                tile.relativeMapPath,
                mask.layerIndex
              );

            return [
              {
                tileX: tile.x,
                tileY: tile.y,
                relativeMapPath:
                  tile.relativeMapPath,
                layerIndex:
                  mask.layerIndex,
                texturePath:
                  groundTexture
                    .mainTexturePath,
                textureRepeating:
                  groundTexture
                    .mainTextureRepeating,
                textureKey,
                maskKey
              }
            ];
          })
      );
    }, [
      activeTiles,
      hiddenTerrainLayerIndices,
      selectedMap,
      showTerrainPaint
    ]);

  useEffect(() => {
    if (
      !bridgeAvailable ||
      !selectedMap ||
      terrainOverlayEntries.length === 0
    ) {
      return;
    }

    const texturePaths =
      Array.from(
        new Set(
          terrainOverlayEntries
            .filter(
              (entry) =>
                !Object.hasOwn(
                  groundTextureAssetsByKey,
                  entry.textureKey
                ) &&
                !Object.hasOwn(
                  requestedGroundTextureKeys,
                  entry.textureKey
                )
            )
            .map(
              (entry) =>
                entry.texturePath
            )
        )
      );

    const maskRequests =
      terrainOverlayEntries.filter(
        (entry) =>
          !Object.hasOwn(
            terrainMaskAssetsByKey,
            entry.maskKey
          ) &&
          !Object.hasOwn(
            requestedTerrainMaskKeys,
            entry.maskKey
          )
      );

    if (texturePaths.length > 0) {
      setRequestedGroundTextureKeys(
        (current) => {
          const next = {
            ...current
          };

          for (const texturePath of
            texturePaths) {
            next[
              getGroundTextureAssetKey(
                selectedMap.directoryName,
                texturePath
              )
            ] = true;
          }

          return next;
        }
      );

      for (const texturePath of
        texturePaths) {
        loadGroundTextureAsset(
          selectedMap.directoryName,
          texturePath
        );
      }
    }

    if (maskRequests.length > 0) {
      setRequestedTerrainMaskKeys(
        (current) => {
          const next = {
            ...current
          };

          for (const request of
            maskRequests) {
            next[
              request.maskKey
            ] = true;
          }

          return next;
        }
      );

      for (const request of
        maskRequests) {
        loadTerrainTextureMaskAsset(
          selectedMap.directoryName,
          request.relativeMapPath,
          request.layerIndex
        );
      }
    }
  }, [
    bridgeAvailable,
    groundTextureAssetsByKey,
    requestedGroundTextureKeys,
    requestedTerrainMaskKeys,
    selectedMap,
    terrainMaskAssetsByKey,
    terrainOverlayEntries
  ]);

  const terrainOverlayPreviews =
    useMemo(
      () =>
        terrainOverlayEntries.map(
          (entry) => ({
            tileX: entry.tileX,
            tileY: entry.tileY,
            layerIndex:
              entry.layerIndex,
            textureRepeating:
              entry.textureRepeating,
            textureAsset:
              groundTextureAssetsByKey[
                entry.textureKey
              ],
            maskAsset:
              terrainMaskAssetsByKey[
                entry.maskKey
              ]
          })
        ),
      [
        groundTextureAssetsByKey,
        terrainMaskAssetsByKey,
        terrainOverlayEntries
      ]
    );

  const objectsForViewport = useMemo(
    () =>
      objects.map(
        (placedObject) =>
          previewObjectTransforms[
            getPlacedObjectKey(
              placedObject
            )
          ] ?? placedObject
      ),
    [
      objects,
      previewObjectTransforms
    ]
  );

  const splinesForViewport = useMemo(
    () =>
      splines.map(
        (placedSpline) =>
          previewSplineTransforms[
            getPlacedSplineKey(
              placedSpline
            )
          ] ?? placedSpline
      ),
    [
      previewSplineTransforms,
      splines
    ]
  );

  const nearbyObjectPaths =
    useMemo(() => {
      const byPath =
        new Map<string, number>();

      for (const placedObject of
        objectsForViewport) {
        const distance =
          activeTile
            ? Math.max(
                Math.abs(
                  placedObject.tileX -
                    activeTile.x
                ),
                Math.abs(
                  placedObject.tileY -
                    activeTile.y
                )
              )
            : 0;

        const current =
          byPath.get(
            placedObject
              .sceneryObjectPath
          );

        if (
          current === undefined ||
          distance < current
        ) {
          byPath.set(
            placedObject
              .sceneryObjectPath,
            distance
          );
        }
      }

      return Array.from(
        byPath.entries()
      )
        .sort(
          (left, right) =>
            left[1] - right[1] ||
            left[0].localeCompare(
              right[0]
            )
        )
        .slice(
          0,
          nearbyObjectPathLimit
        )
        .map(([path]) => path);
    }, [
      activeTile,
      objectsForViewport
    ]);

  const loadedNearbyGeometryCount =
    useMemo(
      () =>
        nearbyObjectPaths.filter(
          (path) =>
            Object.hasOwn(
              geometryByPath,
              path
            )
        ).length,
      [
        geometryByPath,
        nearbyObjectPaths
      ]
    );

  useEffect(() => {
    if (
      !bridgeAvailable ||
      mapLoadMode !== "performance" ||
      Boolean(loadingRegionKey) ||
      loadingGeometryFor ||
      preloadingGeometryFor ||
      nearbyObjectPaths.length === 0
    ) {
      return;
    }

    const nextPath =
      nearbyObjectPaths.find(
        (path) =>
          !Object.hasOwn(
            geometryByPath,
            path
          )
      );

    if (!nextPath) {
      return;
    }

    setPreloadingGeometryFor(
      nextPath
    );

    loadSceneryObjectGeometry(
      nextPath
    );
  }, [
    bridgeAvailable,
    geometryByPath,
    loadingGeometryFor,
    loadingRegionKey,
    mapLoadMode,
    nearbyObjectPaths,
    preloadingGeometryFor
  ]);

  const nearbySplinePaths =
    useMemo(() => {
      const byPath =
        new Map<string, number>();

      for (const placedSpline of
        splinesForViewport) {
        const distance =
          activeTile
            ? Math.max(
                Math.abs(
                  placedSpline.tileX -
                    activeTile.x
                ),
                Math.abs(
                  placedSpline.tileY -
                    activeTile.y
                )
              )
            : 0;

        const current =
          byPath.get(
            placedSpline.splinePath
          );

        if (
          current === undefined ||
          distance < current
        ) {
          byPath.set(
            placedSpline.splinePath,
            distance
          );
        }
      }

      return Array.from(
        byPath.entries()
      )
        .sort(
          (left, right) =>
            left[1] - right[1] ||
            left[0].localeCompare(
              right[0]
            )
        )
        .slice(
          0,
          nearbySplinePathLimit
        )
        .map(([path]) => path);
    }, [
      activeTile,
      splinesForViewport
    ]);

  useEffect(() => {
    if (
      !bridgeAvailable ||
      loadingSplineFor ||
      preloadingSplineProfileFor
    ) {
      return;
    }

    const nextPath =
      nearbySplinePaths.find(
        (path) =>
          !Object.hasOwn(
            splineProfilesByPath,
            path
          )
      );

    if (!nextPath) {
      return;
    }

    setPreloadingSplineProfileFor(
      nextPath
    );

    loadSplineProfile(nextPath);
  }, [
    bridgeAvailable,
    loadingSplineFor,
    nearbySplinePaths,
    preloadingSplineProfileFor,
    splineProfilesByPath
  ]);

  useEffect(() => {
    if (!bridgeAvailable) {
      return;
    }

    const autoKeys =
      Object.keys(
        autoPrefetchedTextureKeys
      );

    const usedObject =
      autoKeys.filter(
        (key) =>
          key.startsWith(
            "scenery|"
          )
      ).length;

    const usedSpline =
      autoKeys.filter(
        (key) =>
          key.startsWith(
            "spline|"
          )
      ).length;

    const objectBudget =
      Math.min(
        autoObjectTextureBatch,
        Math.max(
          0,
          autoObjectTextureLimit -
            usedObject
        )
      );

    const splineBudget =
      Math.min(
        autoSplineTextureBatch,
        Math.max(
          0,
          autoSplineTextureLimit -
            usedSpline
        )
      );

    const objectRequests:
      Array<{
        kind: "scenery";
        key: string;
        sceneryObjectPath: string;
        meshPath: string;
        textureName: string;
      }> = [];

    for (const sceneryObjectPath of
      nearbyObjectPaths) {
      if (
        objectRequests.length >=
          objectBudget
      ) {
        break;
      }

      const geometry =
        geometryByPath[
          sceneryObjectPath
        ];

      if (!geometry) {
        continue;
      }

      const queuedKeys =
        new Set(
          objectRequests.map(
            (request) =>
              request.key
          )
        );

      const queueObjectTexture = (
        meshPath: string,
        textureName:
          | string
          | null
          | undefined
      ) => {
        if (
          !textureName ||
          objectRequests.length >=
            objectBudget
        ) {
          return;
        }

        const key =
          getSceneryTextureAssetKey(
            sceneryObjectPath,
            meshPath,
            textureName
          );

        if (
          queuedKeys.has(key) ||
          Object.hasOwn(
            textureAssetsByKey,
            key
          ) ||
          Object.hasOwn(
            requestedTextureKeys,
            key
          )
        ) {
          return;
        }

        queuedKeys.add(key);

        objectRequests.push({
          kind: "scenery",
          key,
          sceneryObjectPath,
          meshPath,
          textureName
        });
      };

      for (const mesh of
        geometry.meshes) {
        for (const [
          materialIndex,
          material
        ] of mesh.geometry.materials
          .entries()) {
          if (
            objectRequests.length >=
              objectBudget
          ) {
            break;
          }

          queueObjectTexture(
            mesh.declaredPath,
            material.textureName
          );

          const materialOverride =
            findSceneryMaterialOverride(
              mesh,
              materialIndex
            );

          queueObjectTexture(
            mesh.declaredPath,
            materialOverride
              ?.bumpMapTextureName
          );

          queueObjectTexture(
            mesh.declaredPath,
            materialOverride
              ?.environmentMapTextureName
          );

          if (nightPreviewEnabled) {
            queueObjectTexture(
              mesh.declaredPath,
              materialOverride
                ?.nightMapTextureName
            );
          }
        }
      }
    }

    const splineRequests:
      Array<{
        kind: "spline";
        key: string;
        splinePath: string;
        textureName: string;
      }> = [];

    for (const splinePath of
      nearbySplinePaths) {
      if (
        splineRequests.length >=
          splineBudget
      ) {
        break;
      }

      const definition =
        splineProfilesByPath[
          splinePath
        ];

      if (!definition) {
        continue;
      }

      for (const textureName of
        definition.textures) {
        if (
          splineRequests.length >=
            splineBudget
        ) {
          break;
        }

        if (!textureName) {
          continue;
        }

        const key =
          getSplineTextureAssetKey(
            splinePath,
            textureName
          );

        if (
          Object.hasOwn(
            textureAssetsByKey,
            key
          ) ||
          Object.hasOwn(
            requestedTextureKeys,
            key
          )
        ) {
          continue;
        }

        splineRequests.push({
          kind: "spline",
          key,
          splinePath,
          textureName
        });
      }
    }

    const requests = [
      ...objectRequests,
      ...splineRequests
    ];

    if (requests.length === 0) {
      return;
    }

    setRequestedTextureKeys(
      (current) => {
        const next = {
          ...current
        };

        for (const request of
          requests) {
          next[request.key] = true;
        }

        return next;
      }
    );

    setAutoPrefetchedTextureKeys(
      (current) => {
        const next = {
          ...current
        };

        for (const request of
          requests) {
          next[request.key] = true;
        }

        return next;
      }
    );

    for (const request of
      requests) {
      if (
        request.kind ===
        "scenery"
      ) {
        loadSceneryTextureAsset(
          request.key,
          request.sceneryObjectPath,
          request.meshPath,
          request.textureName
        );
      } else {
        loadSplineTextureAsset(
          request.key,
          request.splinePath,
          request.textureName
        );
      }
    }
  }, [
    autoPrefetchedTextureKeys,
    bridgeAvailable,
    geometryByPath,
    nearbyObjectPaths,
    nearbySplinePaths,
    nightPreviewEnabled,
    requestedTextureKeys,
    splineProfilesByPath,
    textureAssetsByKey
  ]);

  const normalizedExplorerSearch =
    explorerSearch
      .trim()
      .toLocaleLowerCase("pt-BR");

  const filteredExplorerObjects =
    useMemo(() => {
      if (
        objectsForViewport.length === 0
      ) {
        return [];
      }

      const filtered =
        normalizedExplorerSearch
          ? objectsForViewport.filter(
              (placedObject) => {
                const name =
                  getObjectName(
                    placedObject
                      .sceneryObjectPath
                  ).toLocaleLowerCase(
                    "pt-BR"
                  );

                const path =
                  placedObject
                    .sceneryObjectPath
                    .toLocaleLowerCase(
                      "pt-BR"
                    );

                const id =
                  String(
                    placedObject.objectId
                  );

                const tile =
                  `${placedObject.tileX},${placedObject.tileY}`;

                return (
                  name.includes(
                    normalizedExplorerSearch
                  ) ||
                  path.includes(
                    normalizedExplorerSearch
                  ) ||
                  id.includes(
                    normalizedExplorerSearch
                  ) ||
                  tile.includes(
                    normalizedExplorerSearch
                  )
                );
              }
            )
          : objectsForViewport;

      return filtered.slice(
        0,
        250
      );
    }, [
      normalizedExplorerSearch,
      objectsForViewport
    ]);

  const filteredExplorerSplines =
    useMemo(() => {
      if (
        splinesForViewport.length === 0
      ) {
        return [];
      }

      const filtered =
        normalizedExplorerSearch
          ? splinesForViewport.filter(
              (placedSpline) => {
                const searchable =
                  [
                    getObjectName(
                      placedSpline
                        .splinePath
                    ),
                    placedSpline
                      .splinePath,
                    placedSpline.splineId,
                    `${placedSpline.tileX},${placedSpline.tileY}`,
                    placedSpline.isHeightSpline
                      ? "spline_h altura"
                      : "spline"
                  ]
                    .join(" ")
                    .toLocaleLowerCase(
                      "pt-BR"
                    );

                return searchable.includes(
                  normalizedExplorerSearch
                );
              }
            )
          : splinesForViewport;

      return filtered.slice(
        0,
        250
      );
    }, [
      normalizedExplorerSearch,
      splinesForViewport
    ]);

  const explorerObjectResultCount =
    useMemo(() => {
      if (
        !normalizedExplorerSearch
      ) {
        return objectsForViewport.length;
      }

      return objectsForViewport.filter(
        (placedObject) => {
          const searchable =
            [
              getObjectName(
                placedObject
                  .sceneryObjectPath
              ),
              placedObject
                .sceneryObjectPath,
              placedObject.objectId,
              `${placedObject.tileX},${placedObject.tileY}`
            ]
              .join(" ")
              .toLocaleLowerCase(
                "pt-BR"
              );

          return searchable.includes(
            normalizedExplorerSearch
          );
        }
      ).length;
    }, [
      normalizedExplorerSearch,
      objectsForViewport
    ]);

  const previewEditCount =
    Object.keys(
      previewObjectTransforms
    ).length;

  const splinePreviewEditCount =
    Object.keys(
      previewSplineTransforms
    ).length;

  const mapObjectPaths = useMemo(() => {
    if (
      mapLoadMode !== "full" ||
      objects.length === 0
    ) {
      return [];
    }

    return Array.from(
      new Set(
        objects.map(
          (placedObject) =>
            placedObject.sceneryObjectPath
        )
      )
    );
  }, [
    mapLoadMode,
    objects
  ]);

  const loadedMapGeometryCount =
    useMemo(
      () =>
        mapObjectPaths.filter(
          (path) =>
            Object.hasOwn(
              geometryByPath,
              path
            )
        ).length,
      [
        geometryByPath,
        mapObjectPaths
      ]
    );

  useEffect(() => {
    if (
      !bridgeAvailable ||
      mapLoadMode !== "full" ||
      loadingFullMap ||
      preloadingGeometryFor ||
      mapObjectPaths.length === 0
    ) {
      return;
    }

    const nextPath =
      mapObjectPaths.find(
        (path) =>
          !Object.hasOwn(
            geometryByPath,
            path
          ) &&
          loadingGeometryFor !== path
      );

    if (!nextPath) {
      return;
    }

    setPreloadingGeometryFor(
      nextPath
    );

    loadSceneryObjectGeometry(
      nextPath
    );
  }, [
    bridgeAvailable,
    geometryByPath,
    loadingFullMap,
    loadingGeometryFor,
    mapLoadMode,
    mapObjectPaths,
    preloadingGeometryFor
  ]);

  const normalizedLibrarySearch =
    librarySearch
      .trim()
      .toLocaleLowerCase("pt-BR");

  const filteredSceneryLibrary =
    useMemo(() => {
      const entries =
        normalizedLibrarySearch
          ? sceneryLibrary.filter(
              (entry) =>
                entry.fileName
                  .toLocaleLowerCase(
                    "pt-BR"
                  )
                  .includes(
                    normalizedLibrarySearch
                  ) ||
                entry.sceneryObjectPath
                  .toLocaleLowerCase(
                    "pt-BR"
                  )
                  .includes(
                    normalizedLibrarySearch
                  )
            )
          : sceneryLibrary;

      return entries.slice(
        0,
        300
      );
    }, [
      normalizedLibrarySearch,
      sceneryLibrary
    ]);

  const sceneryLibraryResultCount =
    useMemo(() => {
      if (!normalizedLibrarySearch) {
        return sceneryLibrary.length;
      }

      return sceneryLibrary.filter(
        (entry) =>
          (
            entry.fileName +
            " " +
            entry.sceneryObjectPath
          )
            .toLocaleLowerCase(
              "pt-BR"
            )
            .includes(
              normalizedLibrarySearch
            )
      ).length;
    }, [
      normalizedLibrarySearch,
      sceneryLibrary
    ]);

  const normalizedSplineLibrarySearch =
    splineLibrarySearch
      .trim()
      .toLocaleLowerCase("pt-BR");

  const filteredSplineLibrary =
    useMemo(() => {
      const entries =
        normalizedSplineLibrarySearch
          ? splineLibrary.filter(
              (entry) =>
                (
                  entry.fileName +
                  " " +
                  entry.splinePath
                )
                  .toLocaleLowerCase(
                    "pt-BR"
                  )
                  .includes(
                    normalizedSplineLibrarySearch
                  )
            )
          : splineLibrary;

      return entries.slice(
        0,
        300
      );
    }, [
      normalizedSplineLibrarySearch,
      splineLibrary
    ]);

  const splineLibraryResultCount =
    useMemo(() => {
      if (
        !normalizedSplineLibrarySearch
      ) {
        return splineLibrary.length;
      }

      return splineLibrary.filter(
        (entry) =>
          (
            entry.fileName +
            " " +
            entry.splinePath
          )
            .toLocaleLowerCase(
              "pt-BR"
            )
            .includes(
              normalizedSplineLibrarySearch
            )
      ).length;
    }, [
      normalizedSplineLibrarySearch,
      splineLibrary
    ]);

  const explorerSplineResultCount =
    useMemo(() => {
      if (
        !normalizedExplorerSearch
      ) {
        return splinesForViewport.length;
      }

      return splinesForViewport.filter(
        (placedSpline) => {
          const searchable =
            [
              getObjectName(
                placedSpline.splinePath
              ),
              placedSpline.splinePath,
              placedSpline.splineId,
              `${placedSpline.tileX},${placedSpline.tileY}`,
              placedSpline.isHeightSpline
                ? "spline_h altura"
                : "spline"
            ]
              .join(" ")
              .toLocaleLowerCase(
                "pt-BR"
              );

          return searchable.includes(
            normalizedExplorerSearch
          );
        }
      ).length;
    }, [
      normalizedExplorerSearch,
      splinesForViewport
    ]);

  const explorerResultCount =
    explorerObjectResultCount +
    explorerSplineResultCount;

  const placementHasKnownTemplate =
    useMemo(
      () =>
        placementAsset
          ? objects.some(
              (placedObject) =>
                placedObject
                  .sceneryObjectPath
                  .toLocaleLowerCase(
                    "pt-BR"
                  ) ===
                placementAsset
                  .sceneryObjectPath
                  .toLocaleLowerCase(
                    "pt-BR"
                  )
            )
          : false,
      [
        objects,
        placementAsset
      ]
    );

  const placementCanPersist =
    Boolean(
      placementAsset &&
      pendingPlacement
    ) &&
    (
      mapLoadMode !== "full" ||
      placementHasKnownTemplate
    );

  const selectedStats = useMemo(() => {
    if (
      activeTiles.length === 0 ||
      !activeTiles.every(
        (tile) => tile.detailsLoaded
      )
    ) {
      return undefined;
    }

    return activeTiles.reduce(
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
          (tile.fileExists ? 0 : 1),
        terrainMarkers:
          stats.terrainMarkers +
          (tile.terrainMarkerPresent
            ? 1
            : 0),
        terrainFiles:
          stats.terrainFiles +
          (tile.terrainFileExists
            ? 1
            : 0),
        terrainBytes:
          stats.terrainBytes +
          tile.terrainFileSize,
        terrainDecoded:
          stats.terrainDecoded +
          (tile.terrain ? 1 : 0)
      }),
      {
        objects: 0,
        splines: 0,
        attachments: 0,
        missingTiles: 0,
        terrainMarkers: 0,
        terrainFiles: 0,
        terrainBytes: 0,
        terrainDecoded: 0
      }
    );
  }, [activeTiles]);

  const activeTileDetails =
    useMemo(
      () =>
        selectedMap &&
        activeTile
          ? selectedMap.tiles.find(
              (tile) =>
                tile.x ===
                  activeTile.x &&
                tile.y ===
                  activeTile.y
            )
          : undefined,
      [
        activeTile,
        selectedMap
      ]
    );

  const activeTerrainRange =
    useMemo(() => {
      const terrain =
        activeTileDetails?.terrain;

      if (
        !terrain ||
        terrain.heights.length === 0
      ) {
        return undefined;
      }

      let minimum =
        Number.POSITIVE_INFINITY;
      let maximum =
        Number.NEGATIVE_INFINITY;

      for (const height of
        terrain.heights) {
        minimum =
          Math.min(
            minimum,
            height
          );
        maximum =
          Math.max(
            maximum,
            height
          );
      }

      return {
        minimum,
        maximum
      };
    }, [
      activeTileDetails?.terrain
    ]);

  const baseGroundMainAsset =
    baseGroundMainKey
      ? groundTextureAssetsByKey[
          baseGroundMainKey
        ]
      : undefined;

  const baseGroundDetailAsset =
    baseGroundDetailKey
      ? groundTextureAssetsByKey[
          baseGroundDetailKey
        ]
      : undefined;

  const selectedSplineProfile =
    selectedSpline
      ? splineProfilesByPath[
          selectedSpline.splinePath
        ]
      : undefined;

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
    if (
      !selectedGeometry ||
      !selectedObject
    ) {
      return [];
    }

    return selectedGeometry.meshes.flatMap(
      (mesh) =>
        mesh.geometry.materials.map(
          (material, index) => {
            const textureKey =
              material.textureName
                ? getSceneryTextureAssetKey(
                    selectedObject
                      .sceneryObjectPath,
                    mesh.declaredPath,
                    material.textureName
                  )
                : undefined;

            const materialOverride =
              findSceneryMaterialOverride(
                mesh,
                index
              );

            const bumpTextureKey =
              materialOverride
                ?.bumpMapTextureName
                ? getSceneryTextureAssetKey(
                    selectedObject
                      .sceneryObjectPath,
                    mesh.declaredPath,
                    materialOverride
                      .bumpMapTextureName
                  )
                : undefined;

            const nightTextureKey =
              materialOverride
                ?.nightMapTextureName
                ? getSceneryTextureAssetKey(
                    selectedObject
                      .sceneryObjectPath,
                    mesh.declaredPath,
                    materialOverride
                      .nightMapTextureName
                  )
                : undefined;

            const environmentTextureKey =
              materialOverride
                ?.environmentMapTextureName
                ? getSceneryTextureAssetKey(
                    selectedObject
                      .sceneryObjectPath,
                    mesh.declaredPath,
                    materialOverride
                      .environmentMapTextureName
                  )
                : undefined;

            return {
              mesh: getObjectName(
                mesh.declaredPath
              ),
              meshPath:
                mesh.declaredPath,
              index,
              material,
              materialOverride,
              textureAsset:
                textureKey
                  ? textureAssetsByKey[
                      textureKey
                    ]
                  : undefined,
              textureRequested:
                textureKey
                  ? Boolean(
                      requestedTextureKeys[
                        textureKey
                      ]
                    )
                  : false,
              bumpTextureAsset:
                bumpTextureKey
                  ? textureAssetsByKey[
                      bumpTextureKey
                    ]
                  : undefined,
              bumpTextureRequested:
                bumpTextureKey
                  ? Boolean(
                      requestedTextureKeys[
                        bumpTextureKey
                      ]
                    )
                  : false,
              nightTextureAsset:
                nightTextureKey
                  ? textureAssetsByKey[
                      nightTextureKey
                    ]
                  : undefined,
              nightTextureRequested:
                nightTextureKey
                  ? Boolean(
                      requestedTextureKeys[
                        nightTextureKey
                      ]
                    )
                  : false,
              environmentTextureAsset:
                environmentTextureKey
                  ? textureAssetsByKey[
                      environmentTextureKey
                    ]
                  : undefined,
              environmentTextureRequested:
                environmentTextureKey
                  ? Boolean(
                      requestedTextureKeys[
                        environmentTextureKey
                      ]
                    )
                  : false
            };
          }
        )
    );
  }, [
    requestedTextureKeys,
    selectedGeometry,
    selectedObject,
    textureAssetsByKey
  ]);

  const getTextureState = useCallback(
    (
      textureName: string | null,
      textureAsset:
        | OmsiTextureAsset
        | undefined,
      requested: boolean
    ) => {
      if (!textureName) {
        return {
          label: "Sem textura",
          tone: "neutral"
        } as const;
      }

      if (textureAsset?.exists) {
        const extension =
          textureAsset.extension
            ?.replace(".", "")
            .toUpperCase();

        return {
          label: extension
            ? `Carregada · ${extension}`
            : "Carregada",
          tone: "loaded"
        } as const;
      }

      if (textureAsset) {
        const label =
          textureAsset.errorCode ===
          "textureNotFound"
            ? "Arquivo ausente"
            : textureAsset.errorCode ===
                "textureTooLarge"
              ? "Acima de 16 MiB"
              : textureAsset.errorCode ===
                  "accessDenied"
                ? "Acesso negado"
                : textureAsset.errorCode ===
                    "textureReadError"
                  ? "Falha de leitura"
                  : "Bloqueada";

        return {
          label,
          tone: "error"
        } as const;
      }

      return {
        label: requested
          ? "Carregando..."
          : "Aguardando",
        tone: requested
          ? "loading"
          : "neutral"
      } as const;
    },
    []
  );

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
          setSelectedSpline(undefined);
          setInspectorTab("general");
        }

        setError(undefined);
      },
      []
    );

  const handleSplineSelection =
    useCallback(
      (
        placedSpline:
          | OmsiPlacedSpline
          | undefined
      ) => {
        const selected =
          placedSpline
            ? previewSplineTransforms[
                getPlacedSplineKey(
                  placedSpline
                )
              ] ?? placedSpline
            : undefined;

        setSelectedSpline(
          selected
        );

        if (placedSpline) {
          setSelectedObject(undefined);
          setInspectorTab("general");
        }

        setError(undefined);
      },
      [
        previewSplineTransforms
      ]
    );

  const handlePreviewObjectTransform =
    useCallback(
      (
        placedObject:
          OmsiPlacedObject
      ) => {
        if (
          splinePreviewEditCount > 0
        ) {
          setError(
            "Salve ou descarte a prévia de spline antes de transformar objetos."
          );
          return;
        }

        const key =
          getPlacedObjectKey(
            placedObject
          );

        const currentPreview =
          previewObjectTransforms[
            key
          ];

        const source =
          currentPreview ??
          objects.find(
            (candidate) =>
              getPlacedObjectKey(
                candidate
              ) === key
          );

        if (
          source &&
          sameObjectTransform(
            source,
            placedObject
          )
        ) {
          setSelectedObject(
            placedObject
          );
          return;
        }

        if (source) {
          setUndoPreviewStack(
            (current) => [
              ...current.slice(-99),
              {
                key,
                before: source,
                after: placedObject,
                hadPreviewBefore:
                  Boolean(
                    currentPreview
                  )
              }
            ]
          );

          setRedoPreviewStack(
            []
          );
        }

        setPreviewObjectTransforms(
          (current) => ({
            ...current,
            [key]: placedObject
          })
        );

        setSelectedObject(
          placedObject
        );

        setError(undefined);
      },
      [
        objects,
        previewObjectTransforms,
        splinePreviewEditCount
      ]
    );

  const handleUndoPreview =
    useCallback(() => {
      const entry =
        undoPreviewStack.at(-1);

      if (!entry) {
        return;
      }

      setUndoPreviewStack(
        (current) =>
          current.slice(0, -1)
      );

      setRedoPreviewStack(
        (current) => [
          ...current.slice(-99),
          entry
        ]
      );

      setPreviewObjectTransforms(
        (current) => {
          const next = {
            ...current
          };

          if (
            entry.hadPreviewBefore
          ) {
            next[entry.key] =
              entry.before;
          } else {
            delete next[
              entry.key
            ];
          }

          return next;
        }
      );

      setSelectedObject(
        entry.before
      );

      setError(undefined);
    }, [
      undoPreviewStack
    ]);

  const handleRedoPreview =
    useCallback(() => {
      const entry =
        redoPreviewStack.at(-1);

      if (!entry) {
        return;
      }

      setRedoPreviewStack(
        (current) =>
          current.slice(0, -1)
      );

      setUndoPreviewStack(
        (current) => [
          ...current.slice(-99),
          entry
        ]
      );

      setPreviewObjectTransforms(
        (current) => ({
          ...current,
          [entry.key]:
            entry.after
        })
      );

      setSelectedObject(
        entry.after
      );

      setError(undefined);
    }, [
      redoPreviewStack
    ]);

  const handleObjectNumericTransform =
    useCallback(
      (
        field:
          | "x"
          | "y"
          | "z"
          | "rotation"
          | "pitch"
          | "bank",
        value: number
      ) => {
        if (
          !selectedObject ||
          !Number.isFinite(value)
        ) {
          return;
        }

        handlePreviewObjectTransform({
          ...selectedObject,
          [field]: value
        });
      },
      [
        handlePreviewObjectTransform,
        selectedObject
      ]
    );

  const handlePreviewSplineTransform =
    useCallback(
      (
        placedSpline:
          OmsiPlacedSpline
      ) => {
        if (previewEditCount > 0) {
          setError(
            "Salve ou descarte as prévias de objetos antes de editar uma spline."
          );
          return;
        }

        const key =
          getPlacedSplineKey(
            placedSpline
          );

        const original =
          splines.find(
            (candidate) =>
              getPlacedSplineKey(
                candidate
              ) === key
          );

        setPreviewSplineTransforms(
          (current) => {
            const next = {
              ...current
            };

            if (
              original &&
              sameSplineTransform(
                original,
                placedSpline
              )
            ) {
              delete next[key];
            } else {
              next[key] =
                placedSpline;
            }

            return next;
          }
        );

        setSelectedSpline(
          original &&
          sameSplineTransform(
            original,
            placedSpline
          )
            ? original
            : placedSpline
        );

        setError(undefined);
      },
      [
        previewEditCount,
        splines
      ]
    );

  const handleSplineNumericTransform =
    useCallback(
      (
        field:
          | "x"
          | "y"
          | "z"
          | "rotation"
          | "length"
          | "radius"
          | "gradientStart"
          | "gradientEnd",
        value: number
      ) => {
        if (
          !selectedSpline ||
          !Number.isFinite(value)
        ) {
          return;
        }

        handlePreviewSplineTransform({
          ...selectedSpline,
          [field]: value
        });
      },
      [
        handlePreviewSplineTransform,
        selectedSpline
      ]
    );

  const handleSaveSplineLinks =
    useCallback(() => {
      if (
        !selectedMap ||
        !selectedSpline ||
        savingSplineLinks
      ) {
        return;
      }

      if (
        !Number.isInteger(
          splineLinkPreviousId
        ) ||
        !Number.isInteger(
          splineLinkNextId
        ) ||
        splineLinkPreviousId ==
          selectedSpline.splineId ||
        splineLinkNextId ==
          selectedSpline.splineId ||
        (
          splineLinkPreviousId != -1 &&
          splineLinkPreviousId ==
            splineLinkNextId
        )
      ) {
        setError(
          errorMessages
            .splineLinkInvalid
        );
        return;
      }

      if (
        previewEditCount > 0 ||
        splinePreviewEditCount > 0 ||
        placementAsset ||
        splinePlacementTemplate
      ) {
        setError(
          "Salve, descarte ou cancele as edições pendentes antes de alterar vínculos."
        );
        return;
      }

      setSavingSplineLinks(true);
      setSaveNotice(undefined);
      setError(undefined);

      updateSplineLinks(
        selectedMap.directoryName,
        selectedSpline,
        splineLinkPreviousId,
        splineLinkNextId
      );
    }, [
      placementAsset,
      previewEditCount,
      savingSplineLinks,
      selectedMap,
      selectedSpline,
      splineLinkNextId,
      splineLinkPreviousId,
      splinePlacementTemplate,
      splinePreviewEditCount
    ]);

  const handleDeleteSelectedSpline =
    useCallback(() => {
      if (
        !selectedMap ||
        !selectedSpline ||
        deletingSpline
      ) {
        return;
      }

      if (
        previewEditCount > 0 ||
        splinePreviewEditCount > 0 ||
        placementAsset ||
        splinePlacementTemplate
      ) {
        setError(
          "Salve, descarte ou cancele as edições pendentes antes de excluir a spline."
        );
        return;
      }

      const confirmed =
        window.confirm(
          `Excluir permanentemente a spline #${selectedSpline.splineId}?\n\nOs vínculos recíprocos dos vizinhos serão liberados na mesma transação e todos os tiles alterados receberão backup.`
        );

      if (!confirmed) {
        return;
      }

      setDeletingSpline(true);
      setSaveNotice(undefined);
      setError(undefined);

      deleteSpline(
        selectedMap.directoryName,
        selectedSpline
      );
    }, [
      deletingSpline,
      placementAsset,
      previewEditCount,
      selectedMap,
      selectedSpline,
      splinePlacementTemplate,
      splinePreviewEditCount
    ]);

  const handleStartSplineCopy =
    useCallback(() => {
      if (
        !selectedMap ||
        !selectedSpline
      ) {
        return;
      }

      if (
        previewEditCount > 0 ||
        splinePreviewEditCount > 0
      ) {
        setError(
          "Salve ou descarte todas as prévias antes de criar uma cópia da spline."
        );
        return;
      }

      if (placementAsset) {
        setError(
          "Cancele a colocação de objeto atual antes de criar uma cópia da spline."
        );
        return;
      }

      if (
        selectedMap
          .usesWorldCoordinates
      ) {
        setError(
          errorMessages
            .splineInsertionWorldCoordinatesUnsupported
        );
        return;
      }

      setSplineLibraryPlacementAsset(
        undefined
      );
      setSplineLibraryPlacementIsHeight(
        false
      );

      setSplinePlacementTemplate(
        selectedSpline
      );
      setPendingSplinePlacement(
        undefined
      );
      setSelectedSpline(undefined);
      setSelectedObject(undefined);
      setEditorTool("select");
      setShowSplines(true);
      setSaveNotice(undefined);
      setError(undefined);
    }, [
      placementAsset,
      previewEditCount,
      selectedMap,
      selectedSpline,
      splinePreviewEditCount
    ]);

  const handleSelectSplineLibraryAsset =
    useCallback(
      (
        entry:
          SplineLibraryEntry,
        isHeightSpline: boolean
      ) => {
        if (!selectedMap) {
          return;
        }

        if (
          previewEditCount > 0 ||
          splinePreviewEditCount > 0
        ) {
          setError(
            "Salve ou descarte todas as prévias antes de colocar uma spline da biblioteca."
          );
          return;
        }

        if (placementAsset) {
          setError(
            "Cancele a colocação de objeto atual antes de colocar uma spline."
          );
          return;
        }

        if (
          selectedMap
            .usesWorldCoordinates
        ) {
          setError(
            errorMessages
              .splineInsertionWorldCoordinatesUnsupported
          );
          return;
        }

        setSplineLibraryPlacementAsset(
          entry
        );

        setSplineLibraryPlacementIsHeight(
          isHeightSpline
        );

        setSplinePlacementTemplate({
          tileX: 0,
          tileY: 0,
          headerValue: "",
          splinePath:
            entry.splinePath,
          splineId: -1,
          sourceSectionOrdinal: -1,
          previousSplineId: -1,
          nextSplineId: -1,
          x: 0,
          y: 0,
          z: 0,
          rotation: 0,
          length: 20,
          radius: 0,
          gradientStart: 0,
          gradientEnd: 0,
          isHeightSpline
        });

        setPendingSplinePlacement(
          undefined
        );
        setSelectedSpline(undefined);
        setSelectedObject(undefined);
        setEditorTool("select");
        setShowSplines(true);
        setSaveNotice(undefined);
        setError(undefined);

        if (
          !Object.hasOwn(
            splineProfilesByPath,
            entry.splinePath
          )
        ) {
          setLoadingSplineFor(
            entry.splinePath
          );

          loadSplineProfile(
            entry.splinePath
          );
        }
      },
      [
        placementAsset,
        previewEditCount,
        selectedMap,
        splinePreviewEditCount,
        splineProfilesByPath
      ]
    );

  const handleSplinePlacementPoint =
    useCallback(
      (
        point: Pick<
          PendingSplinePlacement,
          | "targetTileX"
          | "targetTileY"
          | "x"
          | "y"
        >
      ) => {
        if (!splinePlacementTemplate) {
          return;
        }

        setPendingSplinePlacement({
          ...point,
          z:
            splinePlacementTemplate.z,
          rotation:
            splinePlacementTemplate
              .rotation,
          length:
            splinePlacementTemplate
              .length,
          radius:
            splinePlacementTemplate
              .radius,
          gradientStart:
            splinePlacementTemplate
              .gradientStart,
          gradientEnd:
            splinePlacementTemplate
              .gradientEnd
        });
      },
      [splinePlacementTemplate]
    );

  const handleCancelSplinePlacement =
    useCallback(() => {
      setSplinePlacementTemplate(
        undefined
      );
      setSplineLibraryPlacementAsset(
        undefined
      );
      setSplineLibraryPlacementIsHeight(
        false
      );
      setPendingSplinePlacement(
        undefined
      );
      setInsertingSpline(false);
    }, []);

  const handleConfirmSplinePlacement =
    useCallback(() => {
      if (
        !selectedMap ||
        !splinePlacementTemplate ||
        !pendingSplinePlacement ||
        insertingSpline
      ) {
        return;
      }

      setInsertingSpline(true);
      setSaveNotice(undefined);
      setError(undefined);

      if (
        splineLibraryPlacementAsset
      ) {
        insertSplineFromLibrary(
          selectedMap.directoryName,
          splineLibraryPlacementAsset
            .splinePath,
          splineLibraryPlacementIsHeight,
          pendingSplinePlacement
        );
      } else {
        insertSpline(
          selectedMap.directoryName,
          splinePlacementTemplate,
          pendingSplinePlacement
        );
      }
    }, [
      insertingSpline,
      pendingSplinePlacement,
      selectedMap,
      splineLibraryPlacementAsset,
      splineLibraryPlacementIsHeight,
      splinePlacementTemplate
    ]);

  const handleDiscardSplinePreview =
    useCallback(() => {
      if (selectedSpline) {
        const key =
          getPlacedSplineKey(
            selectedSpline
          );

        const original =
          splines.find(
            (candidate) =>
              getPlacedSplineKey(
                candidate
              ) === key
          );

        setSelectedSpline(
          original
        );
      }

      setPreviewSplineTransforms({});
      setError(undefined);
    }, [
      selectedSpline,
      splines
    ]);

  const handleSaveSplinePreview =
    useCallback(() => {
      if (
        !selectedMap ||
        savingSpline
      ) {
        return;
      }

      const edits =
        Object.values(
          previewSplineTransforms
        );

      if (edits.length === 0) {
        return;
      }

      setSavingSpline(true);
      setSaveNotice(undefined);
      setError(undefined);

      saveSplineTransforms(
        selectedMap.directoryName,
        edits
      );
    }, [
      previewSplineTransforms,
      savingSpline,
      selectedMap
    ]);

  const handleDiscardPreviewEdits =
    useCallback(() => {
      if (selectedObject) {
        const selectedKey =
          getPlacedObjectKey(
            selectedObject
          );

        const original =
          objects.find(
            (placedObject) =>
              getPlacedObjectKey(
                placedObject
              ) === selectedKey
          );

        setSelectedObject(
          original
        );
      }

      setPreviewObjectTransforms(
        {}
      );

      setUndoPreviewStack([]);
      setRedoPreviewStack([]);
      setEditorTool("select");
    }, [
      objects,
      selectedObject
    ]);

  const handleSavePreviewEdits =
    useCallback(() => {
      if (
        !selectedMap ||
        saving
      ) {
        return;
      }

      const edits =
        Object.values(
          previewObjectTransforms
        );

      if (edits.length === 0) {
        return;
      }

      setSaving(true);
      setSaveNotice(undefined);
      setError(undefined);

      saveObjectTransforms(
        selectedMap.directoryName,
        edits
      );
    }, [
      previewObjectTransforms,
      saving,
      selectedMap
    ]);

  const requestCameraAction =
    useCallback(
      (
        type:
          | "fit"
          | "focus"
          | "perspective"
          | "top"
      ) => {
        setCameraAction(
          (current) => ({
            type,
            token:
              (current?.token ?? 0) + 1
          })
        );
      },
      []
    );

  useEffect(() => {
    const handleKeyDown = (
      event: KeyboardEvent
    ) => {
      const target =
        event.target as
          | HTMLElement
          | null;

      if (event.key === "F11") {
        event.preventDefault();
        setFullScreen(
          !isFullScreen
        );
        return;
      }

      if (
        event.key === "Escape" &&
        isFullScreen
      ) {
        event.preventDefault();
        setFullScreen(false);
        return;
      }

      if (
        target?.isContentEditable ||
        target?.tagName === "INPUT" ||
        target?.tagName === "TEXTAREA" ||
        target?.tagName === "SELECT"
      ) {
        return;
      }

      const key =
        event.key.toLowerCase();

      if (
        (event.ctrlKey ||
          event.metaKey) &&
        key === "z"
      ) {
        event.preventDefault();

        if (event.shiftKey) {
          handleRedoPreview();
        } else {
          handleUndoPreview();
        }

        return;
      }

      if (
        (event.ctrlKey ||
          event.metaKey) &&
        key === "y"
      ) {
        event.preventDefault();
        handleRedoPreview();
        return;
      }

      if (
        (event.ctrlKey ||
          event.metaKey) &&
        key === "s"
      ) {
        event.preventDefault();

        if (
          previewEditCount > 0 &&
          !saving
        ) {
          handleSavePreviewEdits();
        } else if (
          splinePreviewEditCount > 0 &&
          !savingSpline
        ) {
          handleSaveSplinePreview();
        }

        return;
      }

      if (key === "1") {
        setCameraMode(
          "perspective"
        );
        requestCameraAction(
          "perspective"
        );
        return;
      }

      if (key === "2") {
        setCameraMode("top");
        requestCameraAction("top");
        return;
      }

      if (key === "n") {
        setSnapEnabled(
          (current) => !current
        );
        return;
      }

      if (key === "q") {
        setEditorTool("select");
        return;
      }

      if (
        key === "w" &&
        (selectedObject ||
          selectedSpline)
      ) {
        setEditorTool("move");
        return;
      }

      if (
        key === "e" &&
        (selectedObject ||
          selectedSpline)
      ) {
        setEditorTool("rotate");
        return;
      }

      if (
        key === "f" &&
        (selectedObject ||
          selectedSpline)
      ) {
        event.preventDefault();
        requestCameraAction(
          "focus"
        );
        return;
      }

      if (event.key === "Home") {
        event.preventDefault();
        requestCameraAction(
          "fit"
        );
        return;
      }

      if (key === "g") {
        setShowGrid(
          (current) => !current
        );
        return;
      }

      if (key === "o") {
        setShowObjects(
          (current) => !current
        );
        return;
      }

      if (key === "l") {
        setShowSplines(
          (current) => !current
        );
        return;
      }

      if (event.key === "Escape") {
        setEditorTool("select");
      }
    };

    window.addEventListener(
      "keydown",
      handleKeyDown
    );

    return () =>
      window.removeEventListener(
        "keydown",
        handleKeyDown
      );
  }, [
    handleRedoPreview,
    handleSavePreviewEdits,
    handleSaveSplinePreview,
    handleUndoPreview,
    isFullScreen,
    previewEditCount,
    requestCameraAction,
    saving,
    savingSpline,
    selectedObject,
    selectedSpline,
    splinePreviewEditCount
  ]);

  const handleSelectPlacementAsset =
    useCallback(
      (
        entry:
          SceneryLibraryEntry,
        transformDefaults:
          PlacementTransformDefaults =
            defaultPlacementTransform
      ) => {
        if (
          splinePreviewEditCount > 0
        ) {
          setError(
            "Salve ou descarte a prévia de spline antes de iniciar uma colocação."
          );
          return;
        }

        if (splinePlacementTemplate) {
          setError(
            "Cancele a colocação de spline atual antes de colocar um objeto."
          );
          return;
        }

        if (
          selectedMap
            ?.usesWorldCoordinates
        ) {
          setError(
            errorMessages
              .objectInsertionWorldCoordinatesUnsupported
          );
          return;
        }

        setPlacementTransformDefaults(
          transformDefaults
        );
        setPlacementAsset(entry);
        setPendingPlacement(undefined);
        setSelectedObject(undefined);
        setSelectedSpline(undefined);
        setEditorTool("select");
        setShowObjects(true);
        setError(undefined);

        if (
          !Object.hasOwn(
            geometryByPath,
            entry.sceneryObjectPath
          )
        ) {
          loadSceneryObjectGeometry(
            entry.sceneryObjectPath
          );
        }
      },
      [
        geometryByPath,
        selectedMap,
        splinePlacementTemplate,
        splinePreviewEditCount
      ]
    );

  const handlePlaceSelectedObjectCopy =
    useCallback(() => {
      if (!selectedObject) {
        return;
      }

      handleSelectPlacementAsset(
        {
          sceneryObjectPath:
            selectedObject
              .sceneryObjectPath,
          fileName: getObjectName(
            selectedObject
              .sceneryObjectPath
          )
        },
        {
          z: selectedObject.z,
          rotation:
            selectedObject.rotation,
          pitch: selectedObject.pitch,
          bank: selectedObject.bank
        }
      );
    }, [
      handleSelectPlacementAsset,
      selectedObject
    ]);

  const handlePlacementPoint =
    useCallback(
      (
        placement:
          PendingObjectPlacement
      ) => {
        setPendingPlacement({
          ...placement,
          ...placementTransformDefaults
        });
      },
      [
        placementTransformDefaults
      ]
    );

  const handleCancelPlacement =
    useCallback(() => {
      setPlacementAsset(undefined);
      setPendingPlacement(undefined);
      setPlacementTransformDefaults(
        defaultPlacementTransform
      );
      setInsertingObject(false);
    }, []);

  const handleConfirmPlacement =
    useCallback(() => {
      if (
        !selectedMap ||
        !placementAsset ||
        !pendingPlacement ||
        !placementCanPersist ||
        insertingObject
      ) {
        return;
      }

      setInsertingObject(true);
      setSaveNotice(undefined);
      setError(undefined);

      insertObject(
        selectedMap.directoryName,
        placementAsset
          .sceneryObjectPath,
        pendingPlacement
      );
    }, [
      insertingObject,
      pendingPlacement,
      placementAsset,
      placementCanPersist,
      selectedMap
    ]);

  const handleDeleteSelectedObject =
    useCallback(() => {
      if (
        !selectedMap ||
        !selectedObject ||
        deletingObject
      ) {
        return;
      }

      if (
        previewEditCount > 0 ||
        splinePreviewEditCount > 0
      ) {
        setError(
          "Salve ou descarte todas as prévias de transformação antes de excluir um objeto."
        );
        return;
      }

      if (placementAsset) {
        setError(
          "Cancele a colocação atual antes de excluir um objeto."
        );
        return;
      }

      const confirmed =
        window.confirm(
          `Excluir permanentemente o objeto #${selectedObject.objectId} deste mapa?\n\nUm backup do tile será criado antes da alteração.`
        );

      if (!confirmed) {
        return;
      }

      setDeletingObject(true);
      setSaveNotice(undefined);
      setError(undefined);

      deleteObject(
        selectedMap.directoryName,
        selectedObject
      );
    }, [
      deletingObject,
      placementAsset,
      previewEditCount,
      selectedMap,
      splinePreviewEditCount,
      selectedObject
    ]);

  const handleExplorerPanelTab =
    useCallback(
      (
        tab:
          | "map"
          | "library"
          | "splineLibrary"
      ) => {
        setExplorerPanelTab(tab);

        if (
          tab === "library" &&
          sceneryLibrary.length === 0 &&
          !loadingSceneryLibrary
        ) {
          if (!bridgeAvailable) {
            setError(
              "Abra a Biblioteca pelo aplicativo desktop OMSI Map Studio."
            );
            return;
          }

          setLoadingSceneryLibrary(
            true
          );

          setError(undefined);
          loadSceneryLibrary();
        }

        if (
          tab === "splineLibrary" &&
          splineLibrary.length === 0 &&
          !loadingSplineLibrary
        ) {
          if (!bridgeAvailable) {
            setError(
              "Abra a Biblioteca de Splines pelo aplicativo desktop OMSI Map Studio."
            );
            return;
          }

          setLoadingSplineLibrary(
            true
          );

          setError(undefined);
          loadSplineLibrary();
        }
      },
      [
        bridgeAvailable,
        loadingSceneryLibrary,
        loadingSplineLibrary,
        sceneryLibrary.length,
        splineLibrary.length
      ]
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
    saving ||
    savingSpline ||
    savingSplineLinks ||
    insertingObject ||
    deletingObject ||
    insertingSpline ||
    deletingSpline ||
    loadingFullMap ||
    Boolean(loadingRegionKey);

  const renderNav = () => (
    <aside className="studio-sidebar">
      <div className="sidebar-brand">
        <img
          className="brand-logo"
          src="/mapstudio-icon.ico"
          alt=""
          aria-hidden="true"
        />
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
        <span>Edição preservativa</span>
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
          o mapa e edite dados reais com validação,
          backup e gravação preservativa.
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
          <strong>
            Edição preservativa (alpha)
          </strong>
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
          <dt>Tiles totais</dt>
          <dd>
            {selectedMap?.tiles.length ?? 0}
          </dd>
        </div>
        <div>
          <dt>
            {mapLoadMode === "full"
              ? "Objetos"
              : "Objetos (área ativa)"}
          </dt>
          <dd>
            {selectedStats?.objects ??
              (loadingFullMap ||
              Boolean(loadingRegionKey)
                ? "Carregando..."
                : objects.length)}
          </dd>
        </div>
        <div>
          <dt>
            {mapLoadMode === "full"
              ? "Splines"
              : "Splines (área ativa)"}
          </dt>
          <dd>
            {selectedStats?.splines ??
              (loadingFullMap ||
              Boolean(loadingRegionKey)
                ? "Carregando..."
                : splines.length)}
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
          <dt>
            {mapLoadMode === "full"
              ? "Tiles carregados"
              : "Tiles ativos"}
          </dt>
          <dd>
            {activeTiles.length}
          </dd>
        </div>
        <div>
          <dt>Tile ativo</dt>
          <dd>
            {activeTile
              ? `${activeTile.x}, ${activeTile.y}`
              : "—"}
          </dd>
        </div>
        <div>
          <dt>[terrain]</dt>
          <dd>
            {!activeTileDetails
              ?.detailsLoaded
              ? "Não carregado"
              : activeTileDetails
                  .terrainMarkerPresent
                ? "Presente"
                : "Ausente"}
          </dd>
        </div>
        <div>
          <dt>Sidecar terrain</dt>
          <dd>
            {!activeTileDetails
              ?.detailsLoaded
              ? "Não carregado"
              : activeTileDetails
                  .terrainFileExists
                ? `Encontrado · ${formatFileSize(
                    activeTileDetails
                      .terrainFileSize
                  )}`
                : "Ausente"}
          </dd>
        </div>
        <div>
          <dt>Malha terrain</dt>
          <dd>
            {!activeTileDetails
              ?.detailsLoaded
              ? "Não carregado"
              : activeTileDetails
                  .terrain
                ? `${activeTileDetails.terrain.cellCount}×${activeTileDetails.terrain.cellCount} células · ${activeTileDetails.terrain.heights.length} alturas`
                : activeTileDetails
                    .terrainFileExists
                  ? "Formato não decodificado"
                  : "Ausente"}
          </dd>
        </div>
        <div>
          <dt>Altitude terrain</dt>
          <dd>
            {activeTerrainRange
              ? `${formatNumber(activeTerrainRange.minimum)} a ${formatNumber(activeTerrainRange.maximum)} m`
              : "—"}
          </dd>
        </div>
        <div>
          <dt>Render data .rdy</dt>
          <dd>
            {!activeTileDetails
              ?.detailsLoaded
              ? "Não carregado"
              : !activeTileDetails
                  .terrainRenderData
                ? "Ausente"
                : activeTileDetails
                    .terrainRenderData
                    .isValid
                  ? `${activeTileDetails.terrainRenderData.vertexCount} vértices · ${activeTileDetails.terrainRenderData.triangleCount} triângulos · ${activeTileDetails.terrainRenderData.materialCount} material(is) · ${formatFileSize(
                      activeTileDetails.terrainRenderData.fileSize
                    )}`
                  : `Inválido · ${activeTileDetails.terrainRenderData.errorCode ?? "erro desconhecido"} · ${formatFileSize(
                      activeTileDetails.terrainRenderData.fileSize
                    )}`}
          </dd>
        </div>
        <div>
          <dt>Transform .rdy</dt>
          <dd>
            {activeTileDetails
              ?.terrainRenderData
              ?.isValid
              ? activeTileDetails
                  .terrainRenderData
                  .hasTransform
                ? "Presente"
                : "Ausente"
              : "—"}
          </dd>
        </div>
        <div>
          <dt>Máscaras de terreno</dt>
          <dd>
            {!activeTileDetails
              ?.detailsLoaded
              ? "Não carregado"
              : (
                    activeTileDetails
                      .terrainTextureMasks
                      ?.length ?? 0
                  ) > 0
                ? `Camadas ${activeTileDetails.terrainTextureMasks!
                    .map(
                      (mask) =>
                        mask.layerIndex
                    )
                    .join(", ")} · ${formatFileSize(
                    activeTileDetails.terrainTextureMasks!
                      .reduce(
                        (total, mask) =>
                          total +
                          mask.fileSize,
                        0
                      )
                  )}`
                : "Nenhuma"}
          </dd>
        </div>
        <div>
          <dt>Camadas [groundtex]</dt>
          <dd>
            {selectedMap
              ?.groundTextures.length ??
              0}
          </dd>
        </div>
        <div className="terrain-layer-control-row">
          <dt>Visibilidade</dt>
          <dd>
            <div className="terrain-layer-list">
              {selectedMap?.groundTextures.map(
                (layer, index) => {
                  const activeMask =
                    activeTileDetails
                      ?.terrainTextureMasks
                      ?.find(
                        (mask) =>
                          mask.layerIndex ===
                          index
                      );

                  const maskKey =
                    selectedMap &&
                    activeTileDetails &&
                    index > 0
                      ? getTerrainTextureMaskAssetKey(
                          selectedMap.directoryName,
                          activeTileDetails.relativeMapPath,
                          index
                        )
                      : undefined;

                  const maskAsset =
                    maskKey
                      ? terrainMaskAssetsByKey[
                          maskKey
                        ]
                      : undefined;

                  return (
                    <label
                      key={index}
                      className="terrain-layer-item"
                    >
                      <input
                        type="checkbox"
                        checked={
                          !Object.hasOwn(
                            hiddenTerrainLayerIndices,
                            index
                          )
                        }
                        onChange={(event) =>
                          setHiddenTerrainLayerIndices(
                            (current) => {
                              const next = {
                                ...current
                              };

                              if (
                                event.target
                                  .checked
                              ) {
                                delete next[
                                  index
                                ];
                              } else {
                                next[
                                  index
                                ] = true;
                              }

                              return next;
                            }
                          )
                        }
                      />
                      <span>
                        <strong>
                          {index}:{" "}
                          {getObjectName(
                            layer.mainTexturePath
                          )}
                        </strong>
                        <small>
                          {index === 0
                            ? "base"
                            : activeMask
                              ? maskAsset?.exists
                                ? maskAsset.alphaOnly === true
                                  ? `${maskAsset.width ?? "?"}×${maskAsset.height ?? "?"} · ${maskAsset.pixelFormat ?? "DDS"} · validada`
                                  : `${maskAsset.width ?? "?"}×${maskAsset.height ?? "?"} · ${maskAsset.pixelFormat ?? "DDS"} · não renderizada`
                                : "máscara presente"
                              : "sem máscara no tile"}
                        </small>
                      </span>
                    </label>
                  );
                }
              )}
            </div>
          </dd>
        </div>
        <div>
          <dt>Textura base</dt>
          <dd>
            {baseGroundTexture
              ? `${baseGroundTexture.mainTexturePath} · ${getTextureState(
                  baseGroundTexture.mainTexturePath,
                  baseGroundMainAsset,
                  Boolean(
                    baseGroundMainKey &&
                      requestedGroundTextureKeys[
                        baseGroundMainKey
                      ]
                  )
                ).label} · rep. ${formatNumber(
                  baseGroundTexture.mainTextureRepeating
                )}`
              : "Nenhuma camada declarada"}
          </dd>
        </div>
        <div>
          <dt>Detalhe base</dt>
          <dd>
            {baseGroundTexture
              ? `${baseGroundTexture.detailTexturePath} · ${getTextureState(
                  baseGroundTexture.detailTexturePath,
                  baseGroundDetailAsset,
                  Boolean(
                    baseGroundDetailKey &&
                      requestedGroundTextureKeys[
                        baseGroundDetailKey
                      ]
                  )
                ).label} · rep. ${formatNumber(
                  baseGroundTexture.detailTextureRepeating
                )}`
              : "—"}
          </dd>
        </div>
        <div>
          <dt>Terrenos (área)</dt>
          <dd>
            {selectedStats
              ? `${selectedStats.terrainDecoded}/${selectedStats.terrainFiles} malhas/sidecars · ${selectedStats.terrainMarkers} marcadores · ${formatFileSize(
                  selectedStats.terrainBytes
                )}`
              : "Carregando..."}
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
          <>
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

            <button
              type="button"
              className="wide"
              onClick={
                handlePlaceSelectedObjectCopy
              }
              disabled={
                busy ||
                selectedMap
                  ?.usesWorldCoordinates
              }
              title="Criar uma nova colocação usando o mesmo .sco e a transformação atual como base"
            >
              Colocar cópia
            </button>

            <div className="transform-help">
              A cópia usa o mesmo .sco real.
              O próximo clique define X/Y e
              preserva inicialmente Z, rotação,
              pitch e bank da seleção atual.
            </div>

            <button
              type="button"
              className="danger-action wide"
              onClick={
                handleDeleteSelectedObject
              }
              disabled={
                busy ||
                previewEditCount > 0 ||
                splinePreviewEditCount > 0 ||
                Boolean(placementAsset)
              }
              title={
                previewEditCount > 0 ||
                splinePreviewEditCount > 0
                  ? "Salve ou descarte as prévias antes de excluir"
                  : placementAsset
                    ? "Cancele a colocação atual antes de excluir"
                    : "Excluir o objeto do tile com backup automático"
              }
            >
              {deletingObject
                ? "Excluindo..."
                : "Excluir objeto"}
            </button>
          </>
        )}

        {inspectorTab === "transform" && (
          <div className="transform-inspector">
            <dl className="property-list dense">
              <div>
                <dt>Tile</dt>
                <dd>
                  {selectedObject.tileX},{" "}
                  {selectedObject.tileY}
                </dd>
              </div>
              {selectedObjectGlobal && (
                <div>
                  <dt>
                    Global X / Y / Z
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
            </dl>

            <div className="transform-fields">
              {(
                [
                  ["x", "X", selectedObject.x],
                  ["y", "Y", selectedObject.y],
                  ["z", "Z", selectedObject.z],
                  [
                    "rotation",
                    "Rotação",
                    selectedObject.rotation
                  ],
                  [
                    "pitch",
                    "Pitch",
                    selectedObject.pitch
                  ],
                  [
                    "bank",
                    "Bank",
                    selectedObject.bank
                  ]
                ] as const
              ).map(
                ([field, label, value]) => (
                  <label
                    key={field}
                    className="transform-field"
                  >
                    <span>{label}</span>
                    <input
                      key={`${getPlacedObjectKey(
                        selectedObject
                      )}:${field}:${value}`}
                      type="number"
                      step="0.001"
                      defaultValue={value}
                      onKeyDown={(event) => {
                        if (
                          event.key ===
                          "Enter"
                        ) {
                          event.currentTarget.blur();
                        }
                      }}
                      onBlur={(event) => {
                        const next =
                          event.currentTarget
                            .valueAsNumber;

                        if (
                          Number.isFinite(
                            next
                          ) &&
                          next !== value
                        ) {
                          handleObjectNumericTransform(
                            field,
                            next
                          );
                        }
                      }}
                    />
                  </label>
                )
              )}
            </div>

            <div className="transform-help">
              Alterações numéricas entram na
              prévia ao pressionar Enter ou sair
              do campo.
            </div>
          </div>
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
                (mesh, meshIndex) => {
                  const lodThreshold =
                    selectedGeometry
                      ?.meshes[
                        meshIndex
                      ]?.lodThreshold;

                  return (
                    <div
                      className="mesh-row"
                      key={`${mesh.declaredPath}-${meshIndex}`}
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
                        {" · "}
                        {lodThreshold == null
                          ? "Global"
                          : `LOD ${formatNumber(
                              lodThreshold
                            )}`}
                      </span>
                    </div>
                  );
                }
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
              materialRows.map((row) => {
                const textureState =
                  getTextureState(
                    row.material.textureName,
                    row.textureAsset,
                    row.textureRequested
                  );

                return (
                  <div
                    className="material-row"
                    key={`${row.meshPath}-${row.index}`}
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
                      <small
                        title={
                          row.material
                            .textureName ??
                          undefined
                        }
                      >
                        {row.material.textureName ??
                          "Sem textura declarada"}
                      </small>
                      <span
                        className={`texture-state ${textureState.tone}`}
                      >
                        {textureState.label}
                      </span>
                      {row.materialOverride && (
                        <>
                          <small>
                            SCO: alpha{" "}
                            {row.materialOverride
                              .alphaMode ??
                              "padrão"}
                            {row.materialOverride
                              .noZWrite
                              ? " · noZwrite"
                              : ""}
                            {row.materialOverride
                              .noZCheck
                              ? " · noZcheck"
                              : ""}
                          </small>
                          {row.materialOverride
                            .bumpMapTextureName && (
                            <small>
                              Bump:{" "}
                              {row.materialOverride
                                .bumpMapTextureName}
                              {" · "}
                              {getTextureState(
                                row.materialOverride
                                  .bumpMapTextureName,
                                row.bumpTextureAsset,
                                row.bumpTextureRequested
                              ).label}
                              {row.materialOverride
                                .bumpMapStrength !=
                              null
                                ? ` · fator ${formatNumber(
                                    row.materialOverride
                                      .bumpMapStrength
                                  )}`
                                : ""}
                            </small>
                          )}
                          {row.materialOverride
                            .nightMapTextureName && (
                            <small>
                              Nightmap:{" "}
                              {row.materialOverride
                                .nightMapTextureName}
                              {" · "}
                              {nightPreviewEnabled
                                ? getTextureState(
                                    row.materialOverride
                                      .nightMapTextureName,
                                    row.nightTextureAsset,
                                    row.nightTextureRequested
                                  ).label
                                : "preview desligado"}
                            </small>
                          )}
                          {row.materialOverride
                            .environmentMapTextureName && (
                            <small>
                              Envmap:{" "}
                              {row.materialOverride
                                .environmentMapTextureName}
                              {" · "}
                              {getTextureState(
                                row.materialOverride
                                  .environmentMapTextureName,
                                row.environmentTextureAsset,
                                row.environmentTextureRequested
                              ).label}
                              {row.materialOverride
                                .environmentMapStrength !=
                              null
                                ? ` · força ${formatNumber(
                                    row.materialOverride
                                      .environmentMapStrength
                                  )}`
                                : ""}
                            </small>
                          )}
                          {row.materialOverride
                            .transMapSource && (
                            <small>
                              Transmap:{" "}
                              {row.materialOverride
                                .transMapSource}
                              {" · runtime não simulado"}
                            </small>
                          )}
                          {row.materialOverride
                            .lightMapTextureName && (
                            <small>
                              Lightmap:{" "}
                              {row.materialOverride
                                .lightMapTextureName}
                              {" · runtime não simulado"}
                            </small>
                          )}
                          {row.materialOverride
                            .unsupportedCommands
                            .length > 0 && (
                            <small>
                              Não simulado:{" "}
                              {row.materialOverride
                                .unsupportedCommands
                                .join(", ")}
                            </small>
                          )}
                        </>
                      )}
                    </div>
                  </div>
                );
              })
            )}
          </div>
        )}
      </>
    );
  };

  const renderSplineInspector = () => {
    if (!selectedSpline) {
      return renderMapInspector();
    }

    const profile =
      selectedSplineProfile;

    return (
      <>
        <div className="inspector-hero">
          <div className="object-symbol">S</div>
          <div>
            <strong>
              {getObjectName(
                selectedSpline.splinePath
              )}
            </strong>
            <span>
              Spline #{selectedSpline.splineId}
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
            Traçado
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
            Perfil
          </button>
        </div>

        {inspectorTab === "general" && (
          <>
            <dl className="property-list dense">
              <div>
                <dt>Arquivo</dt>
                <dd>
                  {selectedSpline.splinePath}
              </dd>
            </div>
            <div>
              <dt>ID</dt>
              <dd>
                {selectedSpline.splineId}
              </dd>
            </div>
            <div>
              <dt>Anterior / Próxima</dt>
              <dd>
                {selectedSpline.previousSplineId} /{" "}
                {selectedSpline.nextSplineId}
              </dd>
            </div>
            <div>
              <dt>Tile</dt>
              <dd>
                {selectedSpline.tileX},{" "}
                {selectedSpline.tileY}
              </dd>
            </div>
              <div>
                <dt>Tipo</dt>
                <dd>
                  {selectedSpline.isHeightSpline
                    ? "Spline de altura"
                    : "Spline"}
                </dd>
              </div>
            </dl>

            <button
              type="button"
              className="wide"
              onClick={
                handleStartSplineCopy
              }
              disabled={
                busy ||
                previewEditCount > 0 ||
                splinePreviewEditCount > 0 ||
                Boolean(placementAsset)
              }
              title="Criar uma nova spline desconectada usando esta spline real como template"
            >
              Colocar cópia desconectada
            </button>

            <div className="transform-help">
              A nova spline copia tipo, header e
              parâmetros extras reais. Os vínculos
              anterior/próxima começam em -1 para
              não alterar a cadeia existente.
            </div>

            <div className="spline-link-editor">
              <strong>Vínculos da cadeia</strong>

              <div className="spline-link-fields">
                <label className="transform-field">
                  <span>Anterior ID</span>
                  <input
                    type="number"
                    step="1"
                    list="spline-link-id-options"
                    value={
                      splineLinkPreviousId
                    }
                    onChange={(event) => {
                      const value =
                        event.currentTarget
                          .valueAsNumber;

                      if (
                        Number.isInteger(
                          value
                        )
                      ) {
                        setSplineLinkPreviousId(
                          value
                        );
                      }
                    }}
                  />
                </label>

                <label className="transform-field">
                  <span>Próxima ID</span>
                  <input
                    type="number"
                    step="1"
                    list="spline-link-id-options"
                    value={
                      splineLinkNextId
                    }
                    onChange={(event) => {
                      const value =
                        event.currentTarget
                          .valueAsNumber;

                      if (
                        Number.isInteger(
                          value
                        )
                      ) {
                        setSplineLinkNextId(
                          value
                        );
                      }
                    }}
                  />
                </label>
              </div>

              <datalist id="spline-link-id-options">
                {splines
                  .filter(
                    (candidate) =>
                      candidate.splineId !==
                      selectedSpline.splineId
                  )
                  .map((candidate) => (
                    <option
                      key={
                        candidate.splineId
                      }
                      value={
                        candidate.splineId
                      }
                    >
                      {getObjectName(
                        candidate.splinePath
                      )}
                    </option>
                  ))}
              </datalist>

              <div className="transform-help">
                Use -1 para ponta livre. A lista
                sugere splines carregadas, mas o
                host valida IDs no mapa completo e
                atualiza reciprocamente as pontas
                afetadas.
              </div>

              <div className="spline-edit-actions">
                <button
                  type="button"
                  className="primary-button"
                  onClick={
                    handleSaveSplineLinks
                  }
                  disabled={
                    busy ||
                    (
                      splineLinkPreviousId ===
                        selectedSpline.previousSplineId &&
                      splineLinkNextId ===
                        selectedSpline.nextSplineId
                    ) ||
                    previewEditCount > 0 ||
                    splinePreviewEditCount > 0 ||
                    Boolean(placementAsset) ||
                    Boolean(
                      splinePlacementTemplate
                    )
                  }
                >
                  {savingSplineLinks
                    ? "Atualizando..."
                    : "Salvar vínculos"}
                </button>

                <button
                  type="button"
                  className="secondary-action"
                  disabled={busy}
                  onClick={() => {
                    setSplineLinkPreviousId(
                      -1
                    );
                    setSplineLinkNextId(
                      -1
                    );
                  }}
                >
                  Desconectar rascunho
                </button>
              </div>
            </div>

            <button
              type="button"
              className="danger-action wide"
              onClick={
                handleDeleteSelectedSpline
              }
              disabled={
                busy ||
                previewEditCount > 0 ||
                splinePreviewEditCount > 0 ||
                Boolean(placementAsset) ||
                Boolean(splinePlacementTemplate)
              }
              title="Excluir spline e liberar os vínculos recíprocos dos vizinhos na mesma transação"
            >
              {deletingSpline
                ? "Excluindo..."
                : "Excluir spline"}
            </button>
          </>
        )}

        {inspectorTab === "transform" && (
          <div className="transform-inspector">
            <div className="transform-fields">
              {(
                [
                  ["x", "X", selectedSpline.x],
                  ["y", "Y", selectedSpline.y],
                  ["z", "Z", selectedSpline.z],
                  [
                    "rotation",
                    "Rotação",
                    selectedSpline.rotation
                  ],
                  [
                    "length",
                    "Comprimento",
                    selectedSpline.length
                  ],
                  [
                    "radius",
                    "Raio",
                    selectedSpline.radius
                  ],
                  [
                    "gradientStart",
                    "Gradiente inicial",
                    selectedSpline.gradientStart
                  ],
                  [
                    "gradientEnd",
                    "Gradiente final",
                    selectedSpline.gradientEnd
                  ]
                ] as const
              ).map(
                ([field, label, value]) => (
                  <label
                    key={field}
                    className="transform-field"
                  >
                    <span>{label}</span>
                    <input
                      key={`${getPlacedSplineKey(
                        selectedSpline
                      )}:${field}:${value}`}
                      type="number"
                      step="0.001"
                      defaultValue={value}
                      onKeyDown={(event) => {
                        if (
                          event.key ===
                          "Enter"
                        ) {
                          event.currentTarget.blur();
                        }
                      }}
                      onBlur={(event) => {
                        const next =
                          event.currentTarget
                            .valueAsNumber;

                        if (
                          Number.isFinite(
                            next
                          ) &&
                          next !== value
                        ) {
                          handleSplineNumericTransform(
                            field,
                            next
                          );
                        }
                      }}
                    />
                  </label>
                )
              )}
            </div>

            <div className="transform-help">
              Esta etapa não altera IDs nem
              vínculos anterior/próxima. O Save
              relê o tile e valida a identidade
              da spline antes de gravar.
            </div>

            <div className="spline-edit-actions">
              <button
                type="button"
                className="primary-button"
                onClick={
                  handleSaveSplinePreview
                }
                disabled={
                  splinePreviewEditCount === 0 ||
                  savingSpline
                }
              >
                {savingSpline
                  ? "Salvando..."
                  : "Salvar spline"}
              </button>

              <button
                type="button"
                className="secondary-action"
                onClick={
                  handleDiscardSplinePreview
                }
                disabled={
                  splinePreviewEditCount === 0 ||
                  savingSpline
                }
              >
                Descartar prévia
              </button>
            </div>
          </div>
        )}

        {inspectorTab === "geometry" && (
          <div className="inspector-stack">
            {loadingSplineFor ===
              selectedSpline.splinePath && (
              <div className="inspector-empty">
                Lendo perfil .sli...
              </div>
            )}

            {profile && (
              <>
                <dl className="property-list dense">
                  <div>
                    <dt>Arquivo encontrado</dt>
                    <dd>
                      {profile.exists
                        ? "Sim"
                        : "Não"}
                    </dd>
                  </div>
                  <div>
                    <dt>Texturas</dt>
                    <dd>
                      {profile.textures.length}
                    </dd>
                  </div>
                  <div>
                    <dt>Superfícies</dt>
                    <dd>
                      {profile.surfaces.length}
                    </dd>
                  </div>
                </dl>

                <div className="mesh-list">
                  {profile.surfaces.map(
                    (surface, index) => {
                      const textureKey =
                        surface.textureName
                          ? getSplineTextureAssetKey(
                              selectedSpline
                                .splinePath,
                              surface
                                .textureName
                            )
                          : undefined;

                      const textureState =
                        getTextureState(
                          surface.textureName,
                          textureKey
                            ? textureAssetsByKey[
                                textureKey
                              ]
                            : undefined,
                          textureKey
                            ? Boolean(
                                requestedTextureKeys[
                                  textureKey
                                ]
                              )
                            : false
                        );

                      return (
                        <div
                          className="mesh-row spline-surface-row"
                          key={`${surface.textureIndex}-${index}`}
                        >
                          <div>
                            <strong>
                              {surface.textureName ??
                                `Material ${surface.textureIndex}`}
                            </strong>
                            <small>
                              {formatNumber(
                                surface.from.x
                              )} →{" "}
                              {formatNumber(
                                surface.to.x
                              )} m
                            </small>
                          </div>
                          <span
                            className={`texture-state ${textureState.tone}`}
                          >
                            {textureState.label}
                          </span>
                        </div>
                      );
                    }
                  )}
                </div>
              </>
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
            className={
              editorTool === "select"
                ? "tool active"
                : "tool"
            }
            title="Selecionar (Q)"
            onClick={() =>
              setEditorTool("select")
            }
          >
            ↖
          </button>
          <button
            type="button"
            className={
              editorTool === "move"
                ? "tool active"
                : "tool"
            }
            disabled={
              !selectedObject &&
              !selectedSpline
            }
            title="Mover seleção em prévia (W)"
            onClick={() =>
              setEditorTool("move")
            }
          >
            ✥
          </button>
          <button
            type="button"
            className={
              editorTool === "rotate"
                ? "tool active"
                : "tool"
            }
            disabled={
              !selectedObject &&
              !selectedSpline
            }
            title="Rotacionar seleção em prévia (E)"
            onClick={() =>
              setEditorTool("rotate")
            }
          >
            ⟳
          </button>
          <button
            type="button"
            className="tool"
            disabled
            title="Escala — ainda não suportada pelo formato de objeto OMSI nesta etapa"
          >
            ◫
          </button>

          <span className="toolbar-separator" />

          <button
            type="button"
            className="tool"
            title="Enquadrar mapa"
            onClick={() =>
              requestCameraAction("fit")
            }
          >
            ⛶
          </button>

          <button
            type="button"
            className="tool"
            title="Focar seleção"
            disabled={
              !selectedObject &&
              !selectedSpline
            }
            onClick={() =>
              requestCameraAction(
                "focus"
              )
            }
          >
            ◎
          </button>

          <button
            type="button"
            className={
              isFullScreen
                ? "tool active"
                : "tool"
            }
            title={
              isFullScreen
                ? "Sair da tela cheia (F11 ou Esc)"
                : "Tela cheia (F11)"
            }
            onClick={() =>
              setFullScreen(
                !isFullScreen
              )
            }
          >
            ⤢
          </button>

          <button
            type="button"
            className="tool"
            title="Desfazer transformação (Ctrl+Z)"
            disabled={
              undoPreviewStack.length === 0
            }
            onClick={
              handleUndoPreview
            }
          >
            ↶
          </button>

          <button
            type="button"
            className="tool"
            title="Refazer transformação (Ctrl+Y)"
            disabled={
              redoPreviewStack.length === 0
            }
            onClick={
              handleRedoPreview
            }
          >
            ↷
          </button>

          <button
            type="button"
            className="tool"
            title="Descartar todas as transformações temporárias"
            disabled={
              previewEditCount === 0 &&
              splinePreviewEditCount === 0
            }
            onClick={
              splinePreviewEditCount > 0
                ? handleDiscardSplinePreview
                : handleDiscardPreviewEdits
            }
          >
            ✕
          </button>

          <span className="toolbar-separator" />

          <span className="toolbar-chip">
            Global
          </span>
          <span className="toolbar-chip">
            Q/W/E · 1/2 · N · F · F11 · Ctrl+Z/Y/S
          </span>
          <span
            className="toolbar-chip"
            title="Navegação do viewport"
          >
            RMB orbita · MMB desloca · roda zoom · setas movem
          </span>

          <span className="toolbar-separator" />

          <button
            type="button"
            className={
              mapLoadMode === "full"
                ? "secondary-action active-mode"
                : "secondary-action"
            }
            disabled={busy}
            onClick={() => {
              if (
                mapLoadMode === "full"
              ) {
                return;
              }

              setMapLoadMode("full");
              setLoadedFullMapFor(
                undefined
              );
              setLoadedRegionKey(
                undefined
              );
              setSelectedObject(
                undefined
              );
              setSelectedSpline(
                undefined
              );
            }}
          >
            Mapa completo
          </button>

          <button
            type="button"
            className={
              mapLoadMode ===
              "performance"
                ? "secondary-action active-mode"
                : "secondary-action"
            }
            disabled={busy}
            onClick={() => {
              if (
                mapLoadMode ===
                "performance"
              ) {
                return;
              }

              setMapLoadMode(
                "performance"
              );
              setLoadedRegionKey(
                undefined
              );
              setObjects([]);
              setSplines([]);
              setSelectedObject(
                undefined
              );
              setSelectedSpline(
                undefined
              );
            }}
          >
            Modo desempenho 3×3
          </button>

          <span className="toolbar-spacer" />

          <button
            type="button"
            className="primary-button editor-save"
            onClick={
              splinePreviewEditCount > 0
                ? handleSaveSplinePreview
                : handleSavePreviewEdits
            }
            disabled={
              (
                previewEditCount === 0 &&
                splinePreviewEditCount === 0
              ) ||
              busy
            }
            title="Salvar transformações com backup automático (Ctrl+S)"
          >
            {saving || savingSpline
              ? "Salvando..."
              : `Salvar${
                  previewEditCount +
                    splinePreviewEditCount >
                  0
                    ? ` (${
                        previewEditCount +
                        splinePreviewEditCount
                      })`
                    : ""
                }`}
          </button>

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
                className={
                  explorerPanelTab ===
                  "map"
                    ? "active"
                    : ""
                }
                onClick={() =>
                  handleExplorerPanelTab(
                    "map"
                  )
                }
              >
                Explorador
              </button>
              <button
                type="button"
                className={
                  explorerPanelTab ===
                  "library"
                    ? "active"
                    : ""
                }
                onClick={() =>
                  handleExplorerPanelTab(
                    "library"
                  )
                }
              >
                Biblioteca
              </button>
              <button
                type="button"
                className={
                  explorerPanelTab ===
                  "splineLibrary"
                    ? "active"
                    : ""
                }
                onClick={() =>
                  handleExplorerPanelTab(
                    "splineLibrary"
                  )
                }
              >
                Splines
              </button>
            </div>

            {explorerPanelTab === "map" ? (
              <>
              <div className="explorer-tree">
                <div className="tree-root">
                  <span>▾</span>
                  <strong>
                    {selectedMap.displayName}
                  </strong>
                </div>
  
                <div className="tree-node active">
                  <span>▣</span>
                  {mapLoadMode === "full"
                    ? "Objetos"
                    : "Objetos (área)"}
                  <strong>
                    {selectedStats?.objects ??
                      objects.length}
                  </strong>
                </div>
  
                <div className="tree-node">
                  <span>◈</span>
                  {mapLoadMode === "full"
                    ? "Modelos O3D"
                    : "Modelos O3D (área)"}
                  <strong>
                    {mapLoadMode === "full"
                      ? `${loadedMapGeometryCount}/${mapObjectPaths.length}`
                      : `${loadedNearbyGeometryCount}/${nearbyObjectPaths.length}`}
                  </strong>
                </div>
  
                <div className="tree-node">
                  <span>⌇</span>
                  {mapLoadMode === "full"
                    ? "Splines"
                    : "Splines (área)"}
                  <strong>
                    {selectedStats?.splines ??
                      splines.length}
                  </strong>
                </div>
  
                <div className="tree-node">
                  <span>▧</span>
                  Terreno
                  <strong>
                    {activeTiles.filter(
                      (tile) =>
                        Boolean(
                          tile.terrain
                        )
                    ).length}
                  </strong>
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
                  Tiles totais
                  <strong>
                    {selectedMap.tiles.length}
                  </strong>
                </div>
              </div>
  
              <div className="explorer-search">
                <input
                  type="search"
                  placeholder="Buscar objeto, spline, ID ou tile..."
                  value={explorerSearch}
                  onChange={(event) =>
                    setExplorerSearch(
                      event.target.value
                    )
                  }
                />
                <span>
                  {explorerResultCount}
                  {" "}resultado(s)
                  {explorerObjectResultCount > 250 ||
                  explorerSplineResultCount > 250
                    ? " · listas limitadas a 250"
                    : ""}
                </span>
              </div>
  
              <div className="explorer-object-list">
                <div className="explorer-section-label">
                  Objetos · {explorerObjectResultCount}
                </div>

                {filteredExplorerObjects.map(
                  (placedObject) => {
                    const key =
                      getPlacedObjectKey(
                        placedObject
                      );
  
                    const isSelected =
                      selectedObject &&
                      getPlacedObjectKey(
                        selectedObject
                      ) === key;
  
                    const hasPreview =
                      Object.hasOwn(
                        previewObjectTransforms,
                        key
                      );
  
                    return (
                      <button
                        type="button"
                        className={
                          isSelected
                            ? "explorer-object active"
                            : "explorer-object"
                        }
                        key={key}
                        onClick={() => {
                          handleObjectSelection(
                            placedObject
                          );
  
                          requestCameraAction(
                            "focus"
                          );
                        }}
                      >
                        <span
                          className="explorer-object-name"
                          title={
                            placedObject.sceneryObjectPath
                          }
                        >
                          {getObjectName(
                            placedObject
                              .sceneryObjectPath
                          )}
                        </span>
                        <small>
                          #{placedObject.objectId}
                          {" · "}
                          {placedObject.tileX},
                          {placedObject.tileY}
                          {hasPreview
                            ? " · alterado"
                            : ""}
                        </small>
                      </button>
                    );
                  }
                )}
  
                {filteredExplorerObjects.length ===
                  0 && (
                  <div className="explorer-empty">
                    Nenhum objeto encontrado.
                  </div>
                )}

                <div className="explorer-section-label">
                  Splines · {explorerSplineResultCount}
                </div>

                {filteredExplorerSplines.map(
                  (placedSpline) => {
                    const key =
                      getPlacedSplineKey(
                        placedSpline
                      );

                    const isSelected =
                      selectedSpline &&
                      getPlacedSplineKey(
                        selectedSpline
                      ) === key;

                    const hasPreview =
                      Object.hasOwn(
                        previewSplineTransforms,
                        key
                      );

                    return (
                      <button
                        type="button"
                        className={
                          isSelected
                            ? "explorer-object active"
                            : "explorer-object"
                        }
                        key={key}
                        onClick={() => {
                          handleSplineSelection(
                            placedSpline
                          );

                          requestCameraAction(
                            "focus"
                          );
                        }}
                      >
                        <span
                          className="explorer-object-name"
                          title={
                            placedSpline.splinePath
                          }
                        >
                          {getObjectName(
                            placedSpline
                              .splinePath
                          )}
                        </span>
                        <small>
                          #{placedSpline.splineId}
                          {" · "}
                          {placedSpline.tileX},
                          {placedSpline.tileY}
                          {" · "}
                          {placedSpline.isHeightSpline
                            ? "altura"
                            : "spline"}
                          {hasPreview
                            ? " · alterada"
                            : ""}
                        </small>
                      </button>
                    );
                  }
                )}

                {filteredExplorerSplines.length ===
                  0 && (
                  <div className="explorer-empty">
                    Nenhuma spline encontrada.
                  </div>
                )}

              </div>
              </>
            ) : explorerPanelTab ===
              "library" ? (
              <div className="scenery-library-panel">
                <div className="explorer-search">
                  <input
                    type="search"
                    placeholder="Buscar .sco na instalação..."
                    value={librarySearch}
                    onChange={(event) =>
                      setLibrarySearch(
                        event.target.value
                      )
                    }
                  />
                  <span>
                    {loadingSceneryLibrary
                      ? "Lendo Sceneryobjects..."
                      : `${sceneryLibraryResultCount} objeto(s)${sceneryLibraryResultCount > 300 ? " · mostrando 300" : ""}`}
                  </span>
                </div>

                <div className="scenery-library-list">
                  {filteredSceneryLibrary.map(
                    (entry) => (
                      <div
                        className={
                          placementAsset
                            ?.sceneryObjectPath ===
                          entry.sceneryObjectPath
                            ? "scenery-library-entry active"
                            : "scenery-library-entry"
                        }
                        key={
                          entry.sceneryObjectPath
                        }
                      >
                        <strong
                          title={
                            entry.sceneryObjectPath
                          }
                        >
                          {entry.fileName}
                        </strong>
                        <span>
                          {entry.sceneryObjectPath}
                        </span>
                        <button
                          type="button"
                          onClick={() =>
                            handleSelectPlacementAsset(
                              entry
                            )
                          }
                          disabled={
                            insertingObject
                          }
                          title="Selecionar para colocação no mapa"
                        >
                          Colocar
                        </button>
                      </div>
                    )
                  )}

                  {!loadingSceneryLibrary &&
                    filteredSceneryLibrary.length ===
                      0 && (
                      <div className="explorer-empty">
                        Nenhum arquivo .sco encontrado.
                      </div>
                    )}
                </div>
              </div>
            ) : (
              <div className="scenery-library-panel">
                <div className="explorer-search">
                  <input
                    type="search"
                    placeholder="Buscar .sli na instalação..."
                    value={
                      splineLibrarySearch
                    }
                    onChange={(event) =>
                      setSplineLibrarySearch(
                        event.target.value
                      )
                    }
                  />
                  <span>
                    {loadingSplineLibrary
                      ? "Lendo Splines..."
                      : `${splineLibraryResultCount} spline(s)${splineLibraryResultCount > 300 ? " · mostrando 300" : ""}`}
                  </span>
                </div>

                <div className="scenery-library-list">
                  {filteredSplineLibrary.map(
                    (entry) => (
                      <div
                        className={
                          splineLibraryPlacementAsset
                            ?.splinePath ===
                          entry.splinePath
                            ? "scenery-library-entry active"
                            : "scenery-library-entry"
                        }
                        key={
                          entry.splinePath
                        }
                      >
                        <strong
                          title={
                            entry.splinePath
                          }
                        >
                          {entry.fileName}
                        </strong>
                        <span>
                          {entry.splinePath}
                        </span>
                        <div className="library-entry-actions">
                          <button
                            type="button"
                            onClick={() =>
                              handleSelectSplineLibraryAsset(
                                entry,
                                false
                              )
                            }
                            disabled={
                              insertingSpline
                            }
                            title="Criar uma nova [spline] normal usando este .sli"
                          >
                            Normal
                          </button>

                          <button
                            type="button"
                            onClick={() =>
                              handleSelectSplineLibraryAsset(
                                entry,
                                true
                              )
                            }
                            disabled={
                              insertingSpline
                            }
                            title="Criar uma nova [spline_h] usando este .sli"
                          >
                            Altura
                          </button>
                        </div>
                      </div>
                    )
                  )}

                  {!loadingSplineLibrary &&
                    filteredSplineLibrary.length ===
                      0 && (
                      <div className="explorer-empty">
                        Nenhum arquivo .sli encontrado.
                      </div>
                    )}
                </div>

                <div className="library-safety-note">
                  A prévia usa o .sli real.
                  Ao salvar, o host exige um
                  template real neutro do mesmo
                  tipo ([spline] ou [spline_h])
                  já existente no mapa para copiar
                  header e extras sem inventar
                  metadados.
                </div>
              </div>
            )}
          </aside>

          <section className="editor-viewport">
            <Viewport
              tiles={activeTiles}
              cameraStateKey={
                `${selectedMap.directoryName}:${mapLoadMode}`
              }
              objects={
                objectsForViewport
              }
              splines={
                splinesForViewport
              }
              editorTool={editorTool}
              snapEnabled={snapEnabled}
              moveSnap={moveSnap}
              rotationSnap={
                rotationSnap
              }
              showGrid={showGrid}
              showTerrain={showTerrain}
              terrainMainTextureAsset={
                Object.hasOwn(
                  hiddenTerrainLayerIndices,
                  0
                )
                  ? undefined
                  : baseGroundMainAsset
              }
              terrainMainTextureRepeating={
                baseGroundTexture
                  ?.mainTextureRepeating
              }
              terrainOverlays={
                terrainOverlayPreviews
              }
              showObjects={showObjects}
              showSplines={showSplines}
              showSplineProfiles={
                showSplineProfiles
              }
              nightPreviewEnabled={
                nightPreviewEnabled
              }
              cameraAction={cameraAction}
              placementAssetPath={
                placementAsset
                  ?.sceneryObjectPath
              }
              placementGeometry={
                placementAsset
                  ? geometryByPath[
                      placementAsset
                        .sceneryObjectPath
                    ]
                  : undefined
              }
              pendingPlacement={
                pendingPlacement
              }
              onPlacementPoint={
                handlePlacementPoint
              }
              splinePlacementTemplate={
                splinePlacementTemplate
              }
              splinePlacementProfile={
                splinePlacementTemplate
                  ? splineProfilesByPath[
                      splinePlacementTemplate
                        .splinePath
                    ]
                  : undefined
              }
              pendingSplinePlacement={
                pendingSplinePlacement
              }
              onSplinePlacementPoint={
                handleSplinePlacementPoint
              }
              activeTile={activeTile}
              onActiveTileChange={
                (tile) => {
                  if (
                    tile.x ===
                      activeTile?.x &&
                    tile.y ===
                      activeTile?.y
                  ) {
                    return;
                  }

                  setActiveTile(tile);

                  if (
                    mapLoadMode ===
                    "performance"
                  ) {
                    setLoadedRegionKey(
                      undefined
                    );
                    setObjects([]);
                    setSplines([]);
                  }

                  setSelectedObject(
                    undefined
                  );
                  setSelectedSpline(
                    undefined
                  );
                }
              }
              usesWorldCoordinates={
                selectedMap.usesWorldCoordinates
              }
              selectedObject={selectedObject}
              selectedGeometry={selectedGeometry}
              objectGeometryByPath={
                geometryByPath
              }
              textureAssetsByKey={
                textureAssetsByKey
              }
              splineProfilesByPath={
                splineProfilesByPath
              }
              selectedSpline={selectedSpline}
              selectedSplineProfile={
                selectedSplineProfile
              }
              onSelectObject={
                handleObjectSelection
              }
              onSelectSpline={
                handleSplineSelection
              }
              onPreviewObjectTransform={
                handlePreviewObjectTransform
              }
              onPreviewSplineTransform={
                handlePreviewSplineTransform
              }
            />

            {splinePlacementTemplate && (
              <div className="placement-bar">
                <div>
                  <strong>
                    {splineLibraryPlacementAsset
                      ? splineLibraryPlacementIsHeight
                        ? "Nova spline de altura: "
                        : "Nova spline: "
                      : "Copiando spline: "}
                    {getObjectName(
                      splinePlacementTemplate
                        .splinePath
                    )}
                  </strong>
                  <span>
                    {pendingSplinePlacement
                      ? `Tile ${pendingSplinePlacement.targetTileX},${pendingSplinePlacement.targetTileY} · X ${formatNumber(pendingSplinePlacement.x)} · Y ${formatNumber(pendingSplinePlacement.y)} · desconectada`
                      : splineLibraryPlacementAsset
                        ? splineLibraryPlacementIsHeight
                          ? "Clique em um tile para posicionar a nova spline de altura."
                          : "Clique em um tile para posicionar a nova spline normal."
                        : "Clique em um tile para posicionar o início da cópia."}
                  </span>
                </div>

                {pendingSplinePlacement && (
                  <>
                    {(
                      [
                        ["z", "Z", 0.1],
                        [
                          "rotation",
                          "Rot °",
                          1
                        ],
                        [
                          "length",
                          "Comp.",
                          0.1
                        ],
                        [
                          "radius",
                          "Raio",
                          0.1
                        ],
                        [
                          "gradientStart",
                          "Grad. I",
                          0.1
                        ],
                        [
                          "gradientEnd",
                          "Grad. F",
                          0.1
                        ]
                      ] as const
                    ).map(
                      ([field, label, step]) => (
                        <label
                          className="placement-field"
                          key={field}
                        >
                          <span>{label}</span>
                          <input
                            type="number"
                            step={step}
                            min={
                              field === "length"
                                ? 0
                                : undefined
                            }
                            value={
                              pendingSplinePlacement[
                                field
                              ]
                            }
                            onChange={(event) => {
                              const value =
                                event.currentTarget
                                  .valueAsNumber;

                              if (
                                Number.isFinite(
                                  value
                                ) &&
                                (
                                  field !==
                                    "length" ||
                                  value >= 0
                                )
                              ) {
                                setPendingSplinePlacement(
                                  (current) =>
                                    current
                                      ? {
                                          ...current,
                                          [field]:
                                            value
                                        }
                                      : current
                                );
                              }
                            }}
                          />
                        </label>
                      )
                    )}
                  </>
                )}

                <button
                  type="button"
                  className="primary-button"
                  disabled={
                    !pendingSplinePlacement ||
                    insertingSpline
                  }
                  onClick={
                    handleConfirmSplinePlacement
                  }
                  title="Criar backup e inserir uma spline desconectada"
                >
                  {insertingSpline
                    ? "Inserindo..."
                    : "Confirmar e salvar"}
                </button>

                <button
                  type="button"
                  className="secondary-action"
                  disabled={insertingSpline}
                  onClick={
                    handleCancelSplinePlacement
                  }
                >
                  Cancelar
                </button>
              </div>
            )}

            {placementAsset && (
              <div className="placement-bar">
                <div>
                  <strong>
                    Colocando:{" "}
                    {placementAsset.fileName}
                  </strong>
                  <span>
                    {pendingPlacement
                      ? `Tile ${pendingPlacement.tileX},${pendingPlacement.tileY} · X ${formatNumber(pendingPlacement.x)} · Y ${formatNumber(pendingPlacement.y)}`
                      : "Clique em um tile para posicionar a prévia."}
                  </span>

                  {mapLoadMode ===
                    "full" &&
                    !placementHasKnownTemplate && (
                    <span className="placement-warning">
                      Prévia apenas: este .sco ainda não existe no mapa, então seus parâmetros extras não podem ser derivados com segurança.
                    </span>
                  )}
                </div>

                {pendingPlacement && (
                  <>
                    <label className="placement-field">
                      <span>Z</span>
                      <input
                        type="number"
                        step="0.1"
                        value={
                          pendingPlacement.z
                        }
                        onChange={(event) => {
                          const value =
                            event.currentTarget
                              .valueAsNumber;

                          if (
                            Number.isFinite(
                              value
                            )
                          ) {
                            setPendingPlacement(
                              (current) =>
                                current
                                  ? {
                                      ...current,
                                      z: value
                                    }
                                  : current
                            );
                          }
                        }}
                      />
                    </label>

                    <label className="placement-field">
                      <span>Rot °</span>
                      <input
                        type="number"
                        step="1"
                        value={
                          pendingPlacement.rotation
                        }
                        onChange={(event) => {
                          const value =
                            event.currentTarget
                              .valueAsNumber;

                          if (
                            Number.isFinite(
                              value
                            )
                          ) {
                            setPendingPlacement(
                              (current) =>
                                current
                                  ? {
                                      ...current,
                                      rotation:
                                        value
                                    }
                                  : current
                            );
                          }
                        }}
                      />
                    </label>

                    <label className="placement-field">
                      <span>Pitch °</span>
                      <input
                        type="number"
                        step="1"
                        value={
                          pendingPlacement.pitch
                        }
                        onChange={(event) => {
                          const value =
                            event.currentTarget
                              .valueAsNumber;

                          if (
                            Number.isFinite(
                              value
                            )
                          ) {
                            setPendingPlacement(
                              (current) =>
                                current
                                  ? {
                                      ...current,
                                      pitch: value
                                    }
                                  : current
                            );
                          }
                        }}
                      />
                    </label>

                    <label className="placement-field">
                      <span>Bank °</span>
                      <input
                        type="number"
                        step="1"
                        value={
                          pendingPlacement.bank
                        }
                        onChange={(event) => {
                          const value =
                            event.currentTarget
                              .valueAsNumber;

                          if (
                            Number.isFinite(
                              value
                            )
                          ) {
                            setPendingPlacement(
                              (current) =>
                                current
                                  ? {
                                      ...current,
                                      bank: value
                                    }
                                  : current
                            );
                          }
                        }}
                      />
                    </label>
                  </>
                )}

                <button
                  type="button"
                  className="primary-button"
                  disabled={
                    !placementCanPersist ||
                    insertingObject
                  }
                  onClick={
                    handleConfirmPlacement
                  }
                  title={
                    placementCanPersist
                      ? "Criar backup e inserir o objeto"
                      : "É necessário um template real do mesmo .sco no mapa"
                  }
                >
                  {insertingObject
                    ? "Inserindo..."
                    : "Confirmar e salvar"}
                </button>

                <button
                  type="button"
                  className="secondary-action"
                  disabled={insertingObject}
                  onClick={
                    handleCancelPlacement
                  }
                >
                  Cancelar
                </button>
              </div>
            )}

            <div className="viewport-toolbar">
              <button
                type="button"
                className={
                  cameraMode ===
                  "perspective"
                    ? "viewport-mode active"
                    : "viewport-mode"
                }
                onClick={() => {
                  setCameraMode(
                    "perspective"
                  );
                  requestCameraAction(
                    "perspective"
                  );
                }}
              >
                Perspectiva · 1
              </button>

              <button
                type="button"
                className={
                  cameraMode === "top"
                    ? "viewport-mode active"
                    : "viewport-mode"
                }
                onClick={() => {
                  setCameraMode("top");
                  requestCameraAction(
                    "top"
                  );
                }}
              >
                Topo · 2
              </button>

              <button
                type="button"
                className={
                  snapEnabled
                    ? "viewport-mode active"
                    : "viewport-mode"
                }
                onClick={() =>
                  setSnapEnabled(
                    (current) =>
                      !current
                  )
                }
                title="Alternar snap (N)"
              >
                Snap · N
              </button>

              <label className="snap-field">
                <span>m</span>
                <input
                  type="number"
                  min="0.01"
                  step="0.1"
                  value={moveSnap}
                  onChange={(event) => {
                    const value =
                      event.currentTarget
                        .valueAsNumber;

                    if (
                      Number.isFinite(
                        value
                      ) &&
                      value > 0
                    ) {
                      setMoveSnap(
                        value
                      );
                    }
                  }}
                />
              </label>

              <label className="snap-field">
                <span>°</span>
                <input
                  type="number"
                  min="0.1"
                  step="1"
                  value={rotationSnap}
                  onChange={(event) => {
                    const value =
                      event.currentTarget
                        .valueAsNumber;

                    if (
                      Number.isFinite(
                        value
                      ) &&
                      value > 0
                    ) {
                      setRotationSnap(
                        value
                      );
                    }
                  }}
                />
              </label>

              <span className="viewport-tool-state">
                Ferramenta:{" "}
                {editorTool === "select"
                  ? "Selecionar"
                  : editorTool === "move"
                    ? "Mover"
                    : "Rotacionar"}
              </span>

              {previewEditCount > 0 && (
                <span className="preview-warning">
                  Prévia objeto ·{" "}
                  {previewEditCount}
                </span>
              )}

              {splinePreviewEditCount > 0 && (
                <span className="preview-warning">
                  Prévia spline ·{" "}
                  {splinePreviewEditCount}
                </span>
              )}
            </div>

            <div className="viewport-layers">
              <label>
                <input
                  type="checkbox"
                  checked={showGrid}
                  onChange={(event) =>
                    setShowGrid(
                      event.target.checked
                    )
                  }
                />
                Grade / tiles
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={showObjects}
                  onChange={(event) =>
                    setShowObjects(
                      event.target.checked
                    )
                  }
                />
                Objetos
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={showSplines}
                  onChange={(event) =>
                    setShowSplines(
                      event.target.checked
                    )
                  }
                />
                Splines
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={
                    showSplineProfiles
                  }
                  disabled={!showSplines}
                  onChange={(event) =>
                    setShowSplineProfiles(
                      event.target.checked
                    )
                  }
                />
                Perfis spline
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={
                    nightPreviewEnabled
                  }
                  onChange={(event) =>
                    setNightPreviewEnabled(
                      event.target.checked
                    )
                  }
                />
                Nightmap
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={showTerrain}
                  disabled={
                    selectedMap
                      .usesWorldCoordinates
                  }
                  onChange={(event) =>
                    setShowTerrain(
                      event.target.checked
                    )
                  }
                />
                Terreno
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={
                    showTerrainPaint
                  }
                  disabled={
                    !showTerrain ||
                    selectedMap
                      .usesWorldCoordinates
                  }
                  onChange={(event) =>
                    setShowTerrainPaint(
                      event.target.checked
                    )
                  }
                />
                Pintura terreno
              </label>

              <button
                type="button"
                className="texture-cache-clear"
                disabled={
                  Object.keys(
                    textureAssetsByKey
                  ).length === 0 &&
                  Object.keys(
                    requestedTextureKeys
                  ).length === 0 &&
                  Object.keys(
                    groundTextureAssetsByKey
                  ).length === 0 &&
                  Object.keys(
                    requestedGroundTextureKeys
                  ).length === 0 &&
                  Object.keys(
                    terrainMaskAssetsByKey
                  ).length === 0 &&
                  Object.keys(
                    requestedTerrainMaskKeys
                  ).length === 0
                }
                onClick={() => {
                  setTextureAssetsByKey({});
                  setRequestedTextureKeys({});
                  setAutoPrefetchedTextureKeys(
                    {}
                  );
                  setGroundTextureAssetsByKey(
                    {}
                  );
                  setRequestedGroundTextureKeys(
                    {}
                  );
                  setTerrainMaskAssetsByKey(
                    {}
                  );
                  setRequestedTerrainMaskKeys(
                    {}
                  );
                  textureCacheOrderRef.current =
                    [];
                  groundTextureCacheOrderRef
                    .current = [];
                  terrainMaskCacheOrderRef
                    .current = [];
                }}
                title="Limpar texturas de objetos, splines, terreno e máscaras sem descarregar o mapa"
              >
                Limpar cache
              </button>
            </div>

            {(loadingFullMap ||
              Boolean(loadingRegionKey)) && (
              <div className="viewport-loading">
                {loadingFullMap
                  ? `Carregando mapa completo${fullMapProgress ? ` · ${fullMapProgress.completed}/${fullMapProgress.total} tiles` : ""}...`
                  : "Carregando área ativa..."}
              </div>
            )}
          </section>

          <aside className="object-inspector">
            <div className="inspector-heading">
              <strong>Inspetor</strong>
              <span>
                {selectedObject
                  ? "Objeto selecionado"
                  : selectedSpline
                    ? "Spline selecionada"
                    : "Mapa aberto"}
              </span>
            </div>

            {selectedSpline
              ? renderSplineInspector()
              : renderObjectInspector()}
          </aside>
        </div>

        <footer className="editor-statusbar">
          <span>
            {error
              ? "Erro"
              : insertingObject
                ? "Inserindo objeto com backup..."
                : saving
                  ? "Salvando com backup..."
                : saveNotice
                  ? saveNotice
                  : loadingFullMap
                ? "Carregando mapa completo..."
                : Boolean(loadingRegionKey)
                  ? "Carregando área..."
                : preloadingGeometryFor
                  ? mapLoadMode === "full"
                    ? `Carregando modelos O3D ${loadedMapGeometryCount}/${mapObjectPaths.length}...`
                    : `Carregando modelos O3D da área ${loadedNearbyGeometryCount}/${nearbyObjectPaths.length}...`
                  : loadingSplineFor
                    ? "Lendo SLI..."
                  : preloadingSplineProfileFor
                    ? "Preparando perfis SLI próximos..."
                  : loadingMetadataFor
                    ? "Lendo SCO..."
                    : loadingGeometryFor
                    ? "Lendo geometria..."
                    : "Pronto"}
          </span>

          <span>
            Modo:{" "}
            {mapLoadMode === "full"
              ? "Mapa completo"
              : "Desempenho 3×3"}
            <b>·</b>
            Objetos:{" "}
            {selectedStats?.objects ??
              objects.length}
            <b>·</b>
            Splines:{" "}
            {selectedStats?.splines ??
              splines.length}
            <b>·</b>
            O3D:{" "}
            {mapLoadMode === "full"
              ? `${loadedMapGeometryCount}/${mapObjectPaths.length}`
              : `${loadedNearbyGeometryCount}/${nearbyObjectPaths.length}`}
            <b>·</b>
            Perfis SLI:{" "}
            {Object.keys(
              splineProfilesByPath
            ).length}
            <b>·</b>
            Texturas auto:{" "}
            {Object.keys(
              autoPrefetchedTextureKeys
            ).length}/
            {autoTextureLimit}
            <b>·</b>
            Cache:{" "}
            {Object.keys(
              textureAssetsByKey
            ).length}/
            {maxTextureCacheEntries}
            <b>·</b>
            Prévia:{" "}
            {previewEditCount}
            <b>·</b>
            Tiles:{" "}
            {activeTiles.length}/
            {selectedMap.tiles.length}
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
