import {
  useCallback,
  useEffect,
  useMemo,
  useState
} from "react";
import {
  insertObject,
  isDesktopBridgeAvailable,
  loadMapFull,
  loadMapRegion,
  loadSceneryLibrary,
  loadSplineProfile,
  loadSceneryObjectGeometry,
  loadSceneryObjectMetadata,
  saveObjectTransforms,
  selectMap,
  selectOmsiRoot,
  subscribeToHost,
  type SceneryLibraryEntry,
  type OmsiMap,
  type OmsiPlacedObject,
  type OmsiPlacedSpline,
  type OmsiSplineDefinition,
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

  const [showObjects, setShowObjects] =
    useState(true);

  const [showSplines, setShowSplines] =
    useState(true);

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
    previewObjectTransforms,
    setPreviewObjectTransforms
  ] = useState<
    Record<string, OmsiPlacedObject>
  >({});

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
    "map" | "library"
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

  const [saving, setSaving] =
    useState(false);

  const [saveNotice, setSaveNotice] =
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
          setSelectedSpline(undefined);
          setSplineProfilesByPath({});
          setSceneryMetadataByPath({});
          setGeometryByPath({});
          setPreviewObjectTransforms({});
          setUndoPreviewStack([]);
          setRedoPreviewStack([]);
          setEditorTool("select");
          setPreloadingGeometryFor(undefined);
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
          setPlacementAsset(undefined);
          setPendingPlacement(undefined);
          setInsertingObject(false);
          setError(undefined);
          setView("map");
          return;
        }

        if (message.type === "mapOpened") {
          setSelectedMap(message.map);
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
          setPreloadingGeometryFor(undefined);
          setSelectingMap(false);
          setMapLoadMode("full");
          setEditorTool("select");
          setCameraMode("perspective");
          setPreviewObjectTransforms({});
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
          "objectTransformsSaved"
        ) {
          setSaving(false);
          setPreviewObjectTransforms({});
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
          setLoadingMetadataFor(undefined);
          setLoadingGeometryFor(undefined);
          setPreloadingGeometryFor(undefined);
          setLoadingSceneryLibrary(false);
          setSaving(false);
          setInsertingObject(false);

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
          (tile.fileExists ? 0 : 1)
      }),
      {
        objects: 0,
        splines: 0,
        attachments: 0,
        missingTiles: 0
      }
    );
  }, [activeTiles]);

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
        setSelectedSpline(
          placedSpline
        );

        if (placedSpline) {
          setSelectedObject(undefined);
          setInspectorTab("general");
        }

        setError(undefined);
      },
      []
    );

  const handlePreviewObjectTransform =
    useCallback(
      (
        placedObject:
          OmsiPlacedObject
      ) => {
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
        previewObjectTransforms
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
        selectedObject
      ) {
        setEditorTool("move");
        return;
      }

      if (
        key === "e" &&
        selectedObject
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
    handleUndoPreview,
    previewEditCount,
    requestCameraAction,
    saving,
    selectedObject,
    selectedSpline
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
        selectedMap
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

  const handleExplorerPanelTab =
    useCallback(
      (tab: "map" | "library") => {
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
      },
      [
        bridgeAvailable,
        loadingSceneryLibrary,
        sceneryLibrary.length
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
    insertingObject ||
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
        )}

        {inspectorTab === "transform" && (
          <dl className="property-list dense">
            <div>
              <dt>Posição X / Y / Z</dt>
              <dd>
                {formatNumber(
                  selectedSpline.x
                )}{" / "}
                {formatNumber(
                  selectedSpline.y
                )}{" / "}
                {formatNumber(
                  selectedSpline.z
                )}
              </dd>
            </div>
            <div>
              <dt>Rotação</dt>
              <dd>
                {formatNumber(
                  selectedSpline.rotation
                )}°
              </dd>
            </div>
            <div>
              <dt>Comprimento</dt>
              <dd>
                {formatNumber(
                  selectedSpline.length
                )} m
              </dd>
            </div>
            <div>
              <dt>Raio</dt>
              <dd>
                {formatNumber(
                  selectedSpline.radius
                )} m
              </dd>
            </div>
            <div>
              <dt>Gradiente inicial</dt>
              <dd>
                {formatNumber(
                  selectedSpline.gradientStart
                )}%
              </dd>
            </div>
            <div>
              <dt>Gradiente final</dt>
              <dd>
                {formatNumber(
                  selectedSpline.gradientEnd
                )}%
              </dd>
            </div>
          </dl>
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
                    (surface, index) => (
                      <div
                        className="mesh-row"
                        key={`${surface.textureIndex}-${index}`}
                      >
                        <strong>
                          {surface.textureName ??
                            `Material ${surface.textureIndex}`}
                        </strong>
                        <span>
                          {formatNumber(
                            surface.from.x
                          )} →{" "}
                          {formatNumber(
                            surface.to.x
                          )} m
                        </span>
                      </div>
                    )
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
            disabled={!selectedObject}
            title="Mover objeto em prévia (W)"
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
            disabled={!selectedObject}
            title="Rotacionar objeto em prévia (E)"
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
              previewEditCount === 0
            }
            onClick={
              handleDiscardPreviewEdits
            }
          >
            ✕
          </button>

          <span className="toolbar-separator" />

          <span className="toolbar-chip">
            Global
          </span>
          <span className="toolbar-chip">
            Q/W/E · 1/2 · N · F · Ctrl+Z/Y/S
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
              handleSavePreviewEdits
            }
            disabled={
              previewEditCount === 0 ||
              busy
            }
            title="Salvar transformações com backup automático (Ctrl+S)"
          >
            {saving
              ? "Salvando..."
              : `Salvar${previewEditCount > 0 ? ` (${previewEditCount})` : ""}`}
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
                disabled
              >
                Favoritos
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
  
                {mapLoadMode === "full" && (
                  <div className="tree-node">
                    <span>◈</span>
                    Modelos O3D
                    <strong>
                      {loadedMapGeometryCount}/
                      {mapObjectPaths.length}
                    </strong>
                  </div>
                )}
  
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
                  Tiles totais
                  <strong>
                    {selectedMap.tiles.length}
                  </strong>
                </div>
              </div>
  
              <div className="explorer-search">
                <input
                  type="search"
                  placeholder="Buscar objeto, ID ou tile..."
                  value={explorerSearch}
                  onChange={(event) =>
                    setExplorerSearch(
                      event.target.value
                    )
                  }
                />
                <span>
                  {explorerObjectResultCount}
                  {" "}resultado(s)
                  {explorerObjectResultCount > 250
                    ? " · mostrando 250"
                    : ""}
                </span>
              </div>
  
              <div className="explorer-object-list">
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
  
              </>
            ) : (
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
            )}
            </div>
          </aside>

          <section className="editor-viewport">
            <Viewport
              tiles={activeTiles}
              objects={
                objectsForViewport
              }
              splines={splines}
              editorTool={editorTool}
              snapEnabled={snapEnabled}
              moveSnap={moveSnap}
              rotationSnap={
                rotationSnap
              }
              showGrid={showGrid}
              showObjects={showObjects}
              showSplines={showSplines}
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
            />

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
                  Prévia não salva ·{" "}
                  {previewEditCount}
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
              <label className="muted">
                <input
                  type="checkbox"
                  disabled
                />
                Terreno
              </label>
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
                  ? `Carregando modelos O3D ${loadedMapGeometryCount}/${mapObjectPaths.length}...`
                  : loadingSplineFor
                    ? "Lendo SLI..."
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
              : "sob demanda"}
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
