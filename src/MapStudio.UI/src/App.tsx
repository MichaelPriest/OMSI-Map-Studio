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
  getSkyTextureAssetKey,
  getTerrainTextureMaskAssetKey,
  getSceneryTextureAssetKey,
  getSplineTextureAssetKey,
  insertObject,
  insertObjectBatch,
  insertObjectMultiBatch,
  insertSpline,
  insertSplineFromLibrary,
  applyTerrainElevationGrid,
  createCoordinateMap,
  isDesktopBridgeAvailable,
  levelTerrain,
  loadGoogleElevationGrid,
  loadGoogleMapReference,
  loadMapCatalog,
  loadMapFull,
  loadMapRegion,
  loadGroundTextureAsset,
  loadSkyTextureAsset,
  loadTerrainTextureMaskAsset,
  loadSceneryLibrary,
  loadSceneryTextureAsset,
  loadSplineLibrary,
  loadSplineTextureAsset,
  loadSplineProfile,
  loadSceneryObjectGeometry,
  loadSceneryObjectMetadata,
  replaceMapAssetPath,
  restoreMapStudioBackup,
  saveMapGeoreference,
  saveObjectTransforms,
  saveSplineTransforms,
  sceneryTreeTextureMeshToken,
  openMapFromCatalog,
  selectMap,
  selectOmsiRoot,
  setFullScreen,
  subscribeToHost,
  updateSplineLinks,
  type GoogleElevationGrid,
  type GoogleMapReference,
  type OmsiMapCatalogEntry,
  type SceneryLibraryEntry,
  type SplineLibraryEntry,
  type OmsiMap,
  type OmsiPlacedObject,
  type OmsiPlacedSpline,
  type OmsiSplineDefinition,
  type OmsiSceneryObjectGeometry,
  type OmsiSceneryObjectMetadata,
  type OmsiTextureAsset,
  type OmsiTile
} from "./bridge/desktopBridge";
import { AssetPreview3D } from "./editor/AssetPreview3D";
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

type SelectionMode =
  | "all"
  | "object"
  | "spline"
  | "terrain";

type QuickCreateTool =
  | "road"
  | "bridge"
  | "junction"
  | "building"
  | "transit"
  | "street"
  | "utilities"
  | "object"
  | "terrain"
  | "water"
  | "grass"
  | "tree";

type SceneryLibraryGroup =
  | "all"
  | "junctions"
  | "bridges"
  | "buildings"
  | "vegetation"
  | "transit"
  | "street"
  | "utilities"
  | "other";

type SplineLibraryGroup =
  | "all"
  | "roads"
  | "paths"
  | "rail"
  | "bridges"
  | "markings"
  | "other";

type LibraryViewMode =
  | "groups"
  | "favorites"
  | "recent"
  | "frequent"
  | "collection";

type SceneryTechnicalFilter =
  | "all"
  | "used"
  | "tree"
  | "loaded"
  | "problems";

type SplineTechnicalFilter =
  | "all"
  | "used"
  | "loaded"
  | "problems";

const libraryStorageKeys = {
  sceneryFavorites:
    "omsi-map-studio:library:scenery-favorites",
  sceneryRecent:
    "omsi-map-studio:library:scenery-recent",
  sceneryUsage:
    "omsi-map-studio:library:scenery-usage",
  splineFavorites:
    "omsi-map-studio:library:spline-favorites",
  splineRecent:
    "omsi-map-studio:library:spline-recent",
  splineUsage:
    "omsi-map-studio:library:spline-usage",
  collections:
    "omsi-map-studio:library:collections",
  thumbnails:
    "omsi-map-studio:library:thumbnails-v1",
  constructionSets:
    "omsi-map-studio:construction-sets-v1"
} as const;

const readStoredJson = <T,>(
  key: string,
  fallback: T
): T => {
  try {
    const raw =
      window.localStorage.getItem(key);

    return raw
      ? (JSON.parse(raw) as T)
      : fallback;
  } catch {
    return fallback;
  }
};

const writeStoredJson = (
  key: string,
  value: unknown
) => {
  try {
    window.localStorage.setItem(
      key,
      JSON.stringify(value)
    );
  } catch {
    // Library personalization must never block
    // real map editing if browser storage is full.
  }
};

const assetSearchSynonyms:
  Record<string, string[]> = {
    rua: [
      "rua",
      "road",
      "street",
      "strasse",
      "straße"
    ],
    avenida: [
      "avenida",
      "avenue",
      "allee",
      "boulevard"
    ],
    arvore: [
      "arvore",
      "árvore",
      "tree",
      "baum"
    ],
    casa: [
      "casa",
      "house",
      "haus",
      "wohn"
    ],
    predio: [
      "predio",
      "prédio",
      "building",
      "gebaeude",
      "gebäude"
    ],
    ponte: [
      "ponte",
      "bridge",
      "bruecke",
      "brücke"
    ],
    cruzamento: [
      "cruzamento",
      "junction",
      "intersection",
      "kreuzung"
    ],
    calcada: [
      "calcada",
      "calçada",
      "sidewalk",
      "gehweg"
    ],
    trilho: [
      "trilho",
      "rail",
      "track",
      "gleis",
      "tram"
    ],
    poste: [
      "poste",
      "pole",
      "lamp",
      "light"
    ],
    ponto: [
      "ponto",
      "busstop",
      "bus stop",
      "haltestelle"
    ]
  };

const matchesSmartAssetSearch = (
  rawText: string,
  rawQuery: string
) => {
  const text =
    normalizeAssetClassifierText(
      rawText
    );
  const tokens =
    normalizeAssetClassifierText(
      rawQuery
    )
      .split(/\s+/)
      .filter(Boolean);

  return tokens.every((token) => {
    const alternatives =
      assetSearchSynonyms[token] ??
      [token];

    return alternatives.some(
      (candidate) =>
        text.includes(
          normalizeAssetClassifierText(
            candidate
          )
        )
    );
  });
};

const sceneryLibraryGroups: Array<{
  id: SceneryLibraryGroup;
  label: string;
  icon: string;
}> = [
  { id: "all", label: "Todos", icon: "▦" },
  { id: "junctions", label: "Cruzamentos", icon: "✣" },
  { id: "bridges", label: "Pontes", icon: "⌁" },
  { id: "buildings", label: "Casas / prédios", icon: "⌂" },
  { id: "vegetation", label: "Árvores / verde", icon: "♣" },
  { id: "transit", label: "Transporte", icon: "▤" },
  { id: "street", label: "Mobiliário", icon: "⚑" },
  { id: "utilities", label: "Infraestrutura", icon: "⚙" },
  { id: "other", label: "Outros", icon: "◇" }
];

const splineLibraryGroups: Array<{
  id: SplineLibraryGroup;
  label: string;
  icon: string;
}> = [
  { id: "all", label: "Todas", icon: "▦" },
  { id: "roads", label: "Ruas", icon: "═" },
  { id: "paths", label: "Calçadas / caminhos", icon: "┄" },
  { id: "rail", label: "Trilhos", icon: "≋" },
  { id: "bridges", label: "Pontes / túneis", icon: "⌁" },
  { id: "markings", label: "Faixas / marcas", icon: "⋯" },
  { id: "other", label: "Outras", icon: "◇" }
];

const normalizeAssetClassifierText = (
  value: string
) =>
  value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\\/g, "/")
    .toLocaleLowerCase("en-US");

const containsAnyAssetTerm = (
  text: string,
  terms: string[]
) =>
  terms.some((term) =>
    text.includes(term)
  );

const getSceneryLibraryGroup = (
  entry: SceneryLibraryEntry,
  metadata:
    | OmsiSceneryObjectMetadata
    | undefined,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined
): Exclude<SceneryLibraryGroup, "all"> => {
  if (geometry?.tree) {
    return "vegetation";
  }

  const text =
    normalizeAssetClassifierText(
      [
        entry.fileName,
        entry.sceneryObjectPath,
        metadata?.friendlyName ?? ""
      ].join(" ")
    );

  if (
    containsAnyAssetTerm(text, [
      "junction",
      "intersection",
      "kreuzung",
      "crossing",
      "cruzamento",
      "rotatoria",
      "roundabout"
    ])
  ) {
    return "junctions";
  }

  if (
    containsAnyAssetTerm(text, [
      "bridge",
      "bruecke",
      "brucke",
      "ponte",
      "viaduct",
      "viaduto",
      "overpass",
      "elevated"
    ])
  ) {
    return "bridges";
  }

  if (
    containsAnyAssetTerm(text, [
      "tree",
      "baum",
      "bush",
      "shrub",
      "hedge",
      "grass",
      "vegetation",
      "flora",
      "arvore",
      "arbusto"
    ])
  ) {
    return "vegetation";
  }

  if (
    containsAnyAssetTerm(text, [
      "busstop",
      "bus stop",
      "haltestelle",
      "terminal",
      "bahnhof",
      "station",
      "shelter",
      "depot",
      "garage"
    ])
  ) {
    return "transit";
  }

  if (
    containsAnyAssetTerm(text, [
      "house",
      "haus",
      "building",
      "gebaeude",
      "wohn",
      "apartment",
      "shop",
      "store",
      "factory",
      "warehouse",
      "school",
      "hospital",
      "igreja",
      "church",
      "predio",
      "casa"
    ])
  ) {
    return "buildings";
  }

  if (
    containsAnyAssetTerm(text, [
      "lamp",
      "light",
      "bench",
      "bank",
      "bin",
      "trash",
      "sign",
      "schild",
      "traffic",
      "fence",
      "zaun",
      "bollard",
      "pole",
      "poste",
      "placa",
      "semaforo"
    ])
  ) {
    return "street";
  }

  if (
    containsAnyAssetTerm(text, [
      "power",
      "utility",
      "substation",
      "transformer",
      "water",
      "wasser",
      "sewer",
      "gas",
      "pipeline",
      "tower",
      "antenna",
      "infra"
    ])
  ) {
    return "utilities";
  }

  return "other";
};

const getSplineLibraryGroup = (
  entry: SplineLibraryEntry
): Exclude<SplineLibraryGroup, "all"> => {
  const text =
    normalizeAssetClassifierText(
      entry.fileName +
        " " +
        entry.splinePath
    );

  if (
    containsAnyAssetTerm(text, [
      "bridge",
      "bruecke",
      "brucke",
      "ponte",
      "viaduct",
      "viaduto",
      "tunnel",
      "elevated"
    ])
  ) {
    return "bridges";
  }

  if (
    containsAnyAssetTerm(text, [
      "rail",
      "track",
      "gleis",
      "tram",
      "strab",
      "bahn",
      "metro"
    ])
  ) {
    return "rail";
  }

  if (
    containsAnyAssetTerm(text, [
      "sidewalk",
      "foot",
      "path",
      "walk",
      "cycle",
      "bike",
      "radweg",
      "gehweg",
      "calcada",
      "caminho"
    ])
  ) {
    return "paths";
  }

  if (
    containsAnyAssetTerm(text, [
      "mark",
      "line",
      "stripe",
      "lane",
      "roadmark",
      "fahrbahnmark",
      "faixa"
    ])
  ) {
    return "markings";
  }

  if (
    containsAnyAssetTerm(text, [
      "road",
      "street",
      "strasse",
      "strabe",
      "asphalt",
      "avenue",
      "allee",
      "rua",
      "avenida"
    ])
  ) {
    return "roads";
  }

  return "other";
};

const getSceneryLibrarySubcategory = (
  entry: SceneryLibraryEntry,
  group: Exclude<
    SceneryLibraryGroup,
    "all"
  >
) => {
  const text =
    normalizeAssetClassifierText(
      entry.fileName +
        " " +
        entry.sceneryObjectPath
    );

  if (group === "junctions") {
    return containsAnyAssetTerm(
      text,
      ["roundabout", "rotatoria"]
    )
      ? "Rotatórias"
      : "Interseções";
  }

  if (group === "bridges") {
    if (
      containsAnyAssetTerm(text, [
        "viaduct",
        "viaduto",
        "overpass",
        "elevated"
      ])
    ) {
      return "Viadutos / elevados";
    }
    return "Pontes";
  }

  if (group === "buildings") {
    if (
      containsAnyAssetTerm(text, [
        "shop",
        "store",
        "commercial",
        "laden"
      ])
    ) {
      return "Comercial";
    }
    if (
      containsAnyAssetTerm(text, [
        "factory",
        "industrial",
        "warehouse"
      ])
    ) {
      return "Industrial";
    }
    if (
      containsAnyAssetTerm(text, [
        "school",
        "hospital",
        "church",
        "igreja",
        "public"
      ])
    ) {
      return "Público";
    }
    if (
      containsAnyAssetTerm(text, [
        "house",
        "haus",
        "wohn",
        "apartment",
        "casa"
      ])
    ) {
      return "Residencial";
    }
    return "Edificações";
  }

  if (group === "vegetation") {
    if (
      containsAnyAssetTerm(text, [
        "grass",
        "grama"
      ])
    ) {
      return "Grama";
    }
    if (
      containsAnyAssetTerm(text, [
        "bush",
        "shrub",
        "hedge",
        "arbusto"
      ])
    ) {
      return "Arbustos";
    }
    return "Árvores";
  }

  if (group === "transit") {
    if (
      containsAnyAssetTerm(text, [
        "depot",
        "garage"
      ])
    ) {
      return "Garagens / depósitos";
    }
    if (
      containsAnyAssetTerm(text, [
        "terminal",
        "station",
        "bahnhof"
      ])
    ) {
      return "Terminais / estações";
    }
    return "Pontos / abrigos";
  }

  if (group === "street") {
    if (
      containsAnyAssetTerm(text, [
        "lamp",
        "light",
        "poste"
      ])
    ) {
      return "Iluminação";
    }
    if (
      containsAnyAssetTerm(text, [
        "sign",
        "schild",
        "traffic",
        "placa",
        "semaforo"
      ])
    ) {
      return "Sinalização";
    }
    if (
      containsAnyAssetTerm(text, [
        "fence",
        "zaun",
        "barrier",
        "bollard"
      ])
    ) {
      return "Cercas / barreiras";
    }
    return "Mobiliário urbano";
  }

  if (group === "utilities") {
    if (
      containsAnyAssetTerm(text, [
        "power",
        "transformer",
        "substation"
      ])
    ) {
      return "Energia";
    }
    if (
      containsAnyAssetTerm(text, [
        "water",
        "wasser",
        "sewer"
      ])
    ) {
      return "Água / saneamento";
    }
    return "Infraestrutura";
  }

  return "Geral";
};

const getSplineLibrarySubcategory = (
  entry: SplineLibraryEntry,
  group: Exclude<
    SplineLibraryGroup,
    "all"
  >
) => {
  const text =
    normalizeAssetClassifierText(
      entry.fileName +
        " " +
        entry.splinePath
    );

  if (group === "roads") {
    if (
      containsAnyAssetTerm(text, [
        "oneway",
        "one way",
        "einbahn"
      ])
    ) {
      return "Mão única";
    }
    if (
      containsAnyAssetTerm(text, [
        "avenue",
        "allee",
        "multi",
        "4lane",
        "6lane"
      ])
    ) {
      return "Avenidas";
    }
    if (
      containsAnyAssetTerm(text, [
        "country",
        "rural",
        "landstr",
        "highway"
      ])
    ) {
      return "Estradas";
    }
    return "Ruas urbanas";
  }

  if (group === "paths") {
    return containsAnyAssetTerm(
      text,
      ["cycle", "bike", "radweg"]
    )
      ? "Ciclovias"
      : "Calçadas / caminhos";
  }

  if (group === "rail") {
    return containsAnyAssetTerm(
      text,
      ["tram", "strab"]
    )
      ? "Bonde / tram"
      : "Ferrovia";
  }

  if (group === "bridges") {
    return containsAnyAssetTerm(
      text,
      ["tunnel"]
    )
      ? "Túneis"
      : "Pontes / elevados";
  }

  if (group === "markings") {
    return "Marcação viária";
  }

  return "Geral";
};

type ConstructionHistoryEntry = {
  label: string;
  backupDirectory: string;
};

type DependencyReplacementTarget = {
  kind: "object" | "spline";
  path: string;
};

type ViewportCameraAction = {
  type:
    | "fit"
    | "focus"
    | "perspective"
    | "top"
    | "tile";
  token: number;
  tileX?: number;
  tileY?: number;
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

type ObjectPlacementMode =
  | "single"
  | "repeat"
  | "line"
  | "area"
  | "matrix"
  | "circle"
  | "lot";

type ConstructionPresetId =
  | "none"
  | "avenueTrees"
  | "streetLights"
  | "housingLots"
  | "greenSquare"
  | "parkingGrid";

type ConstructionSetSide =
  | "left"
  | "right"
  | "both";

type ConstructionSetCompanion = {
  id: string;
  sceneryObjectPath: string;
  spacing: number;
  lateralOffset: number;
  side: ConstructionSetSide;
  rotationOffset: number;
};

type ConstructionSetDefinition = {
  id: string;
  name: string;
  splinePath: string | null;
  companions:
    ConstructionSetCompanion[];
};

type PlacementTransformDefaults = Pick<
  PendingObjectPlacement,
  "z" | "rotation" | "pitch" | "bank"
>;

const placementWorldPoint = (
  placement: Pick<
    PendingObjectPlacement,
    "tileX" | "tileY" | "x" | "y"
  >
) => ({
  x:
    placement.tileX * 300 +
    placement.x,
  y:
    placement.tileY * 300 +
    placement.y
});

const placementFromWorldPoint = (
  worldX: number,
  worldY: number,
  transform:
    PlacementTransformDefaults
): PendingObjectPlacement => {
  const tileX =
    Math.floor(worldX / 300);
  const tileY =
    Math.floor(worldY / 300);

  return {
    tileX,
    tileY,
    x: worldX - tileX * 300,
    y: worldY - tileY * 300,
    ...transform
  };
};

const getSplineAxisEnd = (
  spline: OmsiPlacedSpline
) => {
  const startX =
    spline.tileX * 300 +
    spline.x;
  const startY =
    spline.tileY * 300 +
    spline.y;
  const heading =
    spline.rotation *
    Math.PI /
    180;

  if (
    Math.abs(spline.radius) <
      0.001 ||
    Math.abs(spline.length) <
      0.001
  ) {
    return {
      x:
        startX +
        Math.sin(heading) *
          spline.length,
      y:
        startY +
        Math.cos(heading) *
          spline.length
    };
  }

  const turn =
    spline.length /
    spline.radius;
  const endHeading =
    heading + turn;

  return {
    x:
      startX +
      spline.radius *
        (
          Math.cos(heading) -
          Math.cos(endHeading)
        ),
    y:
      startY +
      spline.radius *
        (
          Math.sin(endHeading) -
          Math.sin(heading)
        )
  };
};

type JunctionSuggestion = {
  key: string;
  tileX: number;
  tileY: number;
  x: number;
  y: number;
  rotation: number;
  splineA: number;
  splineB: number;
};

const getSplineAxisPoint = (
  spline: OmsiPlacedSpline,
  progress: number
) => {
  const t =
    Math.max(
      0,
      Math.min(1, progress)
    );
  const startX =
    spline.tileX * 300 +
    spline.x;
  const startY =
    spline.tileY * 300 +
    spline.y;
  const heading =
    spline.rotation *
    Math.PI /
    180;

  if (
    Math.abs(spline.radius) <
      0.001
  ) {
    return {
      x:
        startX +
        Math.sin(heading) *
          spline.length *
          t,
      y:
        startY +
        Math.cos(heading) *
          spline.length *
          t,
      heading
    };
  }

  const turn =
    (
      spline.length /
      spline.radius
    ) *
    t;
  const localHeading =
    heading + turn;

  return {
    x:
      startX +
      spline.radius *
        (
          Math.cos(heading) -
          Math.cos(localHeading)
        ),
    y:
      startY +
      spline.radius *
        (
          Math.sin(localHeading) -
          Math.sin(heading)
        ),
    heading: localHeading
  };
};

const sampleSplineAxis = (
  spline: OmsiPlacedSpline
) => {
  const curveRadians =
    Math.abs(spline.radius) >
      0.001
      ? Math.abs(
          spline.length /
            spline.radius
        )
      : 0;
  const segmentCount =
    Math.max(
      1,
      Math.min(
        24,
        Math.ceil(
          curveRadians * 6
        )
      )
    );

  return Array.from(
    { length: segmentCount + 1 },
    (_, index) => ({
      ...getSplineAxisPoint(
        spline,
        index / segmentCount
      ),
      progress:
        index / segmentCount
    })
  );
};

const segmentIntersection = (
  a0: { x: number; y: number },
  a1: { x: number; y: number },
  b0: { x: number; y: number },
  b1: { x: number; y: number }
) => {
  const ax = a1.x - a0.x;
  const ay = a1.y - a0.y;
  const bx = b1.x - b0.x;
  const by = b1.y - b0.y;
  const denominator =
    ax * by - ay * bx;

  if (
    Math.abs(denominator) <
      0.0001
  ) {
    return undefined;
  }

  const dx = b0.x - a0.x;
  const dy = b0.y - a0.y;
  const ta =
    (
      dx * by -
      dy * bx
    ) /
    denominator;
  const tb =
    (
      dx * ay -
      dy * ax
    ) /
    denominator;

  if (
    ta < -0.001 ||
    ta > 1.001 ||
    tb < -0.001 ||
    tb > 1.001
  ) {
    return undefined;
  }

  return {
    x: a0.x + ax * ta,
    y: a0.y + ay * ta,
    ta,
    tb,
    angleA:
      Math.atan2(ax, ay),
    angleB:
      Math.atan2(bx, by)
  };
};

const findJunctionSuggestions = (
  splines: OmsiPlacedSpline[]
) => {
  const candidates =
    splines
      .filter(
        (spline) =>
          !spline.isHeightSpline &&
          spline.length > 1
      )
      .slice(0, 400)
      .map((spline) => ({
        spline,
        points:
          sampleSplineAxis(spline)
      }));
  const result:
    JunctionSuggestion[] = [];

  for (
    let aIndex = 0;
    aIndex < candidates.length;
    aIndex++
  ) {
    const a = candidates[aIndex];

    for (
      let bIndex = aIndex + 1;
      bIndex < candidates.length;
      bIndex++
    ) {
      const b = candidates[bIndex];

      if (
        a.spline.splineId ===
        b.spline.splineId
      ) {
        continue;
      }

      for (
        let ai = 0;
        ai < a.points.length - 1;
        ai++
      ) {
        for (
          let bi = 0;
          bi < b.points.length - 1;
          bi++
        ) {
          const hit =
            segmentIntersection(
              a.points[ai],
              a.points[ai + 1],
              b.points[bi],
              b.points[bi + 1]
            );

          if (!hit) {
            continue;
          }

          const angleDifference =
            Math.abs(
              Math.atan2(
                Math.sin(
                  hit.angleA -
                    hit.angleB
                ),
                Math.cos(
                  hit.angleA -
                    hit.angleB
                )
              )
            );

          const acuteAngle =
            Math.min(
              angleDifference,
              Math.PI -
                angleDifference
            );

          if (
            acuteAngle <
              15 *
                Math.PI /
                180
          ) {
            continue;
          }

          const progressA =
            a.points[ai].progress +
            (
              a.points[ai + 1]
                .progress -
              a.points[ai]
                .progress
            ) *
              hit.ta;
          const progressB =
            b.points[bi].progress +
            (
              b.points[bi + 1]
                .progress -
              b.points[bi]
                .progress
            ) *
              hit.tb;

          const interiorA =
            progressA > 0.03 &&
            progressA < 0.97;
          const interiorB =
            progressB > 0.03 &&
            progressB < 0.97;

          if (
            !interiorA &&
            !interiorB
          ) {
            continue;
          }

          if (
            result.some(
              (current) =>
                Math.hypot(
                  current.tileX *
                    300 +
                    current.x -
                    hit.x,
                  current.tileY *
                    300 +
                    current.y -
                    hit.y
                ) < 3
            )
          ) {
            continue;
          }

          const tileX =
            Math.floor(
              hit.x / 300
            );
          const tileY =
            Math.floor(
              hit.y / 300
            );

          result.push({
            key:
              a.spline.splineId +
              ":" +
              b.spline.splineId +
              ":" +
              Math.round(hit.x) +
              ":" +
              Math.round(hit.y),
            tileX,
            tileY,
            x:
              hit.x -
              tileX * 300,
            y:
              hit.y -
              tileY * 300,
            rotation:
              hit.angleA *
              180 /
              Math.PI,
            splineA:
              a.spline.splineId,
            splineB:
              b.spline.splineId
          });

          if (
            result.length >= 64
          ) {
            return result;
          }
        }
      }
    }
  }

  return result;
};

const buildConstructionSetObjectGroups = (
  spline: OmsiPlacedSpline,
  set: ConstructionSetDefinition
) => {
  const groups:
    Array<{
      sceneryObjectPath: string;
      placements:
        PendingObjectPlacement[];
    }> = [];
  let remainingTotal = 512;

  for (const companion of
    set.companions.slice(0, 16)) {
    if (remainingTotal <= 0) {
      break;
    }

    const spacing =
      Math.max(
        1,
        companion.spacing
      );
    const count =
      Math.max(
        1,
        Math.min(
          256,
          Math.floor(
            spline.length /
            spacing
          ) +
            1
        )
      );
    const placements:
      PendingObjectPlacement[] = [];

    for (
      let index = 0;
      index < count &&
      placements.length < 256 &&
      remainingTotal > 0;
      index++
    ) {
      const progress =
        count <= 1
          ? 0
          : index /
            (count - 1);
      const axis =
        getSplineAxisPoint(
          spline,
          progress
        );
      const distanceAlong =
        spline.length *
        progress;
      const gradientDelta =
        spline.gradientEnd -
        spline.gradientStart;
      const z =
        spline.z +
        (
          spline.gradientStart *
            distanceAlong +
          0.5 *
            gradientDelta *
            spline.length *
            progress *
            progress
        ) /
          100;
      const headingDegrees =
        axis.heading *
          180 /
          Math.PI +
        companion.rotationOffset;
      const leftX =
        -Math.cos(axis.heading);
      const leftY =
        Math.sin(axis.heading);

      const sides =
        companion.side === "both"
          ? [-1, 1]
          : companion.side === "left"
            ? [1]
            : [-1];

      for (const sideSign of sides) {
        if (
          placements.length >= 256 ||
          remainingTotal <= 0
        ) {
          break;
        }

        const offset =
          companion.lateralOffset *
          sideSign;

        placements.push(
          placementFromWorldPoint(
            axis.x +
              leftX * offset,
            axis.y +
              leftY * offset,
            {
              z,
              rotation:
                headingDegrees,
              pitch: 0,
              bank: 0
            }
          )
        );
        remainingTotal -= 1;
      }
    }

    if (placements.length > 0) {
      groups.push({
        sceneryObjectPath:
          companion
            .sceneryObjectPath,
        placements
      });
    }
  }

  return groups;
};

const snapPlacementToNearestRoad = (
  placement: PendingObjectPlacement,
  splines: OmsiPlacedSpline[],
  maximumDistance: number
) => {
  const point =
    placementWorldPoint(placement);
  let best:
    | {
        distance: number;
        x: number;
        y: number;
        rotation: number;
      }
    | undefined;

  for (const spline of splines) {
    if (
      spline.isHeightSpline ||
      spline.length <= 0
    ) {
      continue;
    }

    const startX =
      spline.tileX * 300 +
      spline.x;
    const startY =
      spline.tileY * 300 +
      spline.y;
    const end =
      getSplineAxisEnd(spline);
    const dx = end.x - startX;
    const dy = end.y - startY;
    const lengthSquared =
      dx * dx + dy * dy;

    if (lengthSquared <= 0.001) {
      continue;
    }

    const t =
      Math.max(
        0,
        Math.min(
          1,
          (
            (
              point.x - startX
            ) *
              dx +
            (
              point.y - startY
            ) *
              dy
          ) /
            lengthSquared
        )
      );
    const x = startX + dx * t;
    const y = startY + dy * t;
    const distance =
      Math.hypot(
        point.x - x,
        point.y - y
      );

    if (
      distance >
        maximumDistance ||
      (
        best &&
        distance >= best.distance
      )
    ) {
      continue;
    }

    const turn =
      Math.abs(spline.radius) >
        0.001
        ? (
            spline.length /
            spline.radius
          ) *
          t
        : 0;
    best = {
      distance,
      x,
      y,
      rotation:
        spline.rotation +
        turn *
          180 /
          Math.PI
    };
  }

  return best
    ? placementFromWorldPoint(
        best.x,
        best.y,
        {
          z: placement.z,
          rotation: best.rotation,
          pitch: placement.pitch,
          bank: placement.bank
        }
      )
    : placement;
};

const buildLinePlacements = (
  start: PendingObjectPlacement,
  end: PendingObjectPlacement,
  spacing: number,
  randomRotation: boolean
) => {
  const from =
    placementWorldPoint(start);
  const to =
    placementWorldPoint(end);
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const distance =
    Math.hypot(dx, dy);
  const safeSpacing =
    Math.max(0.5, spacing);
  const segments =
    Math.max(
      1,
      Math.ceil(
        distance /
        safeSpacing
      )
    );
  const heading =
    Math.atan2(dx, dy) *
    180 /
    Math.PI;

  return Array.from(
    {
      length:
        Math.min(
          256,
          segments + 1
        )
    },
    (_, index) => {
      const t =
        segments <= 0
          ? 0
          : index / segments;
      const rotation =
        randomRotation
          ? (
              heading +
              index * 137.507764
            ) %
            360
          : heading;

      return placementFromWorldPoint(
        from.x + dx * t,
        from.y + dy * t,
        {
          z: start.z,
          rotation,
          pitch: start.pitch,
          bank: start.bank
        }
      );
    }
  );
};

const buildAreaPlacements = (
  center: PendingObjectPlacement,
  radius: number,
  count: number,
  randomRotation: boolean
) => {
  const origin =
    placementWorldPoint(center);
  const safeRadius =
    Math.max(1, radius);
  const safeCount =
    Math.max(
      1,
      Math.min(
        256,
        Math.floor(count)
      )
    );

  return Array.from(
    { length: safeCount },
    (_, index) => {
      const progress =
        (index + 0.5) /
        safeCount;
      const radial =
        safeRadius *
        Math.sqrt(progress);
      const angle =
        index *
          2.399963229728653 +
        (
          (
            origin.x +
            origin.y
          ) %
          17
        ) *
          0.07;
      const rotation =
        randomRotation
          ? (
              angle *
              180 /
              Math.PI
            ) %
            360
          : center.rotation;

      return placementFromWorldPoint(
        origin.x +
          Math.cos(angle) *
            radial,
        origin.y +
          Math.sin(angle) *
            radial,
        {
          z: center.z,
          rotation,
          pitch: center.pitch,
          bank: center.bank
        }
      );
    }
  );
};

const buildMatrixPlacements = (
  center: PendingObjectPlacement,
  rows: number,
  columns: number,
  spacingX: number,
  spacingY: number,
  randomRotation: boolean
) => {
  const origin =
    placementWorldPoint(center);
  const safeRows =
    Math.max(
      1,
      Math.min(
        16,
        Math.floor(rows)
      )
    );
  const safeColumns =
    Math.max(
      1,
      Math.min(
        16,
        Math.floor(columns)
      )
    );
  const sx =
    Math.max(0.5, spacingX);
  const sy =
    Math.max(0.5, spacingY);
  const total =
    Math.min(
      256,
      safeRows * safeColumns
    );
  const placements:
    PendingObjectPlacement[] = [];

  for (
    let index = 0;
    index < total;
    index++
  ) {
    const row =
      Math.floor(
        index / safeColumns
      );
    const column =
      index % safeColumns;
    const offsetX =
      (
        column -
        (safeColumns - 1) / 2
      ) *
      sx;
    const offsetY =
      (
        row -
        (safeRows - 1) / 2
      ) *
      sy;

    placements.push(
      placementFromWorldPoint(
        origin.x + offsetX,
        origin.y + offsetY,
        {
          z: center.z,
          rotation:
            randomRotation
              ? (
                  center.rotation +
                  index *
                    137.507764
                ) %
                360
              : center.rotation,
          pitch: center.pitch,
          bank: center.bank
        }
      )
    );
  }

  return placements;
};

const buildCirclePlacements = (
  center: PendingObjectPlacement,
  radius: number,
  count: number,
  tangentRotation: boolean,
  randomRotation: boolean
) => {
  const origin =
    placementWorldPoint(center);
  const safeRadius =
    Math.max(0.5, radius);
  const safeCount =
    Math.max(
      1,
      Math.min(
        256,
        Math.floor(count)
      )
    );

  return Array.from(
    { length: safeCount },
    (_, index) => {
      const angle =
        (
          index /
          safeCount
        ) *
        Math.PI *
        2;
      const baseRotation =
        tangentRotation
          ? (
              angle *
              180 /
              Math.PI +
              90
            ) %
            360
          : center.rotation;

      return placementFromWorldPoint(
        origin.x +
          Math.cos(angle) *
            safeRadius,
        origin.y +
          Math.sin(angle) *
            safeRadius,
        {
          z: center.z,
          rotation:
            randomRotation
              ? (
                  baseRotation +
                  index *
                    137.507764
                ) %
                360
              : baseRotation,
          pitch: center.pitch,
          bank: center.bank
        }
      );
    }
  );
};

const buildLotPlacements = (
  start: PendingObjectPlacement,
  end: PendingObjectPlacement,
  spacing: number,
  setback: number,
  randomRotation: boolean
) => {
  const from =
    placementWorldPoint(start);
  const to =
    placementWorldPoint(end);
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const distance =
    Math.hypot(dx, dy);

  if (distance < 0.5) {
    return [start];
  }

  const count =
    Math.max(
      1,
      Math.min(
        256,
        Math.floor(
          distance /
          Math.max(1, spacing)
        ) +
          1
      )
    );
  const normalX =
    -dy / distance;
  const normalY =
    dx / distance;
  const heading =
    Math.atan2(dx, dy) *
    180 /
    Math.PI;

  return Array.from(
    { length: count },
    (_, index) => {
      const t =
        count === 1
          ? 0
          : index /
            (count - 1);

      return placementFromWorldPoint(
        from.x +
          dx * t +
          normalX *
            setback,
        from.y +
          dy * t +
          normalY *
            setback,
        {
          z: start.z,
          rotation:
            randomRotation
              ? (
                  heading +
                  index *
                    137.507764
                ) %
                360
              : heading,
          pitch: start.pitch,
          bank: start.bank
        }
      );
    }
  );
};

const constructionPresets: Array<{
  id: ConstructionPresetId;
  label: string;
  mode?: ObjectPlacementMode;
  spacing?: number;
  brushRadius?: number;
  brushCount?: number;
  matrixRows?: number;
  matrixColumns?: number;
  matrixSpacingX?: number;
  matrixSpacingY?: number;
  lotSetback?: number;
  randomRotation?: boolean;
  alignRoad?: boolean;
}> = [
  {
    id: "none",
    label: "Personalizado"
  },
  {
    id: "avenueTrees",
    label: "Arborização de avenida",
    mode: "line",
    spacing: 8,
    randomRotation: true
  },
  {
    id: "streetLights",
    label: "Postes de iluminação",
    mode: "line",
    spacing: 25,
    alignRoad: true
  },
  {
    id: "housingLots",
    label: "Casas alinhadas",
    mode: "lot",
    spacing: 18,
    lotSetback: 7
  },
  {
    id: "greenSquare",
    label: "Praça arborizada",
    mode: "area",
    brushRadius: 25,
    brushCount: 30,
    randomRotation: true
  },
  {
    id: "parkingGrid",
    label: "Grade / estacionamento",
    mode: "matrix",
    matrixRows: 5,
    matrixColumns: 8,
    matrixSpacingX: 3,
    matrixSpacingY: 6
  }
];

const defaultPlacementTransform:
  PlacementTransformDefaults = {
    z: 0,
    rotation: 0,
    pitch: 0,
    bank: 0
  };

const performanceObjectTextureLimit = 128;
const performanceSplineTextureLimit = 96;
const fullMapObjectTextureLimit = 1024;
const fullMapSplineTextureLimit = 256;
const performanceObjectTextureBatch = 12;
const performanceSplineTextureBatch = 8;
const fullMapObjectTextureBatch = 24;
const fullMapSplineTextureBatch = 12;
const geometryPreloadBatchSize = 12;
const splineProfilePreloadBatchSize = 12;

const maxTextureCacheEntries = 1536;
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
  invalidCoordinateMapRequest:
    "Revise o nome da pasta, nome do mapa e coordenadas antes de criar o projeto.",
  newMapTemplateMissing:
    "A instalação do OMSI não possui template\\NewMap, necessário para criar um mapa compatível sem inventar arquivos.",
  newMapTemplateInvalid:
    "O template NewMap da instalação não contém um global.cfg válido.",
  coordinateMapAlreadyExists:
    "Já existe um mapa com essa pasta em OMSI 2\\maps.",
  coordinateMapCreateError:
    "Não foi possível criar o mapa a partir do template NewMap.",
  invalidTerrainBrush:
    "Os parâmetros da ferramenta de nivelamento de terreno são inválidos.",
  terrainFileMissing:
    "Este tile não possui um arquivo .terrain editável.",
  terrainEditError:
    "Não foi possível nivelar o terreno com segurança. O arquivo original foi preservado.",
  invalidGoogleReferenceRequest:
    "Revise a chave, coordenadas, zoom e tipo de mapa da referência do Google.",
  googleMapsReferenceError:
    "Não foi possível carregar a referência do Google Maps/Elevation. Verifique a chave, APIs habilitadas, billing e conexão.",
  invalidGoogleElevationRequest:
    "Os dados usados para buscar a grade de elevação são inválidos.",
  googleElevationGridError:
    "Não foi possível carregar a grade de elevação do Google Elevation.",
  invalidElevationGrid:
    "A grade de elevação recebida não é válida para o terreno.",
  invalidMapGeoreference:
    "Os dados de coordenadas da referência do mapa são inválidos.",
  mapGeoreferenceSaveError:
    "Não foi possível salvar os metadados de georreferenciamento do Map Studio.",
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

const sampleTerrainHeight = (
  tiles: OmsiTile[],
  tileX: number,
  tileY: number,
  localX: number,
  localY: number
) => {
  const tile =
    tiles.find(
      (candidate) =>
        candidate.x === tileX &&
        candidate.y === tileY
    );

  const terrain = tile?.terrain;

  if (
    !terrain ||
    terrain.cellCount <= 0
  ) {
    return undefined;
  }

  const cellCount =
    terrain.cellCount;
  const sampleCount =
    cellCount + 1;

  if (
    terrain.heights.length !==
    sampleCount * sampleCount
  ) {
    return undefined;
  }

  const gridX =
    Math.min(
      cellCount,
      Math.max(
        0,
        (localX / 300) *
          cellCount
      )
    );

  const gridY =
    Math.min(
      cellCount,
      Math.max(
        0,
        (localY / 300) *
          cellCount
      )
    );

  const x0 = Math.floor(gridX);
  const y0 = Math.floor(gridY);
  const x1 =
    Math.min(
      cellCount,
      x0 + 1
    );
  const y1 =
    Math.min(
      cellCount,
      y0 + 1
    );

  const fx = gridX - x0;
  const fy = gridY - y0;

  const h00 =
    terrain.heights[
      y0 * sampleCount + x0
    ];
  const h10 =
    terrain.heights[
      y0 * sampleCount + x1
    ];
  const h01 =
    terrain.heights[
      y1 * sampleCount + x0
    ];
  const h11 =
    terrain.heights[
      y1 * sampleCount + x1
    ];

  const top =
    h00 +
    (h10 - h00) * fx;

  const bottom =
    h01 +
    (h11 - h01) * fx;

  return (
    top +
    (bottom - top) * fy
  );
};

type RoadPoint = {
  targetTileX: number;
  targetTileY: number;
  x: number;
  y: number;
};

type RoadEndpointSnap = {
  point: RoadPoint;
  splineId: number;
  endpoint: "start" | "end";
  distance: number;
  heading: number;
};

const findRoadEndpointSnap = (
  point: RoadPoint,
  splines: OmsiPlacedSpline[],
  maximumDistance: number
): RoadEndpointSnap | undefined => {
  const worldX =
    point.targetTileX * 300 +
    point.x;
  const worldY =
    point.targetTileY * 300 +
    point.y;
  let best:
    | RoadEndpointSnap
    | undefined;

  const consider = (
    spline: OmsiPlacedSpline,
    endpoint: "start" | "end",
    x: number,
    y: number,
    heading: number
  ) => {
    const distance =
      Math.hypot(
        worldX - x,
        worldY - y
      );

    if (
      distance > maximumDistance ||
      (
        best &&
        distance >= best.distance
      )
    ) {
      return;
    }

    const tileX =
      Math.floor(x / 300);
    const tileY =
      Math.floor(y / 300);

    best = {
      point: {
        targetTileX: tileX,
        targetTileY: tileY,
        x: x - tileX * 300,
        y: y - tileY * 300
      },
      splineId:
        spline.splineId,
      endpoint,
      distance,
      heading
    };
  };

  for (const spline of splines) {
    if (
      spline.isHeightSpline ||
      spline.length <= 0
    ) {
      continue;
    }

    const startX =
      spline.tileX * 300 +
      spline.x;
    const startY =
      spline.tileY * 300 +
      spline.y;
    const end =
      getSplineAxisEnd(spline);
    const startHeading =
      spline.rotation;
    const endHeading =
      spline.rotation +
      (
        Math.abs(spline.radius) >
          0.001
          ? (
              spline.length /
              spline.radius
            ) *
            180 /
            Math.PI
          : 0
      );

    consider(
      spline,
      "start",
      startX,
      startY,
      startHeading
    );
    consider(
      spline,
      "end",
      end.x,
      end.y,
      endHeading
    );
  }

  return best;
};

const deriveRoadArc = (
  start: RoadPoint,
  end: RoadPoint,
  curveOffset: number
) => {
  const startWorldX =
    start.targetTileX * 300 +
    start.x;
  const startWorldY =
    start.targetTileY * 300 +
    start.y;
  const endWorldX =
    end.targetTileX * 300 +
    end.x;
  const endWorldY =
    end.targetTileY * 300 +
    end.y;

  const deltaX =
    endWorldX - startWorldX;
  const deltaY =
    endWorldY - startWorldY;
  const chordLength =
    Math.hypot(deltaX, deltaY);

  if (chordLength < 0.001) {
    return undefined;
  }

  const chordBearing =
    Math.atan2(
      deltaX,
      deltaY
    );

  if (
    Math.abs(curveOffset) <
      0.05
  ) {
    return {
      rotation:
        chordBearing *
        180 /
        Math.PI,
      length: chordLength,
      radius: 0,
      chordLength
    };
  }

  const maximumOffset =
    Math.max(
      0.05,
      chordLength * 0.49
    );
  const sagitta =
    Math.max(
      -maximumOffset,
      Math.min(
        maximumOffset,
        curveOffset
      )
    );

  const radiusMagnitude =
    (
      chordLength *
      chordLength
    ) /
      (
        8 *
        Math.abs(sagitta)
      ) +
    Math.abs(sagitta) /
      2;

  const signedRadius =
    Math.sign(sagitta) *
    radiusMagnitude;

  const halfAngle =
    Math.asin(
      Math.min(
        1,
        chordLength /
          (
            2 *
            radiusMagnitude
          )
      )
    );

  const signedAngle =
    Math.sign(sagitta) *
    halfAngle *
    2;

  return {
    rotation:
      (
        chordBearing -
        signedAngle / 2
      ) *
      180 /
      Math.PI,
    length:
      radiusMagnitude *
      Math.abs(signedAngle),
    radius:
      signedRadius,
    chordLength
  };
};

const getSplineEndWorldPoint = (
  spline: {
    tileX: number;
    tileY: number;
    x: number;
    y: number;
    rotation: number;
    length: number;
    radius: number;
  }
) => {
  const yaw =
    spline.rotation *
    Math.PI /
    180;

  const hasCurve =
    Math.abs(spline.radius) >
    0.001;

  const angle =
    hasCurve
      ? spline.length /
        spline.radius
      : 0;

  const localX =
    hasCurve
      ? spline.radius *
        (
          1 -
          Math.cos(angle)
        )
      : 0;

  const localY =
    hasCurve
      ? spline.radius *
        Math.sin(angle)
      : spline.length;

  const cosYaw =
    Math.cos(yaw);
  const sinYaw =
    Math.sin(yaw);

  return {
    x:
      spline.tileX * 300 +
      spline.x +
      localX * cosYaw +
      localY * sinYaw,
    y:
      spline.tileY * 300 +
      spline.y -
      localX * sinYaw +
      localY * cosYaw
  };
};

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


const getStaticTransparencyMapName = (
  value: string | null | undefined
) => {
  const normalized = value?.trim();

  if (
    !normalized ||
    normalized.startsWith("\\S:")
  ) {
    return undefined;
  }

  return normalized;
};
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

  const occurrenceIndex =
    mesh.geometry.materials
      .slice(0, materialIndex + 1)
      .filter(
        (material) =>
          material.textureName &&
          normalizeTextureFileName(
            material.textureName
          ) === normalized
      ).length - 1;

  return mesh.materialOverrides.find(
    (override) =>
      override.materialIndex ===
        occurrenceIndex &&
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

  const [
    availableMaps,
    setAvailableMaps
  ] = useState<OmsiMapCatalogEntry[]>([]);

  const [
    loadingMapCatalog,
    setLoadingMapCatalog
  ] = useState(false);

  const [
    mapCatalogProgress,
    setMapCatalogProgress
  ] = useState<{
    completed: number;
    total: number;
    skipped: number;
    directoryName: string | null;
  }>();

  const [mapSearch, setMapSearch] =
    useState("");

  const [
    newMapDisplayName,
    setNewMapDisplayName
  ] = useState("");

  const [
    newMapDirectoryName,
    setNewMapDirectoryName
  ] = useState("");

  const [
    newMapLatitude,
    setNewMapLatitude
  ] = useState("");

  const [
    newMapLongitude,
    setNewMapLongitude
  ] = useState("");

  const [
    creatingCoordinateMap,
    setCreatingCoordinateMap
  ] = useState(false);

  const [
    sidebarCollapsed,
    setSidebarCollapsed
  ] = useState(false);

  const [mapLoadMode, setMapLoadMode] =
    useState<MapLoadMode>("performance");

  const autoObjectTextureLimit =
    mapLoadMode === "full"
      ? fullMapObjectTextureLimit
      : performanceObjectTextureLimit;

  const autoSplineTextureLimit =
    mapLoadMode === "full"
      ? fullMapSplineTextureLimit
      : performanceSplineTextureLimit;

  const autoObjectTextureBatch =
    mapLoadMode === "full"
      ? fullMapObjectTextureBatch
      : performanceObjectTextureBatch;

  const autoSplineTextureBatch =
    mapLoadMode === "full"
      ? fullMapSplineTextureBatch
      : performanceSplineTextureBatch;

  const autoTextureLimit =
    autoObjectTextureLimit +
    autoSplineTextureLimit;

  const [editorTool, setEditorTool] =
    useState<EditorTool>("select");

  const [
    activeConstructionTool,
    setActiveConstructionTool
  ] = useState<
    QuickCreateTool | undefined
  >();

  const [
    junctionPlacementTarget,
    setJunctionPlacementTarget
  ] = useState<
    PendingObjectPlacement | undefined
  >();

  const [
    selectionMode,
    setSelectionMode
  ] = useState<SelectionMode>("all");

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
    activeTopMenu,
    setActiveTopMenu
  ] = useState<
    | "file"
    | "edit"
    | "view"
    | "map"
    | undefined
  >();

  const [
    showRealMapPanel,
    setShowRealMapPanel
  ] = useState(false);

  const [
    showTileNavigator,
    setShowTileNavigator
  ] = useState(true);

  const [
    fullScreenPanel,
    setFullScreenPanel
  ] = useState<
    "explorer" | "inspector" | undefined
  >();

  const [
    desktopExplorerOpen,
    setDesktopExplorerOpen
  ] = useState(false);

  const [
    desktopInspectorOpen,
    setDesktopInspectorOpen
  ] = useState(false);

  useEffect(() => {
    if (!isFullScreen) {
      setFullScreenPanel(undefined);
    }
  }, [isFullScreen]);

  const requestFullScreen =
    useCallback(
      (enabled: boolean) => {
        setIsFullScreen(enabled);

        if (bridgeAvailable) {
          setFullScreen(enabled);
          return;
        }

        const root =
          document.documentElement;

        if (enabled) {
          void root
            .requestFullscreen?.()
            .catch(() => {
              setIsFullScreen(true);
            });
        } else if (
          document.fullscreenElement
        ) {
          void document
            .exitFullscreen?.()
            .catch(() => {
              setIsFullScreen(false);
            });
        }
      },
      [bridgeAvailable]
    );

  useEffect(() => {
    const handleBrowserFullScreen = () => {
      if (!bridgeAvailable) {
        setIsFullScreen(
          Boolean(
            document.fullscreenElement
          )
        );
      }
    };

    document.addEventListener(
      "fullscreenchange",
      handleBrowserFullScreen
    );

    return () =>
      document.removeEventListener(
        "fullscreenchange",
        handleBrowserFullScreen
      );
  }, [bridgeAvailable]);

  useEffect(() => {
    type FloatingPosition = {
      left: number;
      top: number;
    };

    const storageKey =
      "omsi-map-studio:floating-tool-positions-v1";

    const readPositions = () => {
      try {
        const raw =
          window.localStorage.getItem(
            storageKey
          );

        return raw
          ? (
              JSON.parse(raw) as
                Record<
                  string,
                  FloatingPosition
                >
            )
          : {};
      } catch {
        return {};
      }
    };

    const writePosition = (
      key: string,
      position: FloatingPosition
    ) => {
      try {
        const current =
          readPositions();

        window.localStorage.setItem(
          storageKey,
          JSON.stringify({
            ...current,
            [key]: position
          })
        );
      } catch {
        // A falha de personalização visual
        // nunca deve bloquear o editor.
      }
    };

    const getPanelKey = (
      panel: HTMLElement
    ) => {
      const explicit =
        panel.dataset
          .floatingToolId;

      if (explicit) {
        return explicit;
      }

      return (
        Array.from(panel.classList)
          .find(
            (name) =>
              name !==
                "floating-tool" &&
              name !== "active" &&
              name !== "warning"
          ) ??
        "floating-tool"
      );
    };

    const applySavedPosition = (
      panel: HTMLElement
    ) => {
      if (
        panel.dataset
          .floatingPositionRestored ===
        "true"
      ) {
        return;
      }

      panel.dataset
        .floatingPositionRestored =
        "true";

      const position =
        readPositions()[
          getPanelKey(panel)
        ];

      if (!position) {
        return;
      }

      const parent =
        panel.offsetParent as
          | HTMLElement
          | null;

      const parentRect =
        parent
          ?.getBoundingClientRect() ??
        new DOMRect(
          0,
          0,
          window.innerWidth,
          window.innerHeight
        );

      const panelRect =
        panel.getBoundingClientRect();

      const maximumLeft =
        Math.max(
          0,
          parentRect.width -
            panelRect.width
        );

      const maximumTop =
        Math.max(
          0,
          parentRect.height -
            panelRect.height
        );

      panel.style.left =
        `${Math.min(
          maximumLeft,
          Math.max(
            0,
            position.left
          )
        )}px`;

      panel.style.top =
        `${Math.min(
          maximumTop,
          Math.max(
            0,
            position.top
          )
        )}px`;

      panel.style.right = "auto";
      panel.style.bottom = "auto";
      panel.style.transform = "none";
    };

    const restorePanels = () => {
      document
        .querySelectorAll<HTMLElement>(
          "[data-floating-tool]"
        )
        .forEach(
          applySavedPosition
        );
    };

    restorePanels();

    const observer =
      new MutationObserver(() => {
        window.requestAnimationFrame(
          restorePanels
        );
      });

    observer.observe(
      document.body,
      {
        childList: true,
        subtree: true
      }
    );

    let drag:
      | {
          panel: HTMLElement;
          key: string;
          pointerId: number;
          offsetX: number;
          offsetY: number;
          parentRect: DOMRect;
        }
      | undefined;

    const handlePointerDown = (
      event: PointerEvent
    ) => {
      if (
        event.button !== 0 ||
        !(event.target instanceof
          HTMLElement)
      ) {
        return;
      }

      const handle =
        event.target.closest(
          "[data-drag-handle]"
        ) as HTMLElement | null;

      const panel =
        handle?.closest(
          "[data-floating-tool]"
        ) as HTMLElement | null;

      if (!handle || !panel) {
        return;
      }

      applySavedPosition(panel);

      const rect =
        panel.getBoundingClientRect();

      const parent =
        panel.offsetParent as
          HTMLElement | null;

      const parentRect =
        parent
          ?.getBoundingClientRect() ??
        new DOMRect(
          0,
          0,
          window.innerWidth,
          window.innerHeight
        );

      panel.style.left =
        `${rect.left -
        parentRect.left}px`;

      panel.style.top =
        `${rect.top -
        parentRect.top}px`;

      panel.style.right = "auto";
      panel.style.bottom = "auto";
      panel.style.transform = "none";

      drag = {
        panel,
        key:
          getPanelKey(panel),
        pointerId:
          event.pointerId,
        offsetX:
          event.clientX -
          rect.left,
        offsetY:
          event.clientY -
          rect.top,
        parentRect
      };

      handle.setPointerCapture?.(
        event.pointerId
      );

      event.preventDefault();
    };

    const handlePointerMove = (
      event: PointerEvent
    ) => {
      if (
        !drag ||
        drag.pointerId !==
          event.pointerId
      ) {
        return;
      }

      const panelRect =
        drag.panel
          .getBoundingClientRect();

      const maximumLeft =
        Math.max(
          0,
          drag.parentRect.width -
            panelRect.width
        );

      const maximumTop =
        Math.max(
          0,
          drag.parentRect.height -
            panelRect.height
        );

      const left =
        Math.min(
          maximumLeft,
          Math.max(
            0,
            event.clientX -
              drag.parentRect.left -
              drag.offsetX
          )
        );

      const top =
        Math.min(
          maximumTop,
          Math.max(
            0,
            event.clientY -
              drag.parentRect.top -
              drag.offsetY
          )
        );

      drag.panel.style.left =
        `${left}px`;

      drag.panel.style.top =
        `${top}px`;
    };

    const handlePointerUp = (
      event: PointerEvent
    ) => {
      if (
        !drag ||
        drag.pointerId !==
          event.pointerId
      ) {
        return;
      }

      const left =
        Number.parseFloat(
          drag.panel.style.left
        );

      const top =
        Number.parseFloat(
          drag.panel.style.top
        );

      if (
        Number.isFinite(left) &&
        Number.isFinite(top)
      ) {
        writePosition(
          drag.key,
          {
            left,
            top
          }
        );
      }

      drag = undefined;
    };

    document.addEventListener(
      "pointerdown",
      handlePointerDown
    );

    window.addEventListener(
      "pointermove",
      handlePointerMove
    );

    window.addEventListener(
      "pointerup",
      handlePointerUp
    );

    window.addEventListener(
      "pointercancel",
      handlePointerUp
    );

    return () => {
      observer.disconnect();

      document.removeEventListener(
        "pointerdown",
        handlePointerDown
      );

      window.removeEventListener(
        "pointermove",
        handlePointerMove
      );

      window.removeEventListener(
        "pointerup",
        handlePointerUp
      );

      window.removeEventListener(
        "pointercancel",
        handlePointerUp
      );
    };
  }, []);

  useEffect(() => {
    if (
      !rootPath ||
      !bridgeAvailable
    ) {
      return;
    }

    setLoadingMapCatalog(true);
    setMapCatalogProgress(undefined);
    loadMapCatalog();
  }, [
    bridgeAvailable,
    rootPath
  ]);

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
    assetWarmupActive,
    setAssetWarmupActive
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

  const [
    terrainEditPoint,
    setTerrainEditPoint
  ] = useState<{
    tileX: number;
    tileY: number;
    x: number;
    y: number;
    height: number;
  }>();

  const [
    terrainTargetHeight,
    setTerrainTargetHeight
  ] = useState(0);

  const [
    terrainBrushRadius,
    setTerrainBrushRadius
  ] = useState(20);

  const [
    terrainBrushFeather,
    setTerrainBrushFeather
  ] = useState(0.25);

  const [
    savingTerrain,
    setSavingTerrain
  ] = useState(false);

  const [
    googleApiKey,
    setGoogleApiKey
  ] = useState("");

  const [
    googleLatitude,
    setGoogleLatitude
  ] = useState("");

  const [
    googleLongitude,
    setGoogleLongitude
  ] = useState("");

  const [
    googleZoom,
    setGoogleZoom
  ] = useState(18);

  const [
    googleMapType,
    setGoogleMapType
  ] = useState<
    "roadmap" |
    "satellite" |
    "hybrid" |
    "terrain"
  >("hybrid");

  const [
    loadingGoogleReference,
    setLoadingGoogleReference
  ] = useState(false);

  const [
    googleReference,
    setGoogleReference
  ] = useState<GoogleMapReference>();

  const [
    referenceVisible,
    setReferenceVisible
  ] = useState(true);

  const [
    referenceOpacity,
    setReferenceOpacity
  ] = useState(0.55);

  const [
    googleElevationGrid,
    setGoogleElevationGrid
  ] = useState<GoogleElevationGrid>();

  const [
    loadingElevationGrid,
    setLoadingElevationGrid
  ] = useState(false);

  const [
    elevationVerticalOffset,
    setElevationVerticalOffset
  ] = useState(0);

  const [
    elevationSampleCount,
    setElevationSampleCount
  ] = useState(17);

  const [
    applyingElevationGrid,
    setApplyingElevationGrid
  ] = useState(false);

  const [
    georefAnchor,
    setGeorefAnchor
  ] = useState<{
    tileX: number;
    tileY: number;
    x: number;
    y: number;
  }>({
    tileX: 0,
    tileY: 0,
    x: 150,
    y: 150
  });

  const [
    easyRoadMode,
    setEasyRoadMode
  ] = useState(false);

  const [
    easyRoadStart,
    setEasyRoadStart
  ] = useState<RoadPoint>();

  const [
    easyRoadEnd,
    setEasyRoadEnd
  ] = useState<RoadPoint>();

  const [
    easyRoadCurveOffset,
    setEasyRoadCurveOffset
  ] = useState(0);

  const [
    roadPlacementKind,
    setRoadPlacementKind
  ] = useState<"road" | "bridge">(
    "road"
  );

  const [
    roadElevationOffset,
    setRoadElevationOffset
  ] = useState(0);

  const [
    roadEndpointSnapEnabled,
    setRoadEndpointSnapEnabled
  ] = useState(true);

  const [
    roadEndpointSnapDistance,
    setRoadEndpointSnapDistance
  ] = useState(5);

  const [
    roadStartSnap,
    setRoadStartSnap
  ] = useState<
    RoadEndpointSnap | undefined
  >();

  const [
    roadEndSnap,
    setRoadEndSnap
  ] = useState<
    RoadEndpointSnap | undefined
  >();

  const [
    roadAutoConnectEnabled,
    setRoadAutoConnectEnabled
  ] = useState(true);

  const pendingRoadAutoLinkRef =
    useRef<
      | {
          previousSplineId: number;
          nextSplineId: number;
        }
      | undefined
    >(undefined);

  const pendingConstructionSetAfterLinkRef =
    useRef<
      ConstructionSetDefinition | undefined
    >(undefined);

  const pendingConstructionSetSplineRef =
    useRef<
      OmsiPlacedSpline | undefined
    >(undefined);

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

  const [
    assetIndexStatus,
    setAssetIndexStatus
  ] = useState<{
    state:
      | "idle"
      | "indexing"
      | "ready"
      | "error";
    examinedFiles: number;
    candidateFiles: number;
    totalEntries: number;
    addedFiles: number;
    updatedFiles: number;
    unchangedFiles: number;
    removedFiles: number;
    durationMilliseconds: number;
    sceneryObjects: number;
    splines: number;
    models: number;
    textures: number;
    detail?: string;
  }>({
    state: "idle",
    examinedFiles: 0,
    candidateFiles: 0,
    totalEntries: 0,
    addedFiles: 0,
    updatedFiles: 0,
    unchangedFiles: 0,
    removedFiles: 0,
    durationMilliseconds: 0,
    sceneryObjects: 0,
    splines: 0,
    models: 0,
    textures: 0
  });

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
    sceneryLibraryGroup,
    setSceneryLibraryGroup
  ] = useState<SceneryLibraryGroup>(
    "all"
  );

  const [
    sceneryLibraryPreviewAsset,
    setSceneryLibraryPreviewAsset
  ] = useState<SceneryLibraryEntry>();

  const [
    sceneryLibraryView,
    setSceneryLibraryView
  ] = useState<LibraryViewMode>(
    "groups"
  );

  const [
    sceneryTechnicalFilter,
    setSceneryTechnicalFilter
  ] = useState<SceneryTechnicalFilter>(
    "all"
  );

  const [
    scenerySubcategory,
    setScenerySubcategory
  ] = useState("all");

  const [
    sceneryFavorites,
    setSceneryFavorites
  ] = useState<string[]>(() =>
    readStoredJson(
      libraryStorageKeys.sceneryFavorites,
      []
    )
  );

  const [
    sceneryRecent,
    setSceneryRecent
  ] = useState<string[]>(() =>
    readStoredJson(
      libraryStorageKeys.sceneryRecent,
      []
    )
  );

  const [
    sceneryUsage,
    setSceneryUsage
  ] = useState<Record<string, number>>(
    () =>
      readStoredJson(
        libraryStorageKeys.sceneryUsage,
        {}
      )
  );

  const [
    loadingSceneryLibrary,
    setLoadingSceneryLibrary
  ] = useState(false);

  const [
    sceneryLibraryReady,
    setSceneryLibraryReady
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

  const requestedGeometryPathsRef =
    useRef<Set<string>>(
      new Set()
    );

  const requestedSplineProfilePathsRef =
    useRef<Set<string>>(
      new Set()
    );

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
    splineLibraryGroup,
    setSplineLibraryGroup
  ] = useState<SplineLibraryGroup>(
    "all"
  );

  const [
    splineLibraryPreviewAsset,
    setSplineLibraryPreviewAsset
  ] = useState<SplineLibraryEntry>();

  const [
    splineLibraryView,
    setSplineLibraryView
  ] = useState<LibraryViewMode>(
    "groups"
  );

  const [
    splineTechnicalFilter,
    setSplineTechnicalFilter
  ] = useState<SplineTechnicalFilter>(
    "all"
  );

  const [
    splineSubcategory,
    setSplineSubcategory
  ] = useState("all");

  const [
    splineFavorites,
    setSplineFavorites
  ] = useState<string[]>(() =>
    readStoredJson(
      libraryStorageKeys.splineFavorites,
      []
    )
  );

  const [
    splineRecent,
    setSplineRecent
  ] = useState<string[]>(() =>
    readStoredJson(
      libraryStorageKeys.splineRecent,
      []
    )
  );

  const [
    splineUsage,
    setSplineUsage
  ] = useState<Record<string, number>>(
    () =>
      readStoredJson(
        libraryStorageKeys.splineUsage,
        {}
      )
  );

  const [
    libraryCollections,
    setLibraryCollections
  ] = useState<Record<string, string[]>>(
    () =>
      readStoredJson(
        libraryStorageKeys.collections,
        {}
      )
  );

  const [
    assetThumbnailCache,
    setAssetThumbnailCache
  ] = useState<Record<string, string>>(
    () =>
      readStoredJson(
        libraryStorageKeys.thumbnails,
        {}
      )
  );

  const [
    activeLibraryCollection,
    setActiveLibraryCollection
  ] = useState("");

  const [
    newCollectionName,
    setNewCollectionName
  ] = useState("");

  const [
    constructionSets,
    setConstructionSets
  ] = useState<
    ConstructionSetDefinition[]
  >(() =>
    readStoredJson(
      libraryStorageKeys
        .constructionSets,
      []
    )
  );

  const [
    activeConstructionSetId,
    setActiveConstructionSetId
  ] = useState("");

  const [
    newConstructionSetName,
    setNewConstructionSetName
  ] = useState("");

  const [
    showConstructionSetPanel,
    setShowConstructionSetPanel
  ] = useState(false);

  const [
    showMapHealthPanel,
    setShowMapHealthPanel
  ] = useState(false);

  const [
    applyConstructionSet,
    setApplyConstructionSet
  ] = useState(true);

  const pendingConstructionSetRef =
    useRef<
      ConstructionSetDefinition | undefined
    >(undefined);

  const pendingConstructionSetLabelRef =
    useRef<string | undefined>(
      undefined
    );

  const [
    loadingSplineLibrary,
    setLoadingSplineLibrary
  ] = useState(false);

  const [
    splineLibraryReady,
    setSplineLibraryReady
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
    pendingPlacementBatch,
    setPendingPlacementBatch
  ] = useState<
    PendingObjectPlacement[]
  >([]);

  const [
    placementMode,
    setPlacementMode
  ] = useState<ObjectPlacementMode>(
    "single"
  );

  const [
    placementLineStart,
    setPlacementLineStart
  ] = useState<
    PendingObjectPlacement
  >();

  const [
    placementSpacing,
    setPlacementSpacing
  ] = useState(12);

  const [
    placementBrushRadius,
    setPlacementBrushRadius
  ] = useState(20);

  const [
    placementBrushCount,
    setPlacementBrushCount
  ] = useState(18);

  const [
    placementMatrixRows,
    setPlacementMatrixRows
  ] = useState(4);

  const [
    placementMatrixColumns,
    setPlacementMatrixColumns
  ] = useState(4);

  const [
    placementMatrixSpacingX,
    setPlacementMatrixSpacingX
  ] = useState(6);

  const [
    placementMatrixSpacingY,
    setPlacementMatrixSpacingY
  ] = useState(6);

  const [
    placementCircleRadius,
    setPlacementCircleRadius
  ] = useState(15);

  const [
    placementCircleCount,
    setPlacementCircleCount
  ] = useState(12);

  const [
    placementCircleTangent,
    setPlacementCircleTangent
  ] = useState(true);

  const [
    placementLotSetback,
    setPlacementLotSetback
  ] = useState(7);

  const [
    constructionPreset,
    setConstructionPreset
  ] = useState<ConstructionPresetId>(
    "none"
  );

  const [
    placementRandomRotation,
    setPlacementRandomRotation
  ] = useState(false);

  const [
    placementAlignRoad,
    setPlacementAlignRoad
  ] = useState(false);

  const [
    placementRoadSnapDistance,
    setPlacementRoadSnapDistance
  ] = useState(8);

  const batchKeepPlacementRef =
    useRef(false);
  const batchPlacementAssetRef =
    useRef<
      SceneryLibraryEntry | undefined
    >(undefined);

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

  const [
    constructionUndoStack,
    setConstructionUndoStack
  ] = useState<
    ConstructionHistoryEntry[]
  >([]);

  const [
    constructionRedoStack,
    setConstructionRedoStack
  ] = useState<
    ConstructionHistoryEntry[]
  >([]);

  const [
    restoringConstruction,
    setRestoringConstruction
  ] = useState(false);

  const [
    dependencyReplacementTarget,
    setDependencyReplacementTarget
  ] = useState<
    DependencyReplacementTarget | undefined
  >();

  const [
    replacingDependency,
    setReplacingDependency
  ] = useState(false);

  const constructionRestoreModeRef =
    useRef<
      "undo" | "redo" | undefined
    >(undefined);

  const constructionRestoreEntryRef =
    useRef<
      ConstructionHistoryEntry | undefined
    >(undefined);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys.sceneryFavorites,
      sceneryFavorites
    );
  }, [sceneryFavorites]);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys.sceneryRecent,
      sceneryRecent
    );
  }, [sceneryRecent]);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys.sceneryUsage,
      sceneryUsage
    );
  }, [sceneryUsage]);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys.splineFavorites,
      splineFavorites
    );
  }, [splineFavorites]);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys.splineRecent,
      splineRecent
    );
  }, [splineRecent]);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys.splineUsage,
      splineUsage
    );
  }, [splineUsage]);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys.collections,
      libraryCollections
    );
  }, [libraryCollections]);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys.thumbnails,
      assetThumbnailCache
    );
  }, [assetThumbnailCache]);

  useEffect(() => {
    writeStoredJson(
      libraryStorageKeys
        .constructionSets,
      constructionSets
    );
  }, [constructionSets]);

  useEffect(() => {
    setPendingPlacementBatch([]);
    setPlacementLineStart(undefined);
    batchKeepPlacementRef.current =
      false;
    batchPlacementAssetRef.current =
      undefined;
    setConstructionUndoStack([]);
    setConstructionRedoStack([]);
    setRestoringConstruction(false);
    setDependencyReplacementTarget(
      undefined
    );
    setReplacingDependency(false);
    constructionRestoreModeRef.current =
      undefined;
    constructionRestoreEntryRef.current =
      undefined;
  }, [selectedMap?.directoryName]);

  const activeConstructionSet =
    useMemo(
      () =>
        constructionSets.find(
          (set) =>
            set.id ===
            activeConstructionSetId
        ),
      [
        activeConstructionSetId,
        constructionSets
      ]
    );

  const activeConstructionSetMissingTemplates =
    useMemo(() => {
      if (!activeConstructionSet) {
        return [];
      }

      const known =
        new Set(
          objects.map((item) =>
            normalizeAssetClassifierText(
              item.sceneryObjectPath
            )
          )
        );

      return activeConstructionSet
        .companions
        .filter(
          (companion) =>
            !known.has(
              normalizeAssetClassifierText(
                companion
                  .sceneryObjectPath
              )
            )
        )
        .map(
          (companion) =>
            companion
              .sceneryObjectPath
        );
    }, [
      activeConstructionSet,
      objects
    ]);

  const interactionLocked =
    selectingRoot ||
    selectingMap ||
    loadingFullMap ||
    Boolean(loadingRegionKey) ||
    loadingSceneryLibrary ||
    loadingSplineLibrary ||
    creatingCoordinateMap;

  useEffect(() => {
    if (!interactionLocked) {
      return;
    }

    const activeElement =
      document.activeElement;

    if (
      activeElement instanceof
      HTMLElement
    ) {
      activeElement.blur();
    }

    const blockKeyboard = (
      event: KeyboardEvent
    ) => {
      if (
        event.key === "F11" ||
        (event.key === "Escape" &&
          isFullScreen)
      ) {
        return;
      }

      event.preventDefault();
      event.stopImmediatePropagation();
    };

    window.addEventListener(
      "keydown",
      blockKeyboard,
      true
    );

    return () =>
      window.removeEventListener(
        "keydown",
        blockKeyboard,
        true
      );
  }, [
    interactionLocked,
    isFullScreen
  ]);

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
          setAssetIndexStatus({
            state: "idle",
            examinedFiles: 0,
            candidateFiles: 0,
            totalEntries: 0,
            addedFiles: 0,
            updatedFiles: 0,
            unchangedFiles: 0,
            removedFiles: 0,
            durationMilliseconds: 0,
            sceneryObjects: 0,
            splines: 0,
            models: 0,
            textures: 0
          });
          setSelectedMap(undefined);
          setAvailableMaps([]);
          setLoadingMapCatalog(true);
          setMapCatalogProgress(undefined);
          setMapSearch("");
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
          setSelectionMode("all");
          setPreloadingGeometryFor(undefined);
          setPreloadingSplineProfileFor(
            undefined
          );
          setSelectingRoot(false);
          setSelectingMap(false);
          setLoadingRegionKey(undefined);
          setLoadingFullMap(false);
          setAssetWarmupActive(false);
          setFullMapProgress(undefined);
          setLoadedFullMapFor(undefined);
          setSaving(false);
          setSaveNotice(undefined);
          setExplorerSearch("");
          setExplorerPanelTab("map");
          setSceneryLibrary([]);
          setLibrarySearch("");
          setLoadingSceneryLibrary(false);
          setSceneryLibraryReady(false);
          setSplineLibrary([]);
          setSplineLibrarySearch("");
          setLoadingSplineLibrary(false);
          setSplineLibraryReady(false);
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

        if (
          message.type ===
          "assetIndexRefreshStarted"
        ) {
          setAssetIndexStatus({
            state: "indexing",
            examinedFiles: 0,
            candidateFiles: 0,
            totalEntries: 0,
            addedFiles: 0,
            updatedFiles: 0,
            unchangedFiles: 0,
            removedFiles: 0,
            durationMilliseconds: 0,
            sceneryObjects: 0,
            splines: 0,
            models: 0,
            textures: 0
          });
          return;
        }

        if (
          message.type ===
          "assetIndexRefreshProgress"
        ) {
          setAssetIndexStatus(
            (current) => ({
              ...current,
              state: "indexing",
              examinedFiles:
                message.examinedFiles,
              candidateFiles:
                message.candidateFiles
            })
          );
          return;
        }

        if (
          message.type ===
          "assetIndexRefreshCompleted"
        ) {
          setAssetIndexStatus({
            state: "ready",
            examinedFiles:
              message.examinedFiles,
            candidateFiles:
              message.totalEntries,
            totalEntries:
              message.totalEntries,
            addedFiles:
              message.addedFiles,
            updatedFiles:
              message.updatedFiles,
            unchangedFiles:
              message.unchangedFiles,
            removedFiles:
              message.removedFiles,
            durationMilliseconds:
              message.durationMilliseconds,
            sceneryObjects:
              message.sceneryObjects,
            splines:
              message.splines,
            models:
              message.models,
            textures:
              message.textures
          });
          return;
        }

        if (
          message.type ===
          "assetIndexRefreshFailed"
        ) {
          setAssetIndexStatus(
            (current) => ({
              ...current,
              state: "error",
              detail: message.detail
            })
          );
          return;
        }

        if (
          message.type ===
          "mapCatalogLoadingStarted"
        ) {
          setLoadingMapCatalog(true);
          setMapCatalogProgress({
            completed: 0,
            total: 0,
            skipped: 0,
            directoryName: null
          });
          return;
        }

        if (
          message.type ===
          "mapCatalogLoadingProgress"
        ) {
          setLoadingMapCatalog(true);
          setMapCatalogProgress({
            completed: message.completed,
            total: message.total,
            skipped: message.skipped,
            directoryName:
              message.directoryName
          });
          return;
        }

        if (
          message.type ===
          "mapCatalogLoaded"
        ) {
          setAvailableMaps(
            message.entries
          );
          setLoadingMapCatalog(false);
          setMapCatalogProgress(
            (current) =>
              current
                ? {
                    ...current,
                    completed:
                      current.total,
                    skipped:
                      message.skippedMaps
                  }
                : undefined
          );
          return;
        }

        if (
          message.type ===
          "coordinateMapCreated"
        ) {
          setCreatingCoordinateMap(false);
          setGoogleLatitude(
            String(message.latitude)
          );
          setGoogleLongitude(
            String(message.longitude)
          );
          setGoogleReference(undefined);
          setGoogleElevationGrid(undefined);
          setReferenceVisible(true);
          setSaveNotice(
            `Mapa "${message.displayName}" criado a partir do template NewMap com âncora ${message.latitude}, ${message.longitude}.`
          );
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
          requestedGeometryPathsRef.current.clear();
          requestedSplineProfilePathsRef.current.clear();
          setPreloadingGeometryFor(undefined);
          setPreloadingSplineProfileFor(
            undefined
          );
          setSelectingMap(false);
          setMapLoadMode("performance");
          setEditorTool("select");
          setSelectionMode("all");
          setCameraMode("perspective");
          setPreviewObjectTransforms({});
          setPreviewSplineTransforms({});
          setUndoPreviewStack([]);
          setRedoPreviewStack([]);
          setLoadingRegionKey(undefined);
          setLoadedRegionKey(undefined);
          setLoadingFullMap(false);
          setAssetWarmupActive(true);
          setFullMapProgress(undefined);
          setLoadedFullMapFor(undefined);
          setInspectorTab("general");
          setDesktopExplorerOpen(false);
          setDesktopInspectorOpen(false);
          setSaving(false);
          setSaveNotice(undefined);
          setExplorerSearch("");
          setExplorerPanelTab("map");
          setSceneryLibraryGroup("all");
          setSplineLibraryGroup("all");
          setSceneryLibraryPreviewAsset(
            undefined
          );
          setSplineLibraryPreviewAsset(
            undefined
          );
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
          setSceneryLibraryReady(
            true
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
          setSplineLibraryReady(
            true
          );
          return;
        }

        if (
          message.type ===
          "mapFullLoadingStarted"
        ) {
          setLoadingFullMap(true);
          setAssetWarmupActive(true);
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
                (tile) => {
                  const loaded =
                    loadedByCoordinate.get(
                      `${tile.x}:${tile.y}`
                    );

                  if (loaded) {
                    return loaded;
                  }

                  if (
                    !tile.terrain &&
                    !tile.terrainRenderData &&
                    !tile.terrainTextureMasks
                      ?.length
                  ) {
                    return tile;
                  }

                  return {
                    ...tile,
                    detailsLoaded: false,
                    streamDetail:
                      undefined,
                    streamRing:
                      undefined,
                    terrain: undefined,
                    terrainRenderData:
                      undefined,
                    terrainTextureMasks:
                      undefined
                  };
                }
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

          requestedSplineProfilePathsRef.current.delete(
            message.splinePath
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

          requestedGeometryPathsRef.current.delete(
            message.sceneryObjectPath
          );

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
          "assetPathReplaced"
        ) {
          setReplacingDependency(
            false
          );

          if (
            message.replacements > 0 &&
            message.backupDirectory
          ) {
            setConstructionUndoStack(
              (current) => [
                ...current,
                {
                  label:
                    "Substituição " +
                    (
                      message.kind ===
                        "object"
                        ? "SCO"
                        : "SLI"
                    ) +
                    ": " +
                    getObjectName(
                      message.oldPath
                    ),
                  backupDirectory:
                    message
                      .backupDirectory
                }
              ].slice(-40)
            );
            setConstructionRedoStack([]);
          }

          setDependencyReplacementTarget(
            undefined
          );
          setLoadedFullMapFor(
            undefined
          );
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);
          setSelectedObject(undefined);
          setSelectedSpline(undefined);
          setPreviewObjectTransforms({});
          setPreviewSplineTransforms({});

          setSaveNotice(
            message.replacements > 0
              ? `${message.replacements} referência(s) ${message.kind === "object" ? ".sco" : ".sli"} substituída(s) em ${message.filesSaved} arquivo(s). Backup: ${message.backupDirectory}`
              : "Nenhuma referência correspondente foi encontrada nos tiles do mapa."
          );
          return;
        }

        if (
          message.type ===
          "backupRestored"
        ) {
          const mode =
            constructionRestoreModeRef
              .current;
          const entry =
            constructionRestoreEntryRef
              .current;

          setRestoringConstruction(
            false
          );

          if (entry && mode === "undo") {
            setConstructionUndoStack(
              (current) =>
                current.slice(
                  0,
                  Math.max(
                    0,
                    current.length - 1
                  )
                )
            );
            setConstructionRedoStack(
              (current) => [
                ...current,
                {
                  label:
                    entry.label,
                  backupDirectory:
                    message
                      .rollbackBackupDirectory
                }
              ].slice(-40)
            );
          } else if (
            entry &&
            mode === "redo"
          ) {
            setConstructionRedoStack(
              (current) =>
                current.slice(
                  0,
                  Math.max(
                    0,
                    current.length - 1
                  )
                )
            );
            setConstructionUndoStack(
              (current) => [
                ...current,
                {
                  label:
                    entry.label,
                  backupDirectory:
                    message
                      .rollbackBackupDirectory
                }
              ].slice(-40)
            );
          }

          constructionRestoreModeRef.current =
            undefined;
          constructionRestoreEntryRef.current =
            undefined;

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);
          setSelectedObject(undefined);
          setSelectedSpline(undefined);
          setPreviewObjectTransforms({});
          setPreviewSplineTransforms({});
          setSaveNotice(
            "Backup restaurado em " +
              message.filesRestored +
              " arquivo(s)."
          );

          return;
        }

        if (
          message.type ===
          "objectMultiBatchInserted"
        ) {
          setConstructionUndoStack(
            (current) => [
              ...current,
              {
                label:
                  pendingConstructionSetLabelRef
                    .current ??
                  (
                    "Multi-lote de " +
                    message.count +
                    " objeto(s)"
                  ),
                backupDirectory:
                  message.backupDirectory
              }
            ].slice(-40)
          );
          setConstructionRedoStack([]);
          setInsertingObject(false);

          setSaveNotice(
            (pendingConstructionSetLabelRef
              .current ??
              "Conjunto") +
              ": " +
              message.count +
              " objeto(s) em " +
              message.groupCount +
              " grupo(s). Backup: " +
              message.backupDirectory
          );

          pendingConstructionSetLabelRef
            .current =
            undefined;

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
          "objectBatchInserted"
        ) {
          setConstructionUndoStack(
            (current) => [
              ...current,
              {
                label:
                  "Lote de " +
                  message.count +
                  " objeto(s)",
                backupDirectory:
                  message.backupDirectory
              }
            ].slice(-40)
          );
          setConstructionRedoStack([]);
          setInsertingObject(false);
          setPendingPlacementBatch([]);
          setPlacementLineStart(undefined);
          setEditorTool("select");

          if (
            batchKeepPlacementRef.current &&
            batchPlacementAssetRef.current
          ) {
            setPlacementAsset(
              batchPlacementAssetRef.current
            );
            setPendingPlacement(undefined);
          } else {
            setPlacementAsset(undefined);
            setPendingPlacement(undefined);
          }

          setSaveNotice(
            `${message.count} objeto(s) inserido(s) em lote. Backup: ${message.backupDirectory}`
          );

          batchKeepPlacementRef.current =
            false;
          batchPlacementAssetRef.current =
            undefined;

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
          "objectInserted"
        ) {
          setConstructionUndoStack(
            (current) => [
              ...current,
              {
                label:
                  "Objeto #" +
                  message.placedObject
                    .objectId,
                backupDirectory:
                  message.backupDirectory
              }
            ].slice(-40)
          );
          setConstructionRedoStack([]);
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
          setConstructionUndoStack(
            (current) => [
              ...current,
              {
                label:
                  "Spline #" +
                  message.placedSpline
                    .splineId,
                backupDirectory:
                  message.backupDirectory
              }
            ].slice(-40)
          );
          setConstructionRedoStack([]);

          const constructionSet =
            pendingConstructionSetRef
              .current;
          const autoLink =
            pendingRoadAutoLinkRef
              .current;

          pendingConstructionSetRef.current =
            undefined;
          pendingRoadAutoLinkRef.current =
            undefined;

          const hasAutoLink =
            Boolean(
              autoLink &&
              (
                autoLink
                  .previousSplineId !==
                  -1 ||
                autoLink
                  .nextSplineId !==
                  -1
              )
            );

          if (
            hasAutoLink &&
            autoLink
          ) {
            pendingConstructionSetAfterLinkRef
              .current =
              constructionSet;
            pendingConstructionSetSplineRef
              .current =
              message.placedSpline;

            updateSplineLinks(
              message.directoryName,
              message.placedSpline,
              autoLink.previousSplineId,
              autoLink.nextSplineId
            );

            setSaveNotice(
              `Spline #${message.placedSpline.splineId} inserida; conectando vínculos OMSI automaticamente.`
            );
          } else if (
            constructionSet &&
            constructionSet.companions
              .length > 0
          ) {
            const groups =
              buildConstructionSetObjectGroups(
                message.placedSpline,
                constructionSet
              );

            if (groups.length > 0) {
              pendingConstructionSetLabelRef
                .current =
                "Conjunto " +
                constructionSet.name;

              insertObjectMultiBatch(
                message.directoryName,
                groups
              );

              setSaveNotice(
                `Spline #${message.placedSpline.splineId} inserida; aplicando conjunto "${constructionSet.name}" com ${groups.length} tipo(s) de objeto.`
              );
            }
          }

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
          setEasyRoadMode(false);
          setEasyRoadStart(undefined);
          setEasyRoadEnd(undefined);
          setEasyRoadCurveOffset(0);
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
          if (
            message.linksUpdated > 0 &&
            message.backupDirectory
          ) {
            setConstructionUndoStack(
              (current) => [
                ...current,
                {
                  label:
                    "Vínculos spline #" +
                    message.splineId,
                  backupDirectory:
                    message
                      .backupDirectory
                }
              ].slice(-40)
            );
            setConstructionRedoStack([]);
          }

          const deferredSet =
            pendingConstructionSetAfterLinkRef
              .current;
          const deferredSpline =
            pendingConstructionSetSplineRef
              .current;

          pendingConstructionSetAfterLinkRef
            .current =
            undefined;
          pendingConstructionSetSplineRef.current =
            undefined;

          if (
            deferredSet &&
            deferredSpline &&
            deferredSet.companions
              .length > 0
          ) {
            const groups =
              buildConstructionSetObjectGroups(
                deferredSpline,
                deferredSet
              );

            if (groups.length > 0) {
              pendingConstructionSetLabelRef
                .current =
                "Conjunto " +
                deferredSet.name;

              insertObjectMultiBatch(
                message.directoryName,
                groups
              );

              setSaveNotice(
                `Vínculos da spline #${message.splineId} atualizados; aplicando conjunto "${deferredSet.name}".`
              );
            }
          }

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

        if (
          message.type ===
          "terrainLeveled"
        ) {
          setSavingTerrain(false);
          setTerrainEditPoint(undefined);

          setSaveNotice(
            message.changedSamples > 0
              ? `Terreno nivelado: ${message.changedSamples} amostra(s) alterada(s). Backup: ${message.backupDirectory}`
              : "O terreno já estava na altura solicitada."
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);
          return;
        }

        if (
          message.type ===
          "googleMapReferenceLoaded"
        ) {
          setLoadingGoogleReference(false);
          setGoogleReference({
            latitude:
              message.latitude,
            longitude:
              message.longitude,
            zoom:
              message.zoom,
            mapType:
              message.mapType,
            width:
              message.width,
            height:
              message.height,
            metersPerPixel:
              message.metersPerPixel,
            centerElevation:
              message.centerElevation,
            mimeType:
              message.mimeType,
            base64Data:
              message.base64Data,
            attribution:
              message.attribution
          });
          setReferenceVisible(true);

          if (
            message.centerElevation !==
              null
          ) {
            setTerrainTargetHeight(
              message.centerElevation
            );
          }

          setSaveNotice(
            message.centerElevation !==
              null
              ? `Referência Google carregada · elevação central ${formatNumber(message.centerElevation)} m.`
              : "Referência Google carregada."
          );
          return;
        }

        if (
          message.type ===
          "googleElevationGridLoaded"
        ) {
          setLoadingElevationGrid(false);
          setGoogleElevationGrid(
            message.grid
          );

          // Zero means the Google elevations are applied in
          // real metres. The user can enter an explicit local offset
          // before writing the .terrain if the map uses a shifted datum.
          setElevationVerticalOffset(0);

          setSaveNotice(
            `Grade real carregada: ${message.grid.rows}×${message.grid.columns} · ${formatNumber(message.grid.minimumElevation)} a ${formatNumber(message.grid.maximumElevation)} m.`
          );
          return;
        }

        if (
          message.type ===
          "terrainElevationGridApplied"
        ) {
          setApplyingElevationGrid(false);

          setSaveNotice(
            message.changedSamples > 0
              ? `Relevo real aplicado no tile ${message.tileX},${message.tileY}: ${message.changedSamples} amostra(s). Backup: ${message.backupDirectory}`
              : "O terreno já correspondia à grade aplicada."
          );

          setLoadedFullMapFor(undefined);
          setLoadedRegionKey(undefined);
          setObjects([]);
          setSplines([]);
          return;
        }

        if (
          message.type ===
          "mapGeoreferenceSaved"
        ) {
          setSaveNotice(
            `Georreferenciamento salvo em ${message.path}`
          );
          return;
        }

        if (message.type === "hostError") {
          setSelectingRoot(false);
          setSelectingMap(false);
          setLoadingRegionKey(undefined);
          setLoadingFullMap(false);
          setAssetWarmupActive(false);
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
          setLoadingMapCatalog(false);
          setCreatingCoordinateMap(false);
          setLoadingGoogleReference(false);
          setLoadingElevationGrid(false);
          setApplyingElevationGrid(false);
          setSavingTerrain(false);
          setSaving(false);
          setInsertingObject(false);
          setDeletingObject(false);
          setSavingSpline(false);
          setSavingSplineLinks(false);
          setInsertingSpline(false);
          setDeletingSpline(false);
          setReplacingDependency(false);
          pendingConstructionSetRef.current =
            undefined;
          pendingConstructionSetLabelRef.current =
            undefined;
          pendingRoadAutoLinkRef.current =
            undefined;
          pendingConstructionSetAfterLinkRef.current =
            undefined;
          pendingConstructionSetSplineRef.current =
            undefined;

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
      setAssetWarmupActive(true);
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
    setAssetWarmupActive(true);

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
    if (
      !bridgeAvailable ||
      !rootPath ||
      !selectedMap
    ) {
      return;
    }

    const textureName =
      nightPreviewEnabled
        ? "himmel05.bmp"
        : "himmel01.bmp";

    const key =
      getSkyTextureAssetKey(
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
      return;
    }

    setRequestedTextureKeys(
      (current) => ({
        ...current,
        [key]: true
      })
    );

    loadSkyTextureAsset(
      textureName
    );
  }, [
    bridgeAvailable,
    nightPreviewEnabled,
    requestedTextureKeys,
    rootPath,
    selectedMap,
    textureAssetsByKey
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
        splinePath ||
      requestedSplineProfilePathsRef.current.has(
        splinePath
      )
    ) {
      return;
    }

    requestedSplineProfilePathsRef.current.add(
      splinePath
    );

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
        sceneryObjectPath ||
      requestedGeometryPathsRef.current.has(
        sceneryObjectPath
      )
    ) {
      return;
    }

    requestedGeometryPathsRef.current.add(
      sceneryObjectPath
    );

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
        ?.sceneryObjectPath ??
      sceneryLibraryPreviewAsset
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

    queueTexture(
      sceneryTreeTextureMeshToken,
      selectedObject
        ?.extraValues?.[1] ??
        geometry.tree?.textureName
    );

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

        queueTexture(
          mesh.declaredPath,
          getStaticTransparencyMapName(
            materialOverride
              ?.transMapSource
          )
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
    sceneryLibraryPreviewAsset,
    selectedObject,
    textureAssetsByKey
  ]);

  useEffect(() => {
    if (!bridgeAvailable) {
      return;
    }

    const splinePath =
      selectedSpline
        ?.splinePath ??
      splinePlacementTemplate
        ?.splinePath ??
      splineLibraryPreviewAsset
        ?.splinePath;

    if (!splinePath) {
      return;
    }

    const definition =
      splineProfilesByPath[
        splinePath
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
              splinePath,
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
        splinePath,
        request.textureName
      );
    }
  }, [
    bridgeAvailable,
    requestedTextureKeys,
    selectedSpline,
    splineLibraryPreviewAsset,
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
              !mask.isValid ||
              (mask.hasPixelStatistics &&
                mask.maximumAlpha === 0) ||
              Object.hasOwn(
                hiddenTerrainLayerIndices,
                mask.layerIndex
              )
            ) {
              return [];
            }

            if (
              groundTexture.maskResolution !==
                null &&
              (mask.width !==
                groundTexture.maskResolution ||
                mask.height !==
                  groundTexture.maskResolution)
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
                maskIsFull:
                  mask.hasPixelStatistics &&
                  mask.minimumAlpha === 255 &&
                  mask.maximumAlpha === 255,
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
          !entry.maskIsFull &&
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
            maskIsFull:
              entry.maskIsFull,
            textureAsset:
              groundTextureAssetsByKey[
                entry.textureKey
              ],
            maskAsset:
              entry.maskIsFull
                ? undefined
                : terrainMaskAssetsByKey[
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

  const roadAutoLinkPlan =
    useMemo(() => {
      if (!roadAutoConnectEnabled) {
        return {
          previousSplineId: -1,
          nextSplineId: -1,
          previousStatus:
            "desativado",
          nextStatus:
            "desativado"
        };
      }

      let previousSplineId = -1;
      let nextSplineId = -1;
      let previousStatus =
        "sem encaixe";
      let nextStatus =
        "sem encaixe";

      if (roadStartSnap) {
        const target =
          splinesForViewport.find(
            (spline) =>
              spline.splineId ===
              roadStartSnap.splineId
          );

        if (
          roadStartSnap.endpoint !==
          "end"
        ) {
          previousStatus =
            "ponta incompatível";
        } else if (
          !target
        ) {
          previousStatus =
            "spline não carregada";
        } else if (
          target.nextSplineId !== -1
        ) {
          previousStatus =
            "ponta já vinculada";
        } else {
          previousSplineId =
            target.splineId;
          previousStatus =
            "pronto";
        }
      }

      if (roadEndSnap) {
        const target =
          splinesForViewport.find(
            (spline) =>
              spline.splineId ===
              roadEndSnap.splineId
          );

        if (
          roadEndSnap.endpoint !==
          "start"
        ) {
          nextStatus =
            "ponta incompatível";
        } else if (
          !target
        ) {
          nextStatus =
            "spline não carregada";
        } else if (
          target.previousSplineId !==
          -1
        ) {
          nextStatus =
            "ponta já vinculada";
        } else {
          nextSplineId =
            target.splineId;
          nextStatus =
            "pronto";
        }
      }

      if (
        previousSplineId !== -1 &&
        previousSplineId ===
          nextSplineId
      ) {
        nextSplineId = -1;
        nextStatus =
          "mesma spline usada no início";
      }

      return {
        previousSplineId,
        nextSplineId,
        previousStatus,
        nextStatus
      };
    }, [
      roadAutoConnectEnabled,
      roadEndSnap,
      roadStartSnap,
      splinesForViewport
    ]);

  const junctionSuggestions =
    useMemo(
      () =>
        findJunctionSuggestions(
          splinesForViewport
        ),
      [splinesForViewport]
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

  const objectPathsForTexturePreload =
    useMemo(
      () =>
        mapLoadMode === "full"
          ? Array.from(
              new Set(
                objectsForViewport.map(
                  (placedObject) =>
                    placedObject.sceneryObjectPath
                )
              )
            )
          : nearbyObjectPaths,
      [
        mapLoadMode,
        nearbyObjectPaths,
        objectsForViewport
      ]
    );

  useEffect(() => {
    if (
      !bridgeAvailable ||
      mapLoadMode !== "performance" ||
      Boolean(loadingRegionKey) ||
      nearbyObjectPaths.length === 0
    ) {
      return;
    }

    const batch =
      nearbyObjectPaths
        .filter(
          (path) =>
            !Object.hasOwn(
              geometryByPath,
              path
            ) &&
            !requestedGeometryPathsRef.current.has(
              path
            )
        )
        .slice(
          0,
          geometryPreloadBatchSize
        );

    for (const path of batch) {
      requestedGeometryPathsRef.current.add(
        path
      );

      loadSceneryObjectGeometry(
        path
      );
    }
  }, [
    bridgeAvailable,
    geometryByPath,
    loadingRegionKey,
    mapLoadMode,
    nearbyObjectPaths
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
        .map(([path]) => path);
    }, [
      activeTile,
      splinesForViewport
    ]);

  const splinePathsForPreload =
    useMemo(
      () =>
        mapLoadMode === "full"
          ? Array.from(
              new Set(
                splinesForViewport.map(
                  (placedSpline) =>
                    placedSpline.splinePath
                )
              )
            )
          : nearbySplinePaths,
      [
        mapLoadMode,
        nearbySplinePaths,
        splinesForViewport
      ]
    );

  const loadedSplineProfileCount =
    useMemo(
      () =>
        splinePathsForPreload.filter(
          (path) =>
            Object.hasOwn(
              splineProfilesByPath,
              path
            )
        ).length,
      [
        splinePathsForPreload,
        splineProfilesByPath
      ]
    );

  const splineProfileDiagnostics =
    useMemo(() => {
      let missing = 0;
      let empty = 0;
      let surfaces = 0;

      for (const path of
        splinePathsForPreload) {
        const definition =
          splineProfilesByPath[
            path
          ];

        if (!definition) {
          continue;
        }

        if (!definition.exists) {
          missing += 1;
          continue;
        }

        surfaces +=
          definition.surfaces.length;

        if (
          definition.surfaces.length ===
          0
        ) {
          empty += 1;
        }
      }

      return {
        missing,
        empty,
        surfaces
      };
    }, [
      splinePathsForPreload,
      splineProfilesByPath
    ]);

  useEffect(() => {
    if (!bridgeAvailable) {
      return;
    }

    const batch =
      splinePathsForPreload
        .filter(
          (path) =>
            !Object.hasOwn(
              splineProfilesByPath,
              path
            ) &&
            !requestedSplineProfilePathsRef.current.has(
              path
            )
        )
        .slice(
          0,
          splineProfilePreloadBatchSize
        );

    for (const path of batch) {
      requestedSplineProfilePathsRef.current.add(
        path
      );

      loadSplineProfile(
        path
      );
    }
  }, [
    bridgeAvailable,
    splinePathsForPreload,
    splineProfilesByPath
  ]);

  useEffect(() => {
    if (!bridgeAvailable) {
      return;
    }

    const geometryReadyForTexturePrefetch =
      objectPathsForTexturePreload.every(
        (path) =>
          Object.hasOwn(
            geometryByPath,
            path
          )
      );

    const splinesReadyForTexturePrefetch =
      splinePathsForPreload.every(
        (path) =>
          Object.hasOwn(
            splineProfilesByPath,
            path
          )
      );

    if (
      !geometryReadyForTexturePrefetch ||
      !splinesReadyForTexturePrefetch
    ) {
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
      objectPathsForTexturePreload) {
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

      if (geometry.tree) {
        const placedTreeTextures =
          Array.from(
            new Set(
              objectsForViewport
                .filter(
                  (placedObject) =>
                    placedObject
                      .sceneryObjectPath ===
                    sceneryObjectPath
                )
                .map(
                  (placedObject) =>
                    placedObject
                      .extraValues?.[1]
                        ?.trim()
                )
                .filter(
                  (
                    value
                  ): value is string =>
                    Boolean(value)
                )
            )
          );

        if (
          placedTreeTextures.length === 0
        ) {
          queueObjectTexture(
            sceneryTreeTextureMeshToken,
            geometry.tree.textureName
          );
        } else {
          for (const textureName of
            placedTreeTextures) {
            queueObjectTexture(
              sceneryTreeTextureMeshToken,
              textureName
            );
          }
        }
      }

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

          queueObjectTexture(
            mesh.declaredPath,
            getStaticTransparencyMapName(
              materialOverride
                ?.transMapSource
            )
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
      splinePathsForPreload) {
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
    autoObjectTextureBatch,
    autoObjectTextureLimit,
    autoSplineTextureBatch,
    autoSplineTextureLimit,
    bridgeAvailable,
    geometryByPath,
    nightPreviewEnabled,
    objectPathsForTexturePreload,
    objectsForViewport,
    requestedTextureKeys,
    splinePathsForPreload,
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

  const missingSceneryDependencies =
    useMemo(() => {
      if (!sceneryLibraryReady) {
        return [];
      }

      const installed =
        new Set(
          sceneryLibrary.map(
            (entry) =>
              normalizeAssetClassifierText(
                entry.sceneryObjectPath
              )
          )
        );

      return Array.from(
        new Set(
          objects.map(
            (item) =>
              item.sceneryObjectPath
          )
        )
      ).filter(
        (path) =>
          !installed.has(
            normalizeAssetClassifierText(
              path
            )
          )
      );
    }, [
      objects,
      sceneryLibrary,
      sceneryLibraryReady
    ]);

  const missingSplineDependencies =
    useMemo(() => {
      if (!splineLibraryReady) {
        return [];
      }

      const installed =
        new Set(
          splineLibrary.map(
            (entry) =>
              normalizeAssetClassifierText(
                entry.splinePath
              )
          )
        );

      return Array.from(
        new Set(
          splines.map(
            (item) =>
              item.splinePath
          )
        )
      ).filter(
        (path) =>
          !installed.has(
            normalizeAssetClassifierText(
              path
            )
          )
      );
    }, [
      splineLibrary,
      splineLibraryReady,
      splines
    ]);

  const missingDependencyCount =
    missingSceneryDependencies.length +
    missingSplineDependencies.length;

  const sceneryProblemPaths =
    useMemo(() => {
      const result =
        new Set<string>();

      for (const [
        path,
        geometry
      ] of Object.entries(
        geometryByPath
      )) {
        const metadata =
          sceneryMetadataByPath[path];

        if (
          metadata?.meshes.some(
            (mesh) =>
              !mesh.fileExists ||
              Boolean(
                mesh.structure
                  ?.errorCode
              )
          )
        ) {
          result.add(path);
        }

        if (
          geometry.meshes.some(
            (mesh) =>
              !mesh.geometry
                .isLoaded ||
              Boolean(
                mesh.geometry
                  .errorCode
              )
          )
        ) {
          result.add(path);
        }

        if (geometry.tree) {
          const key =
            getSceneryTextureAssetKey(
              path,
              sceneryTreeTextureMeshToken,
              geometry.tree
                .textureName
            );
          const asset =
            textureAssetsByKey[key];

          if (
            asset &&
            (
              !asset.exists ||
              Boolean(asset.errorCode)
            )
          ) {
            result.add(path);
          }
        }

        for (const mesh of
          geometry.meshes) {
          for (const [
            materialIndex,
            material
          ] of mesh.geometry
            .materials.entries()) {
            const materialOverride =
              findSceneryMaterialOverride(
                mesh,
                materialIndex
              );

            const textureNames = [
              material.textureName,
              materialOverride
                ?.bumpMapTextureName,
              materialOverride
                ?.nightMapTextureName,
              materialOverride
                ?.environmentMapTextureName,
              getStaticTransparencyMapName(
                materialOverride
                  ?.transMapSource
              )
            ].filter(
              (
                value
              ): value is string =>
                Boolean(value)
            );

            for (const textureName of
              textureNames) {
              const key =
                getSceneryTextureAssetKey(
                  path,
                  mesh.declaredPath,
                  textureName
                );
              const asset =
                textureAssetsByKey[key];

              if (
                asset &&
                (
                  !asset.exists ||
                  Boolean(
                    asset.errorCode
                  )
                )
              ) {
                result.add(path);
                break;
              }
            }

            if (result.has(path)) {
              break;
            }
          }

          if (result.has(path)) {
            break;
          }
        }
      }

      return result;
    }, [
      geometryByPath,
      sceneryMetadataByPath,
      textureAssetsByKey
    ]);

  const splineProblemPaths =
    useMemo(() => {
      const result =
        new Set<string>();

      for (const [
        path,
        profile
      ] of Object.entries(
        splineProfilesByPath
      )) {
        if (!profile.exists) {
          result.add(path);
          continue;
        }

        for (const textureName of
          new Set(
            profile.textures.filter(
              Boolean
            )
          )) {
          const key =
            getSplineTextureAssetKey(
              path,
              textureName
            );
          const asset =
            textureAssetsByKey[key];

          if (
            asset &&
            (
              !asset.exists ||
              Boolean(asset.errorCode)
            )
          ) {
            result.add(path);
            break;
          }
        }
      }

      return result;
    }, [
      splineProfilesByPath,
      textureAssetsByKey
    ]);

  const mapHealthIssueCount =
    missingDependencyCount +
    sceneryProblemPaths.size +
    splineProblemPaths.size;

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

  const renderableMapGeometryCount =
    useMemo(
      () =>
        mapObjectPaths.filter(
          (path) =>
            Boolean(
              geometryByPath[path]?.tree
            ) ||
            Boolean(
              geometryByPath[
                path
              ]?.meshes.some(
                (mesh) =>
                  mesh.geometry.isLoaded &&
                  mesh.geometry.positions.length >
                    0 &&
                  mesh.geometry.indices.length >
                    0
              )
            )
        ).length,
      [
        geometryByPath,
        mapObjectPaths
      ]
    );

  const failedMapGeometryCount =
    Math.max(
      0,
      loadedMapGeometryCount -
        renderableMapGeometryCount
    );

  const geometryWarmupTotal =
    mapLoadMode === "full"
      ? mapObjectPaths.length
      : nearbyObjectPaths.length;

  const geometryWarmupCompleted =
    mapLoadMode === "full"
      ? loadedMapGeometryCount
      : loadedNearbyGeometryCount;

  const pendingTextureAssetCount =
    useMemo(
      () =>
        Object.keys(
          requestedTextureKeys
        ).filter(
          (key) =>
            !Object.hasOwn(
              textureAssetsByKey,
              key
            )
        ).length,
      [
        requestedTextureKeys,
        textureAssetsByKey
      ]
    );

  const pendingGroundTextureAssetCount =
    useMemo(
      () =>
        Object.keys(
          requestedGroundTextureKeys
        ).filter(
          (key) =>
            !Object.hasOwn(
              groundTextureAssetsByKey,
              key
            )
        ).length,
      [
        groundTextureAssetsByKey,
        requestedGroundTextureKeys
      ]
    );

  const pendingTerrainMaskAssetCount =
    useMemo(
      () =>
        Object.keys(
          requestedTerrainMaskKeys
        ).filter(
          (key) =>
            !Object.hasOwn(
              terrainMaskAssetsByKey,
              key
            )
        ).length,
      [
        requestedTerrainMaskKeys,
        terrainMaskAssetsByKey
      ]
    );

  const loadedTextureAssetCount =
    Object.values(
      textureAssetsByKey
    ).filter(
      (asset) =>
        asset.exists &&
        Boolean(
          asset.base64Data ||
          asset.rgbaBase64
        )
    ).length;

  const failedTextureAssets =
    Object.values(
      textureAssetsByKey
    ).filter(
      (asset) =>
        !asset.exists ||
        Boolean(asset.errorCode)
    );

  const failedTextureAssetCount =
    failedTextureAssets.length;

  const textureErrorSummary =
    Array.from(
      failedTextureAssets.reduce(
        (counts, asset) => {
          const code =
            asset.errorCode ??
            "semDados";

          counts.set(
            code,
            (counts.get(code) ?? 0) + 1
          );

          return counts;
        },
        new Map<string, number>()
      )
    )
      .sort(
        (left, right) =>
          right[1] - left[1] ||
          left[0].localeCompare(
            right[0]
          )
      )
      .slice(0, 4);

  const requestedVisualAssetCount =
    Object.keys(
      requestedTextureKeys
    ).length +
    Object.keys(
      requestedGroundTextureKeys
    ).length +
    Object.keys(
      requestedTerrainMaskKeys
    ).length;

  const completedVisualAssetCount =
    requestedVisualAssetCount -
    pendingTextureAssetCount -
    pendingGroundTextureAssetCount -
    pendingTerrainMaskAssetCount;

  const assetWarmupProgress = {
    completed:
      geometryWarmupCompleted +
      loadedSplineProfileCount +
      completedVisualAssetCount,
    total:
      geometryWarmupTotal +
      splinePathsForPreload.length +
      requestedVisualAssetCount
  };

  const assetWarmupHasPendingWork =
    geometryWarmupCompleted <
      geometryWarmupTotal ||
    loadedSplineProfileCount <
      splinePathsForPreload.length ||
    pendingTextureAssetCount > 0 ||
    pendingGroundTextureAssetCount > 0 ||
    pendingTerrainMaskAssetCount > 0 ||
    Boolean(preloadingGeometryFor) ||
    Boolean(preloadingSplineProfileFor) ||
    Boolean(loadingGeometryFor) ||
    Boolean(loadingSplineFor);

  useEffect(() => {
    if (
      !assetWarmupActive ||
      loadingFullMap ||
      Boolean(loadingRegionKey) ||
      assetWarmupHasPendingWork
    ) {
      return;
    }

    const handle =
      window.setTimeout(
        () =>
          setAssetWarmupActive(
            false
          ),
        650
      );

    return () =>
      window.clearTimeout(
        handle
      );
  }, [
    assetWarmupActive,
    assetWarmupHasPendingWork,
    loadingFullMap,
    loadingRegionKey
  ]);

  const unresolvedMapGeometryCount =
    Math.max(
      0,
      mapObjectPaths.length -
        loadedMapGeometryCount
    );

  const geometryDiagnosticPaths =
    mapLoadMode === "full"
      ? mapObjectPaths
      : nearbyObjectPaths;

  const loadedDiagnosticGeometryCount =
    geometryDiagnosticPaths.filter(
      (path) =>
        Object.hasOwn(
          geometryByPath,
          path
        )
    ).length;

  const renderableDiagnosticGeometryCount =
    geometryDiagnosticPaths.filter(
      (path) =>
        Boolean(
          geometryByPath[path]?.tree
        ) ||
        Boolean(
          geometryByPath[
            path
          ]?.meshes.some(
            (mesh) =>
              mesh.geometry.isLoaded &&
              mesh.geometry.positions.length >
                0 &&
              mesh.geometry.indices.length >
                0
          )
        )
    ).length;

  const failedDiagnosticGeometryCount =
    Math.max(
      0,
      loadedDiagnosticGeometryCount -
        renderableDiagnosticGeometryCount
    );

  const unresolvedDiagnosticGeometryCount =
    Math.max(
      0,
      geometryDiagnosticPaths.length -
        loadedDiagnosticGeometryCount
    );

  const treePlacementDiagnostics =
    useMemo(() => {
      let detected = 0;
      let exact = 0;
      let textureReady = 0;

      for (const placedObject of
        objectsForViewport) {
        const geometry =
          geometryByPath[
            placedObject
              .sceneryObjectPath
          ];

        if (!geometry?.tree) {
          continue;
        }

        detected += 1;

        const values =
          placedObject.extraValues;

        if (
          !values ||
          values.length < 4
        ) {
          continue;
        }

        const valueCount =
          Number.parseInt(
            values[0],
            10
          );

        const textureName =
          values[1]?.trim();

        const height =
          Number.parseFloat(
            values[2]
          );

        const aspect =
          Number.parseFloat(
            values[3]
          );

        if (
          valueCount < 4 ||
          !textureName ||
          !Number.isFinite(height) ||
          !Number.isFinite(aspect) ||
          height <= 0 ||
          aspect <= 0
        ) {
          continue;
        }

        exact += 1;

        const key =
          getSceneryTextureAssetKey(
            placedObject
              .sceneryObjectPath,
            sceneryTreeTextureMeshToken,
            textureName
          );

        if (
          textureAssetsByKey[
            key
          ]?.exists
        ) {
          textureReady += 1;
        }
      }

      return {
        detected,
        exact,
        textureReady
      };
    }, [
      geometryByPath,
      objectsForViewport,
      textureAssetsByKey
    ]);

  const o3dErrorSummary =
    useMemo(() => {
      const counts =
        new Map<string, number>();

      for (const path of
        geometryDiagnosticPaths) {
        const geometry =
          geometryByPath[path];

        if (!geometry) {
          continue;
        }

        for (const mesh of
          geometry.meshes) {
          if (
            geometry.tree &&
            mesh.declaredPath
              .toLowerCase()
              .endsWith(".x")
          ) {
            continue;
          }

          if (
            mesh.geometry.isLoaded &&
            mesh.geometry.positions.length >
              0 &&
            mesh.geometry.indices.length >
              0
          ) {
            continue;
          }

          const code =
            mesh.geometry.errorCode ??
            (mesh.geometry.isLoaded
              ? "emptyGeometry"
              : "notLoaded");

          counts.set(
            code,
            (counts.get(code) ?? 0) +
              1
          );
        }
      }

      return Array.from(
        counts.entries()
      )
        .sort(
          (left, right) =>
            right[1] - left[1] ||
            left[0].localeCompare(
              right[0]
            )
        )
        .slice(0, 5);
    }, [
      geometryByPath,
      geometryDiagnosticPaths
    ]);

  const protectedMeshCount =
    o3dErrorSummary
      .filter(
        ([code]) =>
          code === "encrypted" ||
          code.startsWith("protected")
      )
      .reduce(
        (total, [, count]) =>
          total + count,
        0
      );

  const protectedObjectPathCount =
    geometryDiagnosticPaths.filter(
      (path) =>
        geometryByPath[
          path
        ]?.meshes.some(
          (mesh) => {
            const code =
              mesh.geometry.errorCode;

            return Boolean(
              code === "encrypted" ||
              code?.startsWith(
                "protected"
              )
            );
          }
        )
    ).length;

  useEffect(() => {
    if (
      !bridgeAvailable ||
      mapLoadMode !== "full" ||
      loadingFullMap ||
      mapObjectPaths.length === 0
    ) {
      return;
    }

    const batch =
      mapObjectPaths
        .filter(
          (path) =>
            !Object.hasOwn(
              geometryByPath,
              path
            ) &&
            !requestedGeometryPathsRef.current.has(
              path
            )
        )
        .slice(
          0,
          geometryPreloadBatchSize
        );

    for (const path of batch) {
      requestedGeometryPathsRef.current.add(
        path
      );

      loadSceneryObjectGeometry(
        path
      );
    }
  }, [
    bridgeAvailable,
    geometryByPath,
    loadingFullMap,
    mapLoadMode,
    mapObjectPaths
  ]);

  const normalizedLibrarySearch =
    librarySearch.trim();

  const sceneryLibrarySearchEntries =
    useMemo(
      () =>
        normalizedLibrarySearch
          ? sceneryLibrary.filter(
              (entry) =>
                matchesSmartAssetSearch(
                  entry.fileName +
                    " " +
                    entry.sceneryObjectPath +
                    " " +
                    (
                      sceneryMetadataByPath[
                        entry.sceneryObjectPath
                      ]?.friendlyName ?? ""
                    ),
                  normalizedLibrarySearch
                )
            )
          : sceneryLibrary,
      [
        normalizedLibrarySearch,
        sceneryLibrary,
        sceneryMetadataByPath
      ]
    );

  const usedSceneryPaths =
    useMemo(
      () =>
        new Set(
          objects.map(
            (item) =>
              item.sceneryObjectPath
          )
        ),
      [objects]
    );

  const activeCollectionItems =
    libraryCollections[
      activeLibraryCollection
    ] ?? [];

  const sceneryLibraryEligibleEntries =
    useMemo(() => {
      const favoriteSet =
        new Set(sceneryFavorites);
      const recentIndex =
        new Map(
          sceneryRecent.map(
            (path, index) => [
              path,
              index
            ]
          )
        );
      const collectionSet =
        new Set(
          activeCollectionItems
            .filter((item) =>
              item.startsWith("sco:")
            )
            .map((item) =>
              item.slice(4)
            )
        );

      const entries =
        sceneryLibrarySearchEntries
          .filter((entry) => {
            const path =
              entry.sceneryObjectPath;

            if (
              sceneryLibraryView ===
                "favorites" &&
              !favoriteSet.has(path)
            ) {
              return false;
            }

            if (
              sceneryLibraryView ===
                "recent" &&
              !recentIndex.has(path)
            ) {
              return false;
            }

            if (
              sceneryLibraryView ===
                "frequent" &&
              !sceneryUsage[path]
            ) {
              return false;
            }

            if (
              sceneryLibraryView ===
                "collection" &&
              !collectionSet.has(path)
            ) {
              return false;
            }

            if (
              sceneryTechnicalFilter ===
                "used" &&
              !usedSceneryPaths.has(path)
            ) {
              return false;
            }

            if (
              sceneryTechnicalFilter ===
                "tree" &&
              !geometryByPath[path]?.tree
            ) {
              return false;
            }

            if (
              sceneryTechnicalFilter ===
                "loaded" &&
              !geometryByPath[path]
            ) {
              return false;
            }

            if (
              sceneryTechnicalFilter ===
                "problems" &&
              !sceneryProblemPaths.has(
                path
              )
            ) {
              return false;
            }

            return true;
          });

      if (
        sceneryLibraryView === "recent"
      ) {
        entries.sort(
          (left, right) =>
            (recentIndex.get(
              left.sceneryObjectPath
            ) ?? 9999) -
            (recentIndex.get(
              right.sceneryObjectPath
            ) ?? 9999)
        );
      } else if (
        sceneryLibraryView ===
        "frequent"
      ) {
        entries.sort(
          (left, right) =>
            (sceneryUsage[
              right.sceneryObjectPath
            ] ?? 0) -
            (sceneryUsage[
              left.sceneryObjectPath
            ] ?? 0)
        );
      }

      return entries;
    }, [
      activeCollectionItems,
      geometryByPath,
      sceneryFavorites,
      sceneryLibrarySearchEntries,
      sceneryLibraryView,
      sceneryProblemPaths,
      sceneryRecent,
      sceneryTechnicalFilter,
      sceneryUsage,
      usedSceneryPaths
    ]);

  const sceneryLibraryGroupCounts =
    useMemo(() => {
      const counts:
        Record<SceneryLibraryGroup, number> = {
          all:
            sceneryLibraryEligibleEntries.length,
          junctions: 0,
          bridges: 0,
          buildings: 0,
          vegetation: 0,
          transit: 0,
          street: 0,
          utilities: 0,
          other: 0
        };

      for (const entry of
        sceneryLibraryEligibleEntries) {
        const group =
          getSceneryLibraryGroup(
            entry,
            sceneryMetadataByPath[
              entry.sceneryObjectPath
            ],
            geometryByPath[
              entry.sceneryObjectPath
            ]
          );

        counts[group] += 1;
      }

      return counts;
    }, [
      geometryByPath,
      sceneryLibraryEligibleEntries,
      sceneryMetadataByPath
    ]);

  const scenerySubcategories =
    useMemo(() => {
      const values = new Set<string>();

      for (const entry of
        sceneryLibraryEligibleEntries) {
        const group =
          getSceneryLibraryGroup(
            entry,
            sceneryMetadataByPath[
              entry.sceneryObjectPath
            ],
            geometryByPath[
              entry.sceneryObjectPath
            ]
          );

        if (
          sceneryLibraryGroup !== "all" &&
          group !== sceneryLibraryGroup
        ) {
          continue;
        }

        values.add(
          getSceneryLibrarySubcategory(
            entry,
            group
          )
        );
      }

      return Array.from(values).sort(
        (left, right) =>
          left.localeCompare(
            right,
            "pt-BR"
          )
      );
    }, [
      geometryByPath,
      sceneryLibraryEligibleEntries,
      sceneryLibraryGroup,
      sceneryMetadataByPath
    ]);

  const filteredSceneryLibrary =
    useMemo(
      () =>
        sceneryLibraryEligibleEntries
          .filter((entry) => {
            const group =
              getSceneryLibraryGroup(
                entry,
                sceneryMetadataByPath[
                  entry.sceneryObjectPath
                ],
                geometryByPath[
                  entry.sceneryObjectPath
                ]
              );

            if (
              sceneryLibraryGroup !==
                "all" &&
              group !==
                sceneryLibraryGroup
            ) {
              return false;
            }

            return (
              scenerySubcategory ===
                "all" ||
              getSceneryLibrarySubcategory(
                entry,
                group
              ) ===
                scenerySubcategory
            );
          })
          .slice(0, 300),
      [
        geometryByPath,
        sceneryLibraryEligibleEntries,
        sceneryLibraryGroup,
        sceneryMetadataByPath,
        scenerySubcategory
      ]
    );

  const sceneryLibraryResultCount =
    filteredSceneryLibrary.length;

  const normalizedSplineLibrarySearch =
    splineLibrarySearch.trim();

  const splineLibrarySearchEntries =
    useMemo(
      () =>
        normalizedSplineLibrarySearch
          ? splineLibrary.filter(
              (entry) =>
                matchesSmartAssetSearch(
                  entry.fileName +
                    " " +
                    entry.splinePath,
                  normalizedSplineLibrarySearch
                )
            )
          : splineLibrary,
      [
        normalizedSplineLibrarySearch,
        splineLibrary
      ]
    );

  const usedSplinePaths =
    useMemo(
      () =>
        new Set(
          splines.map(
            (item) =>
              item.splinePath
          )
        ),
      [splines]
    );

  const splineLibraryEligibleEntries =
    useMemo(() => {
      const favoriteSet =
        new Set(splineFavorites);
      const recentIndex =
        new Map(
          splineRecent.map(
            (path, index) => [
              path,
              index
            ]
          )
        );
      const collectionSet =
        new Set(
          activeCollectionItems
            .filter((item) =>
              item.startsWith("sli:")
            )
            .map((item) =>
              item.slice(4)
            )
        );

      const entries =
        splineLibrarySearchEntries
          .filter((entry) => {
            const path =
              entry.splinePath;

            if (
              splineLibraryView ===
                "favorites" &&
              !favoriteSet.has(path)
            ) {
              return false;
            }

            if (
              splineLibraryView ===
                "recent" &&
              !recentIndex.has(path)
            ) {
              return false;
            }

            if (
              splineLibraryView ===
                "frequent" &&
              !splineUsage[path]
            ) {
              return false;
            }

            if (
              splineLibraryView ===
                "collection" &&
              !collectionSet.has(path)
            ) {
              return false;
            }

            if (
              splineTechnicalFilter ===
                "used" &&
              !usedSplinePaths.has(path)
            ) {
              return false;
            }

            if (
              splineTechnicalFilter ===
                "loaded" &&
              !splineProfilesByPath[path]
            ) {
              return false;
            }

            if (
              splineTechnicalFilter ===
                "problems" &&
              !splineProblemPaths.has(
                path
              )
            ) {
              return false;
            }

            return true;
          });

      if (
        splineLibraryView === "recent"
      ) {
        entries.sort(
          (left, right) =>
            (recentIndex.get(
              left.splinePath
            ) ?? 9999) -
            (recentIndex.get(
              right.splinePath
            ) ?? 9999)
        );
      } else if (
        splineLibraryView ===
        "frequent"
      ) {
        entries.sort(
          (left, right) =>
            (splineUsage[
              right.splinePath
            ] ?? 0) -
            (splineUsage[
              left.splinePath
            ] ?? 0)
        );
      }

      return entries;
    }, [
      activeCollectionItems,
      splineFavorites,
      splineLibrarySearchEntries,
      splineLibraryView,
      splineProblemPaths,
      splineProfilesByPath,
      splineRecent,
      splineTechnicalFilter,
      splineUsage,
      usedSplinePaths
    ]);

  const splineLibraryGroupCounts =
    useMemo(() => {
      const counts:
        Record<SplineLibraryGroup, number> = {
          all:
            splineLibraryEligibleEntries.length,
          roads: 0,
          paths: 0,
          rail: 0,
          bridges: 0,
          markings: 0,
          other: 0
        };

      for (const entry of
        splineLibraryEligibleEntries) {
        counts[
          getSplineLibraryGroup(entry)
        ] += 1;
      }

      return counts;
    }, [splineLibraryEligibleEntries]);

  const splineSubcategories =
    useMemo(() => {
      const values = new Set<string>();

      for (const entry of
        splineLibraryEligibleEntries) {
        const group =
          getSplineLibraryGroup(entry);

        if (
          splineLibraryGroup !== "all" &&
          group !== splineLibraryGroup
        ) {
          continue;
        }

        values.add(
          getSplineLibrarySubcategory(
            entry,
            group
          )
        );
      }

      return Array.from(values).sort(
        (left, right) =>
          left.localeCompare(
            right,
            "pt-BR"
          )
      );
    }, [
      splineLibraryEligibleEntries,
      splineLibraryGroup
    ]);

  const filteredSplineLibrary =
    useMemo(
      () =>
        splineLibraryEligibleEntries
          .filter((entry) => {
            const group =
              getSplineLibraryGroup(entry);

            if (
              splineLibraryGroup !==
                "all" &&
              group !==
                splineLibraryGroup
            ) {
              return false;
            }

            return (
              splineSubcategory ===
                "all" ||
              getSplineLibrarySubcategory(
                entry,
                group
              ) ===
                splineSubcategory
            );
          })
          .slice(0, 300),
      [
        splineLibraryEligibleEntries,
        splineLibraryGroup,
        splineSubcategory
      ]
    );

  const splineLibraryResultCount =
    filteredSplineLibrary.length;

  const sceneryPreviewTechnicalSummary =
    useMemo(() => {
      if (
        !sceneryLibraryPreviewAsset
      ) {
        return undefined;
      }

      const path =
        sceneryLibraryPreviewAsset
          .sceneryObjectPath;
      const geometry =
        geometryByPath[path];
      const metadata =
        sceneryMetadataByPath[path];

      if (!geometry) {
        return {
          meshCount: 0,
          loadedMeshCount: 0,
          failedMeshCount: 0,
          missingMeshFileCount:
            metadata?.meshes.filter(
              (mesh) =>
                !mesh.fileExists
            ).length ?? 0,
          collisionMeshCount:
            metadata?.collisionMeshes
              .length ?? 0,
          vertexCount: 0,
          triangleCount: 0,
          materialCount: 0,
          textureCount: 0,
          loadedTextureCount: 0,
          missingTextureCount: 0,
          pendingTextureCount: 0,
          unsupportedCommandCount: 0,
          health:
            "loading" as const
        };
      }

      const textureKeys =
        new Map<
          string,
          {
            key: string;
            textureName: string;
          }
        >();
      let loadedMeshCount = 0;
      let failedMeshCount = 0;
      let vertexCount = 0;
      let triangleCount = 0;
      let materialCount = 0;
      let unsupportedCommandCount =
        0;

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
            path,
            meshPath,
            textureName
          );

        textureKeys.set(key, {
          key,
          textureName
        });
      };

      queueTexture(
        sceneryTreeTextureMeshToken,
        geometry.tree?.textureName
      );

      for (const mesh of
        geometry.meshes) {
        if (
          mesh.geometry.isLoaded
        ) {
          loadedMeshCount += 1;
        } else {
          failedMeshCount += 1;
        }

        vertexCount +=
          Math.floor(
            mesh.geometry
              .positions.length / 3
          );
        triangleCount +=
          Math.floor(
            mesh.geometry
              .indices.length / 3
          );
        materialCount +=
          mesh.geometry
            .materials.length;

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
              ?.nightMapTextureName
          );
          queueTexture(
            mesh.declaredPath,
            materialOverride
              ?.environmentMapTextureName
          );
          queueTexture(
            mesh.declaredPath,
            getStaticTransparencyMapName(
              materialOverride
                ?.transMapSource
            )
          );

          unsupportedCommandCount +=
            materialOverride
              ?.unsupportedCommands
              .length ?? 0;
        }
      }

      let loadedTextureCount = 0;
      let missingTextureCount = 0;
      let pendingTextureCount = 0;

      for (const { key } of
        textureKeys.values()) {
        const asset =
          textureAssetsByKey[key];

        if (asset?.exists) {
          loadedTextureCount += 1;
        } else if (
          asset &&
          !asset.exists
        ) {
          missingTextureCount += 1;
        } else if (
          requestedTextureKeys[key]
        ) {
          pendingTextureCount += 1;
        }
      }

      const missingMeshFileCount =
        metadata?.meshes.filter(
          (mesh) =>
            !mesh.fileExists
        ).length ?? 0;

      const health =
        failedMeshCount > 0 ||
        missingMeshFileCount > 0 ||
        missingTextureCount > 0
          ? "error"
          : pendingTextureCount > 0 ||
              unsupportedCommandCount > 0
            ? "warning"
            : "ok";

      return {
        meshCount:
          geometry.meshes.length,
        loadedMeshCount,
        failedMeshCount,
        missingMeshFileCount,
        collisionMeshCount:
          metadata?.collisionMeshes
            .length ?? 0,
        vertexCount,
        triangleCount,
        materialCount,
        textureCount:
          textureKeys.size,
        loadedTextureCount,
        missingTextureCount,
        pendingTextureCount,
        unsupportedCommandCount,
        health
      };
    }, [
      geometryByPath,
      requestedTextureKeys,
      sceneryLibraryPreviewAsset,
      sceneryMetadataByPath,
      textureAssetsByKey
    ]);

  const splinePreviewTechnicalSummary =
    useMemo(() => {
      if (
        !splineLibraryPreviewAsset
      ) {
        return undefined;
      }

      const path =
        splineLibraryPreviewAsset
          .splinePath;
      const profile =
        splineProfilesByPath[path];

      if (!profile) {
        return {
          surfaceCount: 0,
          textureCount: 0,
          loadedTextureCount: 0,
          missingTextureCount: 0,
          pendingTextureCount: 0,
          alphaSurfaceCount: 0,
          width: 0,
          health:
            "loading" as const
        };
      }

      let minimumX =
        Number.POSITIVE_INFINITY;
      let maximumX =
        Number.NEGATIVE_INFINITY;
      let alphaSurfaceCount = 0;

      for (const surface of
        profile.surfaces) {
        minimumX = Math.min(
          minimumX,
          surface.from.x,
          surface.to.x
        );
        maximumX = Math.max(
          maximumX,
          surface.from.x,
          surface.to.x
        );

        if (surface.alphaMode !== 0) {
          alphaSurfaceCount += 1;
        }
      }

      const uniqueTextures =
        Array.from(
          new Set(
            profile.textures.filter(
              Boolean
            )
          )
        );

      let loadedTextureCount = 0;
      let missingTextureCount = 0;
      let pendingTextureCount = 0;

      for (const textureName of
        uniqueTextures) {
        const key =
          getSplineTextureAssetKey(
            path,
            textureName
          );
        const asset =
          textureAssetsByKey[key];

        if (asset?.exists) {
          loadedTextureCount += 1;
        } else if (
          asset &&
          !asset.exists
        ) {
          missingTextureCount += 1;
        } else if (
          requestedTextureKeys[key]
        ) {
          pendingTextureCount += 1;
        }
      }

      return {
        surfaceCount:
          profile.surfaces.length,
        textureCount:
          uniqueTextures.length,
        loadedTextureCount,
        missingTextureCount,
        pendingTextureCount,
        alphaSurfaceCount,
        width:
          Number.isFinite(minimumX) &&
          Number.isFinite(maximumX)
            ? Math.max(
                0,
                maximumX - minimumX
              )
            : 0,
        health:
          missingTextureCount > 0
            ? "error"
            : pendingTextureCount > 0
              ? "warning"
              : "ok"
      };
    }, [
      requestedTextureKeys,
      splineLibraryPreviewAsset,
      splineProfilesByPath,
      textureAssetsByKey
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

        const sourceExtension =
          textureAsset.sourceExtension
            ?.replace(".", "")
            .toUpperCase();

        return {
          label:
            extension &&
            sourceExtension &&
            extension !== sourceExtension
              ? `Carregada · ${extension} (origem ${sourceExtension})`
              : extension
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
          setPlacementAsset(undefined);
          setPendingPlacement(undefined);
          setPendingPlacementBatch([]);
          setPlacementLineStart(undefined);
          setInsertingObject(false);

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
          setEasyRoadMode(false);
          setEasyRoadStart(undefined);
          setEasyRoadEnd(undefined);
          setEasyRoadCurveOffset(0);
          setRoadStartSnap(undefined);
          setRoadEndSnap(undefined);
          setActiveConstructionTool(
            undefined
          );

          setSelectedSpline(undefined);
          setSelectionMode("object");
          setInspectorTab("transform");
          setEditorTool("select");

          if (isFullScreen) {
            setFullScreenPanel(
              "inspector"
            );
          } else {
            setDesktopInspectorOpen(
              true
            );
          }
        }

        setError(undefined);
      },
      [isFullScreen]
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
          setPlacementAsset(undefined);
          setPendingPlacement(undefined);
          setPendingPlacementBatch([]);
          setPlacementLineStart(undefined);
          setInsertingObject(false);

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
          setEasyRoadMode(false);
          setEasyRoadStart(undefined);
          setEasyRoadEnd(undefined);
          setEasyRoadCurveOffset(0);
          setRoadStartSnap(undefined);
          setRoadEndSnap(undefined);
          setActiveConstructionTool(
            undefined
          );

          setSelectedObject(undefined);
          setSelectionMode("spline");
          setInspectorTab("transform");
          setEditorTool("select");

          if (isFullScreen) {
            setFullScreenPanel(
              "inspector"
            );
          } else {
            setDesktopInspectorOpen(
              true
            );
          }
        }

        setError(undefined);
      },
      [
        isFullScreen,
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

  const handleLevelSelectedSplineToTerrain =
    useCallback(() => {
      if (!selectedSpline) {
        return;
      }

      const startHeight =
        sampleTerrainHeight(
          activeTiles,
          selectedSpline.tileX,
          selectedSpline.tileY,
          selectedSpline.x,
          selectedSpline.y
        );

      if (
        startHeight === undefined ||
        selectedSpline.length <= 0
      ) {
        setError(
          "Não há terreno carregado suficiente para nivelar esta rua."
        );
        return;
      }

      const endPoint =
        getSplineEndWorldPoint(
          selectedSpline
        );

      const endWorldX =
        endPoint.x;

      const endWorldY =
        endPoint.y;

      const endTileX =
        Math.floor(
          endWorldX / 300
        );
      const endTileY =
        Math.floor(
          endWorldY / 300
        );

      const endHeight =
        sampleTerrainHeight(
          activeTiles,
          endTileX,
          endTileY,
          endWorldX -
            endTileX * 300,
          endWorldY -
            endTileY * 300
        );

      if (endHeight === undefined) {
        setError(
          "O terreno do fim da rua não está carregado. Mova para o bloco vizinho ou use o mapa completo."
        );
        return;
      }

      const gradient =
        (
          (endHeight -
            startHeight) /
          selectedSpline.length
        ) *
        100;

      handlePreviewSplineTransform({
        ...selectedSpline,
        z: startHeight,
        gradientStart: gradient,
        gradientEnd: gradient
      });

      setEditorTool("move");
      setSaveNotice(
        `Rua nivelada pela altura real do terreno: ${formatNumber(startHeight)} → ${formatNumber(endHeight)} m.`
      );
      setError(undefined);
    }, [
      activeTiles,
      handlePreviewSplineTransform,
      selectedSpline
    ]);

  const handleLevelPendingRoadToTerrain =
    useCallback(() => {
      if (
        !pendingSplinePlacement ||
        pendingSplinePlacement.length <= 0
      ) {
        return;
      }

      const startHeight =
        sampleTerrainHeight(
          activeTiles,
          pendingSplinePlacement
            .targetTileX,
          pendingSplinePlacement
            .targetTileY,
          pendingSplinePlacement.x,
          pendingSplinePlacement.y
        );

      if (startHeight === undefined) {
        setError(
          "O terreno no início da rua não está carregado."
        );
        return;
      }

      const endPoint =
        getSplineEndWorldPoint({
          tileX:
            pendingSplinePlacement
              .targetTileX,
          tileY:
            pendingSplinePlacement
              .targetTileY,
          x:
            pendingSplinePlacement.x,
          y:
            pendingSplinePlacement.y,
          rotation:
            pendingSplinePlacement
              .rotation,
          length:
            pendingSplinePlacement
              .length,
          radius:
            pendingSplinePlacement
              .radius
        });

      const endWorldX =
        endPoint.x;

      const endWorldY =
        endPoint.y;

      const endTileX =
        Math.floor(
          endWorldX / 300
        );
      const endTileY =
        Math.floor(
          endWorldY / 300
        );

      const endHeight =
        sampleTerrainHeight(
          activeTiles,
          endTileX,
          endTileY,
          endWorldX -
            endTileX * 300,
          endWorldY -
            endTileY * 300
        );

      if (endHeight === undefined) {
        setError(
          "O terreno no fim da rua não está carregado."
        );
        return;
      }

      const gradient =
        (
          (endHeight -
            startHeight) /
          pendingSplinePlacement
            .length
        ) *
        100;

      setPendingSplinePlacement(
        (current) =>
          current
            ? {
                ...current,
                z: startHeight,
                gradientStart:
                  gradient,
                gradientEnd:
                  gradient
              }
            : current
      );

      setSaveNotice(
        `Nivelamento sugerido pelo terreno: ${formatNumber(startHeight)} → ${formatNumber(endHeight)} m.`
      );
      setError(undefined);
    }, [
      activeTiles,
      pendingSplinePlacement
    ]);

  const handleTerrainPoint =
    useCallback(
      (point: {
        tileX: number;
        tileY: number;
        x: number;
        y: number;
        height: number;
      }) => {
        setTerrainEditPoint(
          point
        );
        setTerrainTargetHeight(
          point.height
        );
        setGeorefAnchor({
          tileX: point.tileX,
          tileY: point.tileY,
          x: point.x,
          y: point.y
        });
        setInspectorTab(
          "transform"
        );
      },
      []
    );

  const handleLevelTerrain =
    useCallback(() => {
      if (
        !selectedMap ||
        !terrainEditPoint ||
        savingTerrain
      ) {
        return;
      }

      setSavingTerrain(true);
      setError(undefined);
      setSaveNotice(undefined);

      levelTerrain(
        selectedMap.directoryName,
        {
          tileX:
            terrainEditPoint.tileX,
          tileY:
            terrainEditPoint.tileY,
          x: terrainEditPoint.x,
          y: terrainEditPoint.y,
          targetHeight:
            terrainTargetHeight,
          radius:
            terrainBrushRadius,
          feather:
            terrainBrushFeather
        }
      );
    }, [
      savingTerrain,
      selectedMap,
      terrainBrushFeather,
      terrainBrushRadius,
      terrainEditPoint,
      terrainTargetHeight
    ]);

  const handleLoadGoogleReference =
    useCallback(() => {
      const latitude =
        Number(
          googleLatitude
            .replace(",", ".")
        );

      const longitude =
        Number(
          googleLongitude
            .replace(",", ".")
        );

      if (
        !bridgeAvailable ||
        !googleApiKey.trim() ||
        !Number.isFinite(
          latitude) ||
        !Number.isFinite(
          longitude)
      ) {
        setError(
          "Informe a chave da API do Google e coordenadas válidas."
        );
        return;
      }

      if (activeTile) {
        setGeorefAnchor(
          (current) => ({
            ...current,
            tileX:
              activeTile.x,
            tileY:
              activeTile.y
          })
        );
      }

      setLoadingGoogleReference(
        true
      );
      setError(undefined);
      setSaveNotice(undefined);

      loadGoogleMapReference(
        googleApiKey.trim(),
        {
          latitude,
          longitude,
          zoom: googleZoom,
          mapType:
            googleMapType,
          width: 640,
          height: 640
        }
      );
    }, [
      activeTile,
      bridgeAvailable,
      googleApiKey,
      googleLatitude,
      googleLongitude,
      googleMapType,
      googleZoom
    ]);

  const handleLoadElevationGrid =
    useCallback(() => {
      if (
        !googleReference ||
        !activeTile ||
        !googleApiKey.trim() ||
        loadingElevationGrid
      ) {
        setError(
          "Carregue a referência do Google e mantenha um tile ativo antes de buscar o relevo."
        );
        return;
      }

      setLoadingElevationGrid(true);
      setGoogleElevationGrid(undefined);
      setError(undefined);
      setSaveNotice(undefined);

      loadGoogleElevationGrid(
        googleApiKey.trim(),
        {
          latitude:
            googleReference.latitude,
          longitude:
            googleReference.longitude,
          anchorTileX:
            georefAnchor.tileX,
          anchorTileY:
            georefAnchor.tileY,
          anchorX:
            georefAnchor.x,
          anchorY:
            georefAnchor.y,
          tileX:
            activeTile.x,
          tileY:
            activeTile.y,
          sampleCount:
            elevationSampleCount
        }
      );
    }, [
      activeTile,
      elevationSampleCount,
      georefAnchor,
      googleApiKey,
      googleReference,
      loadingElevationGrid
    ]);

  const handleApplyElevationGrid =
    useCallback(() => {
      if (
        !selectedMap ||
        !googleElevationGrid ||
        applyingElevationGrid
      ) {
        return;
      }

      const confirmed =
        window.confirm(
          `Aplicar o relevo real ao tile ${googleElevationGrid.tileX},${googleElevationGrid.tileY}?\n\nO arquivo .terrain atual será salvo em backup antes da alteração.`
        );

      if (!confirmed) {
        return;
      }

      setApplyingElevationGrid(true);
      setError(undefined);
      setSaveNotice(undefined);

      applyTerrainElevationGrid(
        selectedMap.directoryName,
        {
          tileX:
            googleElevationGrid.tileX,
          tileY:
            googleElevationGrid.tileY,
          rows:
            googleElevationGrid.rows,
          columns:
            googleElevationGrid.columns,
          elevations:
            googleElevationGrid
              .elevations,
          verticalOffset:
            elevationVerticalOffset
        }
      );
    }, [
      applyingElevationGrid,
      elevationVerticalOffset,
      googleElevationGrid,
      selectedMap
    ]);

  const handleSaveMapGeoreference =
    useCallback(() => {
      if (
        !selectedMap ||
        !googleReference
      ) {
        return;
      }

      saveMapGeoreference(
        selectedMap.directoryName,
        {
          latitude:
            googleReference
              .latitude,
          longitude:
            googleReference
              .longitude,
          anchorTileX:
            georefAnchor.tileX,
          anchorTileY:
            georefAnchor.tileY,
          anchorX:
            georefAnchor.x,
          anchorY:
            georefAnchor.y,
          zoom:
            googleReference.zoom,
          mapType:
            googleReference
              .mapType
        }
      );
    }, [
      georefAnchor,
      googleReference,
      selectedMap
    ]);

  const referenceOverlay =
    useMemo(
      () =>
        googleReference &&
        referenceVisible
          ? {
              base64Data:
                googleReference
                  .base64Data,
              mimeType:
                googleReference
                  .mimeType,
              width:
                googleReference.width,
              height:
                googleReference.height,
              metersPerPixel:
                googleReference
                  .metersPerPixel,
              anchorWorldX:
                georefAnchor.tileX *
                  300 +
                georefAnchor.x,
              anchorWorldZ:
                georefAnchor.tileY *
                  300 +
                georefAnchor.y,
              opacity:
                referenceOpacity,
              attribution:
                googleReference
                  .attribution
            }
          : undefined,
      [
        georefAnchor,
        googleReference,
        referenceOpacity,
        referenceVisible
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

        setSplineRecent((current) => [
          entry.splinePath,
          ...current.filter(
            (item) =>
              item !== entry.splinePath
          )
        ].slice(0, 24));
        setSplineUsage((current) => ({
          ...current,
          [entry.splinePath]:
            (current[
              entry.splinePath
            ] ?? 0) + 1
        }));
        setSplineLibraryPreviewAsset(
          entry
        );
        setSplineLibraryPlacementAsset(
          entry
        );

        setSplineLibraryPlacementIsHeight(
          isHeightSpline
        );

        const template:
          OmsiPlacedSpline = {
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
          };

        setSplinePlacementTemplate(
          template
        );

        setEasyRoadStart(undefined);
        setEasyRoadEnd(undefined);
        setEasyRoadCurveOffset(0);

        setPendingSplinePlacement(
          easyRoadMode &&
          !isHeightSpline
            ? undefined
            : activeTile
              ? {
                  targetTileX:
                    activeTile.x,
                  targetTileY:
                    activeTile.y,
                  x: 150,
                  y: 150,
                  z: 0,
                  rotation: 0,
                  length: 20,
                  radius: 0,
                  gradientStart: 0,
                  gradientEnd: 0
                }
              : undefined
        );
        setSelectedSpline(undefined);
        setSelectedObject(undefined);
        setEditorTool("select");
        setShowSplines(true);
        setSaveNotice(undefined);
        setError(undefined);

        if (isFullScreen) {
          setFullScreenPanel(
            "inspector"
          );
        } else {
          setDesktopInspectorOpen(
            true
          );
        }

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
        activeTile,
        easyRoadMode,
        isFullScreen,
        placementAsset,
        previewEditCount,
        selectedMap,
        splinePreviewEditCount,
        splineProfilesByPath
      ]
    );

  const updateEasyRoadPreview =
    useCallback(
      (
        start: RoadPoint,
        end: RoadPoint,
        curveOffset: number
      ) => {
        if (!splinePlacementTemplate) {
          return false;
        }

        const arc =
          deriveRoadArc(
            start,
            end,
            curveOffset
          );

        if (
          !arc ||
          arc.chordLength < 0.25
        ) {
          return false;
        }

        const baseStartHeight =
          sampleTerrainHeight(
            activeTiles,
            start.targetTileX,
            start.targetTileY,
            start.x,
            start.y
          ) ??
          splinePlacementTemplate.z;

        const baseEndHeight =
          sampleTerrainHeight(
            activeTiles,
            end.targetTileX,
            end.targetTileY,
            end.x,
            end.y
          ) ??
          baseStartHeight;

        const elevation =
          roadPlacementKind ===
            "bridge"
            ? roadElevationOffset
            : 0;

        const startHeight =
          baseStartHeight +
          elevation;

        const endHeight =
          baseEndHeight +
          elevation;

        const gradient =
          (
            (
              endHeight -
              startHeight
            ) /
            Math.max(
              0.001,
              arc.length
            )
          ) *
          100;

        setPendingSplinePlacement({
          ...start,
          z: startHeight,
          rotation: arc.rotation,
          length: arc.length,
          radius: arc.radius,
          gradientStart: gradient,
          gradientEnd: gradient
        });

        setError(undefined);
        return true;
      },
      [
        activeTiles,
        roadElevationOffset,
        roadPlacementKind,
        splinePlacementTemplate
      ]
    );

  const handleEasyRoadCurveChange =
    useCallback(
      (curveOffset: number) => {
        setEasyRoadCurveOffset(
          curveOffset
        );

        if (
          easyRoadStart &&
          easyRoadEnd
        ) {
          updateEasyRoadPreview(
            easyRoadStart,
            easyRoadEnd,
            curveOffset
          );
        }
      },
      [
        easyRoadEnd,
        easyRoadStart,
        updateEasyRoadPreview
      ]
    );

  const handleRoadElevationOffsetChange =
    useCallback(
      (nextValue: number) => {
        if (!Number.isFinite(nextValue)) {
          return;
        }

        const value =
          Math.max(
            -20,
            Math.min(
              100,
              Math.round(
                nextValue * 2
              ) / 2
            )
          );

        setRoadElevationOffset(
          value
        );

        if (
          !easyRoadStart ||
          !easyRoadEnd
        ) {
          return;
        }

        const arc =
          deriveRoadArc(
            easyRoadStart,
            easyRoadEnd,
            easyRoadCurveOffset
          );

        if (!arc) {
          return;
        }

        const baseStart =
          sampleTerrainHeight(
            activeTiles,
            easyRoadStart.targetTileX,
            easyRoadStart.targetTileY,
            easyRoadStart.x,
            easyRoadStart.y
          ) ?? 0;

        const baseEnd =
          sampleTerrainHeight(
            activeTiles,
            easyRoadEnd.targetTileX,
            easyRoadEnd.targetTileY,
            easyRoadEnd.x,
            easyRoadEnd.y
          ) ??
          baseStart;

        const startZ =
          baseStart + value;
        const endZ =
          baseEnd + value;
        const gradient =
          (
            (
              endZ -
              startZ
            ) /
            Math.max(
              0.001,
              arc.length
            )
          ) *
          100;

        setPendingSplinePlacement({
          ...easyRoadStart,
          z: startZ,
          rotation: arc.rotation,
          length: arc.length,
          radius: arc.radius,
          gradientStart: gradient,
          gradientEnd: gradient
        });
      },
      [
        activeTiles,
        easyRoadCurveOffset,
        easyRoadEnd,
        easyRoadStart
      ]
    );

  const handleRoadControlPointChange =
    useCallback(
      (
        control: "start" | "end",
        point: RoadPoint
      ) => {
        if (
          !easyRoadStart ||
          !easyRoadEnd
        ) {
          return;
        }

        const snap =
          roadEndpointSnapEnabled
            ? findRoadEndpointSnap(
                point,
                splinesForViewport,
                roadEndpointSnapDistance
              )
            : undefined;
        const resolved =
          snap?.point ??
          point;

        const nextStart =
          control === "start"
            ? resolved
            : easyRoadStart;
        const nextEnd =
          control === "end"
            ? resolved
            : easyRoadEnd;

        if (control === "start") {
          setEasyRoadStart(
            resolved
          );
          setRoadStartSnap(snap);
        } else {
          setEasyRoadEnd(
            resolved
          );
          setRoadEndSnap(snap);
        }

        updateEasyRoadPreview(
          nextStart,
          nextEnd,
          easyRoadCurveOffset
        );
      },
      [
        easyRoadCurveOffset,
        easyRoadEnd,
        easyRoadStart,
        roadEndpointSnapDistance,
        roadEndpointSnapEnabled,
        splinesForViewport,
        updateEasyRoadPreview
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

        if (
          easyRoadMode &&
          !splineLibraryPlacementIsHeight
        ) {
          const snap =
            roadEndpointSnapEnabled
              ? findRoadEndpointSnap(
                  point,
                  splinesForViewport,
                  roadEndpointSnapDistance
                )
              : undefined;
          const resolvedPoint =
            snap?.point ??
            point;

          if (!easyRoadStart) {
            const startHeight =
              (
                sampleTerrainHeight(
                  activeTiles,
                  resolvedPoint.targetTileX,
                  resolvedPoint.targetTileY,
                  resolvedPoint.x,
                  resolvedPoint.y
                ) ??
                splinePlacementTemplate.z
              ) +
              (
                roadPlacementKind ===
                  "bridge"
                  ? roadElevationOffset
                  : 0
              );

            setEasyRoadStart(
              resolvedPoint
            );
            setEasyRoadEnd(undefined);
            setRoadStartSnap(snap);
            setRoadEndSnap(undefined);
            setEasyRoadCurveOffset(0);

            setPendingSplinePlacement({
              ...resolvedPoint,
              z: startHeight,
              rotation: 0,
              length: 0,
              radius: 0,
              gradientStart: 0,
              gradientEnd: 0
            });

            setSaveNotice(
              snap
                ? `Início encaixado na ${snap.endpoint === "start" ? "ponta inicial" : "ponta final"} da spline #${snap.splineId}. Arraste até o fim da nova rua.`
                : "Início marcado. Continue segurando e arraste até o fim da rua; depois ajuste a curva pelo controle."
            );
            return;
          }

          setEasyRoadEnd(
            resolvedPoint
          );
          setRoadEndSnap(snap);

          const updated =
            updateEasyRoadPreview(
              easyRoadStart,
              resolvedPoint,
              easyRoadCurveOffset
            );

          if (!updated) {
            setError(
              "O ponto final precisa estar afastado do início da rua."
            );
          }

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
      [
        activeTiles,
        easyRoadCurveOffset,
        easyRoadMode,
        easyRoadStart,
        roadElevationOffset,
        roadEndpointSnapDistance,
        roadEndpointSnapEnabled,
        roadPlacementKind,
        splineLibraryPlacementIsHeight,
        splinesForViewport,
        splinePlacementTemplate,
        updateEasyRoadPreview
      ]
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
      setEasyRoadMode(false);
      setEasyRoadStart(undefined);
      setEasyRoadEnd(undefined);
      setEasyRoadCurveOffset(0);
      setRoadStartSnap(undefined);
      setRoadEndSnap(undefined);
      setRoadPlacementKind("road");
      setRoadElevationOffset(0);
      pendingConstructionSetRef.current =
        undefined;
      pendingConstructionSetLabelRef.current =
        undefined;
      pendingRoadAutoLinkRef.current =
        undefined;
      pendingConstructionSetAfterLinkRef.current =
        undefined;
      pendingConstructionSetSplineRef.current =
        undefined;
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

      const insertionSplinePath =
        splineLibraryPlacementAsset
          ?.splinePath ??
        splinePlacementTemplate
          .splinePath;

      pendingConstructionSetRef.current =
        applyConstructionSet &&
        activeConstructionSet &&
        activeConstructionSet
          .splinePath &&
        normalizeAssetClassifierText(
          activeConstructionSet
            .splinePath
        ) ===
          normalizeAssetClassifierText(
            insertionSplinePath
          ) &&
        activeConstructionSet
          .companions.length > 0
          ? activeConstructionSet
          : undefined;

      pendingRoadAutoLinkRef.current =
        easyRoadMode &&
        roadAutoConnectEnabled &&
        (
          roadAutoLinkPlan
            .previousSplineId !==
            -1 ||
          roadAutoLinkPlan
            .nextSplineId !==
            -1
        )
          ? {
              previousSplineId:
                roadAutoLinkPlan
                  .previousSplineId,
              nextSplineId:
                roadAutoLinkPlan
                  .nextSplineId
            }
          : undefined;

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
      activeConstructionSet,
      applyConstructionSet,
      easyRoadMode,
      insertingSpline,
      pendingSplinePlacement,
      roadAutoConnectEnabled,
      roadAutoLinkPlan,
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

  const focusTile =
    useCallback(
      (
        tileX: number,
        tileY: number
      ) => {
        if (
          !selectedMap?.tiles.some(
            (tile) =>
              tile.x === tileX &&
              tile.y === tileY
          )
        ) {
          return;
        }

        setActiveTile({
          x: tileX,
          y: tileY
        });

        setTerrainEditPoint(
          undefined
        );
        setGoogleElevationGrid(
          undefined
        );
        setSelectedObject(
          undefined
        );
        setSelectedSpline(
          undefined
        );

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

        setCameraAction(
          (current) => ({
            type: "tile",
            token:
              (current?.token ?? 0) + 1,
            tileX,
            tileY
          })
        );
      },
      [
        mapLoadMode,
        selectedMap
      ]
    );

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
        requestFullScreen(
          !isFullScreen
        );
        return;
      }

      if (
        event.key === "Escape" &&
        isFullScreen
      ) {
        event.preventDefault();
        requestFullScreen(false);
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

  const handleCreateConstructionSet =
    useCallback(() => {
      const name =
        newConstructionSetName
          .trim();

      if (!name) {
        return;
      }

      const id =
        "set-" +
        Date.now().toString(36);

      const next:
        ConstructionSetDefinition = {
        id,
        name,
        splinePath:
          splineLibraryPreviewAsset
            ?.splinePath ??
          null,
        companions: []
      };

      setConstructionSets(
        (current) => [
          ...current,
          next
        ]
      );
      setActiveConstructionSetId(
        id
      );
      setNewConstructionSetName("");
      setShowConstructionSetPanel(
        true
      );
    }, [
      newConstructionSetName,
      splineLibraryPreviewAsset
    ]);

  const updateActiveConstructionSet =
    useCallback(
      (
        update: (
          current:
            ConstructionSetDefinition
        ) =>
          ConstructionSetDefinition
      ) => {
        if (
          !activeConstructionSetId
        ) {
          return;
        }

        setConstructionSets(
          (current) =>
            current.map((set) =>
              set.id ===
              activeConstructionSetId
                ? update(set)
                : set
            )
        );
      },
      [activeConstructionSetId]
    );

  const handleDeleteConstructionSet =
    useCallback(() => {
      if (
        !activeConstructionSetId
      ) {
        return;
      }

      setConstructionSets(
        (current) =>
          current.filter(
            (set) =>
              set.id !==
              activeConstructionSetId
          )
      );
      setActiveConstructionSetId(
        ""
      );
    }, [activeConstructionSetId]);

  const handleUsePreviewSplineInSet =
    useCallback(() => {
      if (
        !splineLibraryPreviewAsset ||
        !activeConstructionSetId
      ) {
        return;
      }

      updateActiveConstructionSet(
        (current) => ({
          ...current,
          splinePath:
            splineLibraryPreviewAsset
              .splinePath
        })
      );
    }, [
      activeConstructionSetId,
      splineLibraryPreviewAsset,
      updateActiveConstructionSet
    ]);

  const handleAddPreviewObjectToSet =
    useCallback(() => {
      if (
        !sceneryLibraryPreviewAsset ||
        !activeConstructionSetId
      ) {
        return;
      }

      updateActiveConstructionSet(
        (current) => {
          if (
            current.companions
              .length >= 16
          ) {
            return current;
          }

          return {
            ...current,
            companions: [
              ...current.companions,
              {
                id:
                  "comp-" +
                  Date.now()
                    .toString(36),
                sceneryObjectPath:
                  sceneryLibraryPreviewAsset
                    .sceneryObjectPath,
                spacing: 20,
                lateralOffset: 6,
                side: "both",
                rotationOffset: 0
              }
            ]
          };
        }
      );
    }, [
      activeConstructionSetId,
      sceneryLibraryPreviewAsset,
      updateActiveConstructionSet
    ]);

  const handleUpdateConstructionSetCompanion =
    useCallback(
      (
        companionId: string,
        patch:
          Partial<
            ConstructionSetCompanion
          >
      ) => {
        updateActiveConstructionSet(
          (current) => ({
            ...current,
            companions:
              current.companions.map(
                (companion) =>
                  companion.id ===
                  companionId
                    ? {
                        ...companion,
                        ...patch
                      }
                    : companion
              )
          })
        );
      },
      [updateActiveConstructionSet]
    );

  const handleRemoveConstructionSetCompanion =
    useCallback(
      (companionId: string) => {
        updateActiveConstructionSet(
          (current) => ({
            ...current,
            companions:
              current.companions.filter(
                (companion) =>
                  companion.id !==
                  companionId
              )
          })
        );
      },
      [updateActiveConstructionSet]
    );

  const toggleSceneryFavorite =
    useCallback((path: string) => {
      setSceneryFavorites((current) =>
        current.includes(path)
          ? current.filter(
              (item) => item !== path
            )
          : [path, ...current]
      );
    }, []);

  const toggleSplineFavorite =
    useCallback((path: string) => {
      setSplineFavorites((current) =>
        current.includes(path)
          ? current.filter(
              (item) => item !== path
            )
          : [path, ...current]
      );
    }, []);

  const registerSceneryLibraryUse =
    useCallback(
      (
        path: string,
        incrementUsage: boolean
      ) => {
        setSceneryRecent((current) => [
          path,
          ...current.filter(
            (item) => item !== path
          )
        ].slice(0, 24));

        if (incrementUsage) {
          setSceneryUsage(
            (current) => ({
              ...current,
              [path]:
                (current[path] ?? 0) +
                1
            })
          );
        }
      },
      []
    );

  const registerSplineLibraryUse =
    useCallback(
      (
        path: string,
        incrementUsage: boolean
      ) => {
        setSplineRecent((current) => [
          path,
          ...current.filter(
            (item) => item !== path
          )
        ].slice(0, 24));

        if (incrementUsage) {
          setSplineUsage(
            (current) => ({
              ...current,
              [path]:
                (current[path] ?? 0) +
                1
            })
          );
        }
      },
      []
    );

  const handleCreateLibraryCollection =
    useCallback(() => {
      const name =
        newCollectionName.trim();

      if (!name) {
        return;
      }

      setLibraryCollections(
        (current) => ({
          ...current,
          [name]: current[name] ?? []
        })
      );
      setActiveLibraryCollection(name);
      setNewCollectionName("");
    }, [newCollectionName]);

  const toggleAssetInCollection =
    useCallback(
      (assetKey: string) => {
        if (!activeLibraryCollection) {
          return;
        }

        setLibraryCollections(
          (current) => {
            const items =
              current[
                activeLibraryCollection
              ] ?? [];
            const next =
              items.includes(assetKey)
                ? items.filter(
                    (item) =>
                      item !== assetKey
                  )
                : [...items, assetKey];

            return {
              ...current,
              [activeLibraryCollection]:
                next
            };
          }
        );
      },
      [activeLibraryCollection]
    );

  const handleBuildActiveConstructionSet =
    useCallback(() => {
      if (
        !activeConstructionSet ||
        !activeConstructionSet
          .splinePath
      ) {
        setError(
          "Defina uma spline .sli para o conjunto."
        );
        return;
      }

      if (
        activeConstructionSetMissingTemplates
          .length > 0
      ) {
        setError(
          "O conjunto usa objeto(s) sem template preservativo no mapa atual: " +
            activeConstructionSetMissingTemplates
              .slice(0, 3)
              .join(", ")
        );
        return;
      }

      const entry =
        splineLibrary.find(
          (candidate) =>
            normalizeAssetClassifierText(
              candidate.splinePath
            ) ===
            normalizeAssetClassifierText(
              activeConstructionSet
                .splinePath!
            )
        );

      if (!entry) {
        setError(
          "A spline do conjunto não está disponível na biblioteca atual."
        );
        return;
      }

      setActiveConstructionTool(
        "road"
      );
      setRoadPlacementKind(
        "road"
      );
      setRoadElevationOffset(0);
      setEasyRoadMode(true);
      setEasyRoadStart(undefined);
      setEasyRoadEnd(undefined);
      setEasyRoadCurveOffset(0);
      setRoadStartSnap(undefined);
      setRoadEndSnap(undefined);
      setSelectionMode("spline");
      setShowSplines(true);
      handleSelectSplineLibraryAsset(
        entry,
        false
      );
      setSaveNotice(
        `Conjunto "${activeConstructionSet.name}" ativo. Desenhe a via; os objetos companheiros serão aplicados depois que a spline for salva.`
      );
    }, [
      activeConstructionSet,
      activeConstructionSetMissingTemplates,
      handleSelectSplineLibraryAsset,
      splineLibrary
    ]);

  const handleAssetThumbnail =
    useCallback(
      (
        assetKey: string,
        dataUrl: string
      ) => {
        setAssetThumbnailCache(
          (current) => {
            if (
              current[assetKey] ===
              dataUrl
            ) {
              return current;
            }

            const next = {
              ...current,
              [assetKey]: dataUrl
            };
            const keys =
              Object.keys(next);

            while (keys.length > 48) {
              const oldest =
                keys.shift();

              if (oldest) {
                delete next[oldest];
              }
            }

            return next;
          }
        );
      },
      []
    );

  const handlePreviewSceneryLibraryAsset =
    useCallback(
      (entry: SceneryLibraryEntry) => {
        registerSceneryLibraryUse(
          entry.sceneryObjectPath,
          false
        );
        setSceneryLibraryPreviewAsset(
          entry
        );

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

        if (
          !Object.hasOwn(
            sceneryMetadataByPath,
            entry.sceneryObjectPath
          )
        ) {
          loadSceneryObjectMetadata(
            entry.sceneryObjectPath
          );
        }
      },
      [
        geometryByPath,
        registerSceneryLibraryUse,
        sceneryMetadataByPath
      ]
    );

  const handlePreviewSplineLibraryAsset =
    useCallback(
      (entry: SplineLibraryEntry) => {
        registerSplineLibraryUse(
          entry.splinePath,
          false
        );
        setSplineLibraryPreviewAsset(
          entry
        );

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
        registerSplineLibraryUse,
        splineProfilesByPath
      ]
    );

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

        registerSceneryLibraryUse(
          entry.sceneryObjectPath,
          true
        );
        setPlacementTransformDefaults(
          transformDefaults
        );
        setSceneryLibraryPreviewAsset(
          entry
        );
        setPlacementAsset(entry);
        setPendingPlacementBatch([]);
        setPlacementLineStart(undefined);
        setPendingPlacement(
          activeConstructionTool ===
              "junction" &&
            junctionPlacementTarget
            ? {
                ...junctionPlacementTarget,
                ...transformDefaults,
                rotation:
                  junctionPlacementTarget
                    .rotation
              }
            : activeTile
              ? {
                  tileX: activeTile.x,
                  tileY: activeTile.y,
                  x: 150,
                  y: 150,
                  ...transformDefaults
                }
              : undefined
        );
        setSelectedObject(undefined);
        setSelectedSpline(undefined);
        setEditorTool("select");
        setShowObjects(true);
        setError(undefined);

        if (isFullScreen) {
          setFullScreenPanel(
            "inspector"
          );
        } else {
          setDesktopInspectorOpen(
            true
          );
        }

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

        if (
          !Object.hasOwn(
            sceneryMetadataByPath,
            entry.sceneryObjectPath
          )
        ) {
          loadSceneryObjectMetadata(
            entry.sceneryObjectPath
          );
        }
      },
      [
        activeConstructionTool,
        activeTile,
        geometryByPath,
        isFullScreen,
        junctionPlacementTarget,
        registerSceneryLibraryUse,
        sceneryMetadataByPath,
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

  const handleUseJunctionSuggestion =
    useCallback(
      (suggestion: JunctionSuggestion) => {
        const target:
          PendingObjectPlacement = {
            tileX:
              suggestion.tileX,
            tileY:
              suggestion.tileY,
            x: suggestion.x,
            y: suggestion.y,
            z:
              placementTransformDefaults.z,
            rotation:
              suggestion.rotation,
            pitch:
              placementTransformDefaults
                .pitch,
            bank:
              placementTransformDefaults
                .bank
          };

        setJunctionPlacementTarget(
          target
        );
        setActiveTile({
          x: suggestion.tileX,
          y: suggestion.tileY
        });

        if (placementAsset) {
          setPendingPlacement(
            target
          );
        }

        setSaveNotice(
          "Cruzamento sugerido entre splines #" +
            suggestion.splineA +
            " e #" +
            suggestion.splineB +
            ". Escolha/posicione um .sco real compatível."
        );
      },
      [
        placementAsset,
        placementTransformDefaults
      ]
    );

  useEffect(() => {
    const handleDuplicateShortcut = (
      event: KeyboardEvent
    ) => {
      const target =
        event.target as
          | HTMLElement
          | null;

      if (
        !(event.ctrlKey ||
          event.metaKey) ||
        event.key.toLowerCase() !==
          "d" ||
        target?.isContentEditable ||
        target?.tagName === "INPUT" ||
        target?.tagName === "TEXTAREA" ||
        target?.tagName === "SELECT" ||
        !selectedObject
      ) {
        return;
      }

      event.preventDefault();
      handlePlaceSelectedObjectCopy();
    };

    window.addEventListener(
      "keydown",
      handleDuplicateShortcut
    );

    return () =>
      window.removeEventListener(
        "keydown",
        handleDuplicateShortcut
      );
  }, [
    handlePlaceSelectedObjectCopy,
    selectedObject
  ]);

  const handleLibraryAssetDrop =
    useCallback(
      (payload: {
        kind: "object" | "spline";
        assetPath: string;
        point: {
          tileX: number;
          tileY: number;
          x: number;
          y: number;
        };
      }) => {
        if (payload.kind === "object") {
          const entry =
            sceneryLibrary.find(
              (candidate) =>
                candidate.sceneryObjectPath ===
                payload.assetPath
            );

          if (!entry) {
            return;
          }

          handleSelectPlacementAsset(
            entry
          );
          setPendingPlacement({
            tileX: payload.point.tileX,
            tileY: payload.point.tileY,
            x: payload.point.x,
            y: payload.point.y,
            ...defaultPlacementTransform
          });
          return;
        }

        const entry =
          splineLibrary.find(
            (candidate) =>
              candidate.splinePath ===
              payload.assetPath
          );

        if (!entry) {
          return;
        }

        handleSelectSplineLibraryAsset(
          entry,
          false
        );
        setEasyRoadMode(false);
        setPendingSplinePlacement({
          targetTileX:
            payload.point.tileX,
          targetTileY:
            payload.point.tileY,
          x: payload.point.x,
          y: payload.point.y,
          z: 0,
          rotation: 0,
          length: 20,
          radius: 0,
          gradientStart: 0,
          gradientEnd: 0
        });
      },
      [
        handleSelectPlacementAsset,
        handleSelectSplineLibraryAsset,
        sceneryLibrary,
        splineLibrary
      ]
    );

  const applyConstructionPreset =
    useCallback(
      (presetId: ConstructionPresetId) => {
        setConstructionPreset(presetId);

        const preset =
          constructionPresets.find(
            (candidate) =>
              candidate.id ===
              presetId
          );

        if (!preset) {
          return;
        }

        if (preset.mode) {
          setPlacementMode(
            preset.mode
          );
        }
        if (
          preset.spacing !==
          undefined
        ) {
          setPlacementSpacing(
            preset.spacing
          );
        }
        if (
          preset.brushRadius !==
          undefined
        ) {
          setPlacementBrushRadius(
            preset.brushRadius
          );
        }
        if (
          preset.brushCount !==
          undefined
        ) {
          setPlacementBrushCount(
            preset.brushCount
          );
        }
        if (
          preset.matrixRows !==
          undefined
        ) {
          setPlacementMatrixRows(
            preset.matrixRows
          );
        }
        if (
          preset.matrixColumns !==
          undefined
        ) {
          setPlacementMatrixColumns(
            preset.matrixColumns
          );
        }
        if (
          preset.matrixSpacingX !==
          undefined
        ) {
          setPlacementMatrixSpacingX(
            preset.matrixSpacingX
          );
        }
        if (
          preset.matrixSpacingY !==
          undefined
        ) {
          setPlacementMatrixSpacingY(
            preset.matrixSpacingY
          );
        }
        if (
          preset.lotSetback !==
          undefined
        ) {
          setPlacementLotSetback(
            preset.lotSetback
          );
        }
        if (
          preset.randomRotation !==
          undefined
        ) {
          setPlacementRandomRotation(
            preset.randomRotation
          );
        }
        if (
          preset.alignRoad !==
          undefined
        ) {
          setPlacementAlignRoad(
            preset.alignRoad
          );
        }

        setPendingPlacementBatch([]);
        setPlacementLineStart(
          undefined
        );
      },
      []
    );

  const handlePlacementPoint =
    useCallback(
      (
        placement:
          PendingObjectPlacement
      ) => {
        let next: PendingObjectPlacement = {
          ...placement,
          ...placementTransformDefaults
        };

        if (placementAlignRoad) {
          next =
            snapPlacementToNearestRoad(
              next,
              splines,
              placementRoadSnapDistance
            );
        }

        const validTiles =
          new Set(
            selectedMap?.tiles.map(
              (tile) =>
                `${tile.x}:${tile.y}`
            ) ?? []
          );
        const keepOnMap = (
          item: PendingObjectPlacement
        ) =>
          validTiles.size === 0 ||
          validTiles.has(
            `${item.tileX}:${item.tileY}`
          );

        if (placementMode === "single") {
          setPlacementLineStart(
            undefined
          );
          setPendingPlacementBatch([]);
          setPendingPlacement(next);
          return;
        }

        if (placementMode === "repeat") {
          setPendingPlacement(next);
          setPendingPlacementBatch(
            (current) => [
              ...current,
              next
            ].slice(-256)
          );
          return;
        }

        if (
          placementMode === "line" ||
          placementMode === "lot"
        ) {
          if (!placementLineStart) {
            setPlacementLineStart(next);
            setPendingPlacement(next);
            setPendingPlacementBatch([
              next
            ]);
            setSaveNotice(
              "Linha: ponto inicial marcado. Clique no ponto final."
            );
            return;
          }

          const batch =
            (
              placementMode === "lot"
                ? buildLotPlacements(
                    placementLineStart,
                    next,
                    placementSpacing,
                    placementLotSetback,
                    placementRandomRotation
                  )
                : buildLinePlacements(
                    placementLineStart,
                    next,
                    placementSpacing,
                    placementRandomRotation
                  )
            ).filter(keepOnMap);

          setPendingPlacement(next);
          setPendingPlacementBatch(
            batch
          );
          setSaveNotice(
            placementMode === "lot"
              ? `Lotes prontos: ${batch.length} objeto(s).`
              : `Linha pronta: ${batch.length} objeto(s).`
          );
          return;
        }

        if (
          placementMode === "matrix"
        ) {
          const batch =
            buildMatrixPlacements(
              next,
              placementMatrixRows,
              placementMatrixColumns,
              placementMatrixSpacingX,
              placementMatrixSpacingY,
              placementRandomRotation
            ).filter(keepOnMap);

          setPlacementLineStart(
            undefined
          );
          setPendingPlacement(next);
          setPendingPlacementBatch(
            batch
          );
          setSaveNotice(
            `Matriz pronta: ${batch.length} objeto(s).`
          );
          return;
        }

        if (
          placementMode === "circle"
        ) {
          const batch =
            buildCirclePlacements(
              next,
              placementCircleRadius,
              placementCircleCount,
              placementCircleTangent,
              placementRandomRotation
            ).filter(keepOnMap);

          setPlacementLineStart(
            undefined
          );
          setPendingPlacement(next);
          setPendingPlacementBatch(
            batch
          );
          setSaveNotice(
            `Círculo pronto: ${batch.length} objeto(s).`
          );
          return;
        }

        const batch =
          buildAreaPlacements(
            next,
            placementBrushRadius,
            placementBrushCount,
            placementRandomRotation
          ).filter(keepOnMap);

        setPlacementLineStart(
          undefined
        );
        setPendingPlacement(next);
        setPendingPlacementBatch(
          batch
        );
        setSaveNotice(
          `Pincel pronto: ${batch.length} objeto(s) em raio de ${formatNumber(placementBrushRadius)} m.`
        );
      },
      [
        placementAlignRoad,
        placementBrushCount,
        placementBrushRadius,
        placementCircleCount,
        placementCircleRadius,
        placementCircleTangent,
        placementLineStart,
        placementLotSetback,
        placementMatrixColumns,
        placementMatrixRows,
        placementMatrixSpacingX,
        placementMatrixSpacingY,
        placementMode,
        placementRandomRotation,
        placementRoadSnapDistance,
        placementSpacing,
        placementTransformDefaults,
        selectedMap,
        splines
      ]
    );

  const handleCancelPlacement =
    useCallback(() => {
      setPlacementAsset(undefined);
      setPendingPlacement(undefined);
      setPendingPlacementBatch([]);
      setPlacementLineStart(undefined);
      batchKeepPlacementRef.current =
        false;
      batchPlacementAssetRef.current =
        undefined;
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

      const placements =
        placementMode === "single"
          ? [pendingPlacement]
          : pendingPlacementBatch
              .length > 0
            ? pendingPlacementBatch
            : [pendingPlacement];

      setInsertingObject(true);
      setSaveNotice(undefined);
      setError(undefined);

      if (placements.length > 1) {
        batchKeepPlacementRef.current =
          placementMode !== "single";
        batchPlacementAssetRef.current =
          placementAsset;

        insertObjectBatch(
          selectedMap.directoryName,
          placementAsset
            .sceneryObjectPath,
          placements
        );
        return;
      }

      insertObject(
        selectedMap.directoryName,
        placementAsset
          .sceneryObjectPath,
        placements[0]
      );
    }, [
      insertingObject,
      pendingPlacement,
      pendingPlacementBatch,
      placementAsset,
      placementCanPersist,
      placementMode,
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

        if (isFullScreen) {
          setFullScreenPanel(
            "explorer"
          );
        } else {
          setDesktopExplorerOpen(
            true
          );
        }

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
        isFullScreen,
        loadingSceneryLibrary,
        loadingSplineLibrary,
        sceneryLibrary.length,
        splineLibrary.length
      ]
    );

  const handleUndoConstruction =
    useCallback(() => {
      if (
        restoringConstruction ||
        !selectedMap ||
        constructionUndoStack.length ===
          0
      ) {
        return;
      }

      const entry =
        constructionUndoStack[
          constructionUndoStack.length -
            1
        ];

      constructionRestoreModeRef.current =
        "undo";
      constructionRestoreEntryRef.current =
        entry;
      setRestoringConstruction(true);
      setError(undefined);
      setSaveNotice(
        "Restaurando: " +
          entry.label
      );

      restoreMapStudioBackup(
        selectedMap.directoryName,
        entry.backupDirectory
      );
    }, [
      constructionUndoStack,
      restoringConstruction,
      selectedMap
    ]);

  const handleRedoConstruction =
    useCallback(() => {
      if (
        restoringConstruction ||
        !selectedMap ||
        constructionRedoStack.length ===
          0
      ) {
        return;
      }

      const entry =
        constructionRedoStack[
          constructionRedoStack.length -
            1
        ];

      constructionRestoreModeRef.current =
        "redo";
      constructionRestoreEntryRef.current =
        entry;
      setRestoringConstruction(true);
      setError(undefined);
      setSaveNotice(
        "Refazendo: " +
          entry.label
      );

      restoreMapStudioBackup(
        selectedMap.directoryName,
        entry.backupDirectory
      );
    }, [
      constructionRedoStack,
      restoringConstruction,
      selectedMap
    ]);

  const handleSelectMissingDependency =
    useCallback(
      (
        kind: "object" | "spline",
        path: string
      ) => {
        setDependencyReplacementTarget({
          kind,
          path
        });
        setError(undefined);

        if (kind === "object") {
          setSceneryLibraryGroup("all");
          setScenerySubcategory("all");
          setLibrarySearch("");
          handleExplorerPanelTab(
            "library"
          );
        } else {
          setSplineLibraryGroup("all");
          setSplineSubcategory("all");
          setSplineLibrarySearch("");
          handleExplorerPanelTab(
            "splineLibrary"
          );
        }

        if (isFullScreen) {
          setFullScreenPanel(
            "explorer"
          );
        }

        setSaveNotice(
          "Escolha um " +
            (
              kind === "object"
                ? ".sco"
                : ".sli"
            ) +
            " real na biblioteca e carregue sua prévia para usar como substituto."
        );
      },
      [
        handleExplorerPanelTab,
        isFullScreen
      ]
    );

  const handleReplaceMissingDependency =
    useCallback(() => {
      if (
        !selectedMap ||
        !dependencyReplacementTarget ||
        replacingDependency
      ) {
        return;
      }

      const replacementPath =
        dependencyReplacementTarget
          .kind === "object"
          ? sceneryLibraryPreviewAsset
              ?.sceneryObjectPath
          : splineLibraryPreviewAsset
              ?.splinePath;

      if (!replacementPath) {
        setError(
          "Selecione e carregue a prévia de um asset real da biblioteca antes de substituir."
        );
        return;
      }

      if (
        normalizeAssetClassifierText(
          replacementPath
        ) ===
        normalizeAssetClassifierText(
          dependencyReplacementTarget
            .path
        )
      ) {
        setError(
          "O substituto precisa ser diferente da dependência ausente."
        );
        return;
      }

      setReplacingDependency(true);
      setError(undefined);
      setSaveNotice(
        "Substituindo referências com backup transacional..."
      );

      replaceMapAssetPath(
        selectedMap.directoryName,
        dependencyReplacementTarget
          .kind,
        dependencyReplacementTarget
          .path,
        replacementPath
      );
    }, [
      dependencyReplacementTarget,
      replacingDependency,
      sceneryLibraryPreviewAsset,
      selectedMap,
      splineLibraryPreviewAsset
    ]);

  const handleAuditDependencies =
    useCallback(() => {
      if (!bridgeAvailable) {
        setError(
          "A verificação de dependências exige o aplicativo desktop."
        );
        return;
      }

      if (
        !sceneryLibraryReady &&
        !loadingSceneryLibrary
      ) {
        setLoadingSceneryLibrary(
          true
        );
        loadSceneryLibrary();
      }

      if (
        !splineLibraryReady &&
        !loadingSplineLibrary
      ) {
        setLoadingSplineLibrary(
          true
        );
        loadSplineLibrary();
      }

      setSaveNotice(
        "Verificando .sco e .sli usados pelo mapa contra a instalação atual do OMSI."
      );
    }, [
      bridgeAvailable,
      loadingSceneryLibrary,
      loadingSplineLibrary,
      sceneryLibraryReady,
      splineLibraryReady
    ]);

  const openQuickCreate =
    useCallback(
      (tool: QuickCreateTool) => {
        if (!selectedMap) {
          setError(
            "Abra um mapa antes de usar as ferramentas de criação."
          );
          setView("map");
          return;
        }

        setView("editor");
        setEditorTool("select");
        setActiveConstructionTool(
          tool
        );
        if (tool !== "junction") {
          setJunctionPlacementTarget(
            undefined
          );
        }
        setError(undefined);

        if (isFullScreen) {
          setFullScreenPanel(
            "explorer"
          );
        }

        if (
          tool === "road" ||
          tool === "bridge"
        ) {
          const isBridge =
            tool === "bridge";

          setRoadPlacementKind(
            isBridge
              ? "bridge"
              : "road"
          );
          setRoadElevationOffset(
            isBridge ? 5 : 0
          );
          setEasyRoadMode(true);
          setEasyRoadStart(undefined);
          setEasyRoadEnd(undefined);
          setEasyRoadCurveOffset(0);
          setRoadStartSnap(undefined);
          setRoadEndSnap(undefined);
          setSelectionMode("spline");
          setShowSplines(true);
          setSplineLibrarySearch("");
          setSplineLibraryView("groups");
          setSplineTechnicalFilter("all");
          setSplineSubcategory("all");
          setSplineLibraryGroup(
            isBridge
              ? "bridges"
              : "roads"
          );
          handleExplorerPanelTab(
            "splineLibrary"
          );
          setSaveNotice(
            isBridge
              ? "Ponte/elevado: escolha uma spline real de ponte/túnel, arraste início/fim e ajuste a elevação."
              : "Criador de rua: escolha uma spline .sli real, clique no início e arraste até o fim. Depois use o controle de curva para ajustar o traçado."
          );
          return;
        }

        setEasyRoadMode(false);
        setEasyRoadStart(undefined);
        setEasyRoadEnd(undefined);
        setEasyRoadCurveOffset(0);
        setRoadStartSnap(undefined);
        setRoadEndSnap(undefined);
        setRoadPlacementKind("road");
        setRoadElevationOffset(0);

        if (tool === "terrain") {
          setSelectionMode("terrain");
          setShowTerrain(true);
          setExplorerPanelTab("map");
          setSaveNotice(
            "Modo terreno: clique diretamente no terreno para marcar o ponto e usar o nivelamento manual com backup."
          );
          return;
        }

        setSelectionMode("object");
        setShowObjects(true);
        setLibrarySearch("");
        setSceneryLibraryView("groups");
        setSceneryTechnicalFilter("all");
        setScenerySubcategory("all");
        setSceneryLibraryGroup(
          tool === "junction"
            ? "junctions"
            : tool === "building"
              ? "buildings"
              : tool === "transit"
                ? "transit"
                : tool === "street"
                  ? "street"
                  : tool === "utilities"
                    ? "utilities"
                    : tool === "tree" ||
                        tool === "grass"
                      ? "vegetation"
                      : "all"
        );
        handleExplorerPanelTab("library");

        const label =
          tool === "junction"
            ? "cruzamento"
            : tool === "building"
              ? "prédio/casa"
              : tool === "transit"
                ? "item de transporte"
                : tool === "street"
                  ? "mobiliário urbano"
                  : tool === "utilities"
                    ? "infraestrutura"
                    : tool === "water"
                      ? "água"
                      : tool === "grass"
                        ? "grama"
                        : tool === "tree"
                          ? "árvore"
                          : "objeto";

        setSaveNotice(
          `Criar ${label}: escolha um .sco real da Biblioteca e use Colocar.`
        );
      },
      [
        handleExplorerPanelTab,
        isFullScreen,
        selectedMap
      ]
    );

  useEffect(() => {
    const handleConstructionShortcut = (
      event: KeyboardEvent
    ) => {
      if (
        !event.altKey ||
        event.ctrlKey ||
        event.metaKey
      ) {
        return;
      }

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

      const selectionShortcut:
        Record<string, SelectionMode> = {
          "1": "all",
          "2": "object",
          "3": "spline",
          "4": "terrain"
        };

      if (
        Object.hasOwn(
          selectionShortcut,
          key
        )
      ) {
        event.preventDefault();
        setSelectionMode(
          selectionShortcut[key]
        );
        setEditorTool("select");
        return;
      }

      const createShortcut:
        Record<string, QuickCreateTool> = {
          r: "road",
          c: "junction",
          o: "object",
          t: "terrain",
          a: "water",
          g: "grass",
          y: "tree"
        };

      if (
        Object.hasOwn(
          createShortcut,
          key
        )
      ) {
        event.preventDefault();
        openQuickCreate(
          createShortcut[key]
        );
      }
    };

    window.addEventListener(
      "keydown",
      handleConstructionShortcut
    );

    return () =>
      window.removeEventListener(
        "keydown",
        handleConstructionShortcut
      );
  }, [openQuickCreate]);

  const normalizedMapSearch =
    mapSearch
      .trim()
      .toLocaleLowerCase("pt-BR");

  const filteredAvailableMaps =
    availableMaps.filter(
      (entry) =>
        !normalizedMapSearch ||
        (
          entry.displayName +
          " " +
          entry.directoryName +
          " " +
          entry.directoryPath
        )
          .toLocaleLowerCase(
            "pt-BR"
          )
          .includes(
            normalizedMapSearch
          )
    );

  const handleRefreshMapCatalog = () => {
    if (
      !rootPath ||
      !bridgeAvailable
    ) {
      return;
    }

    setLoadingMapCatalog(true);
    setMapCatalogProgress(undefined);
    setError(undefined);
    loadMapCatalog();
  };

  const handleCreateCoordinateMap = () => {
    if (
      !rootPath ||
      !bridgeAvailable ||
      creatingCoordinateMap
    ) {
      return;
    }

    const latitude =
      Number(
        newMapLatitude
          .replace(",", ".")
      );

    const longitude =
      Number(
        newMapLongitude
          .replace(",", ".")
      );

    if (
      !newMapDisplayName.trim() ||
      !newMapDirectoryName.trim() ||
      !Number.isFinite(latitude) ||
      !Number.isFinite(longitude) ||
      latitude < -90 ||
      latitude > 90 ||
      longitude < -180 ||
      longitude > 180
    ) {
      setError(
        errorMessages
          .invalidCoordinateMapRequest
      );
      return;
    }

    setCreatingCoordinateMap(true);
    setError(undefined);
    setSaveNotice(undefined);

    createCoordinateMap({
      directoryName:
        newMapDirectoryName.trim(),
      displayName:
        newMapDisplayName.trim(),
      latitude,
      longitude
    });
  };

  const handleOpenCatalogMap = (
    entry: OmsiMapCatalogEntry
  ) => {
    setSelectingMap(true);
    setError(undefined);
    openMapFromCatalog(
      entry.directoryName
    );
  };

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
    interactionLocked ||
    saving ||
    savingSpline ||
    savingSplineLinks ||
    insertingObject ||
    deletingObject ||
    insertingSpline ||
    deletingSpline;

  const loadingOverlay = (() => {
    if (selectingRoot) {
      return {
        title: "Conectando ao OMSI 2",
        detail:
          "Validando a instalação e preparando a lista real de mapas."
      };
    }

    if (creatingCoordinateMap) {
      return {
        title: "Criando mapa real",
        detail:
          "Copiando o template NewMap da instalação, gravando a âncora de coordenadas e validando o projeto."
      };
    }

    if (selectingMap) {
      return {
        title: "Abrindo mapa",
        detail:
          "Lendo global.cfg e validando a estrutura do mapa antes de liberar a edição."
      };
    }

    if (loadingFullMap) {
      const completed =
        fullMapProgress?.completed ?? 0;
      const total =
        fullMapProgress?.total ??
        selectedMap?.tiles.length ??
        0;

      return {
        title: "Carregando mapa completo",
        detail:
          selectedMap
            ? `Preparando ${selectedMap.displayName} para edição.`
            : "Preparando tiles, objetos, splines e terreno.",
        completed,
        total
      };
    }

    if (loadingRegionKey) {
      return {
        title: "Carregando área ativa",
        detail:
          "Atualizando a área ativa em alta prioridade e o anel externo em modo leve. A edição será liberada quando a região central estiver consistente."
      };
    }

    if (assetWarmupActive) {
      return {
        title: "Finalizando renderização do mapa",
        detail:
          mapLoadMode === "full"
            ? "Carregando malhas O3D/.x, perfis SLI e texturas reais do mapa completo."
            : "Carregando malhas O3D/.x, perfis SLI e texturas reais da área ativa; o anel externo permanece em metadata leve.",
        completed:
          assetWarmupProgress.completed,
        total:
          assetWarmupProgress.total
      };
    }

    if (loadingSceneryLibrary) {
      return {
        title: "Carregando biblioteca de objetos",
        detail:
          "Lendo Sceneryobjects instalados no OMSI 2."
      };
    }

    if (loadingSplineLibrary) {
      return {
        title: "Carregando biblioteca de splines",
        detail:
          "Lendo Splines instaladas no OMSI 2."
      };
    }

    return undefined;
  })();

  const loadingPercentage =
    loadingOverlay &&
    "completed" in loadingOverlay &&
    "total" in loadingOverlay &&
    typeof loadingOverlay.completed ===
      "number" &&
    typeof loadingOverlay.total ===
      "number" &&
    loadingOverlay.total > 0
      ? Math.min(
          100,
          Math.max(
            0,
            Math.round(
              (loadingOverlay.completed /
                loadingOverlay.total) *
                100
            )
          )
        )
      : undefined;

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
        <button
          type="button"
          className="sidebar-collapse-button"
          onClick={() =>
            setSidebarCollapsed(
              (current) => !current
            )
          }
          title={
            sidebarCollapsed
              ? "Expandir barra lateral"
              : "Recolher barra lateral"
          }
          aria-label={
            sidebarCollapsed
              ? "Expandir barra lateral"
              : "Recolher barra lateral"
          }
        >
          {sidebarCollapsed ? "»" : "«"}
        </button>
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
          Abra sua instalação, escolha o mapa na
          lista e edite dados reais com validação,
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
            Veja a lista real de mapas instalados
            em <code>maps</code> e escolha qual deseja
            abrir no editor.
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

          {rootPath && (
            <div
              className={
                assetIndexStatus.state ===
                  "ready"
                  ? "status-message success"
                  : "status-message"
              }
            >
              <strong>
                {assetIndexStatus.state ===
                "indexing"
                  ? "Indexando assets locais…"
                  : assetIndexStatus.state ===
                      "ready"
                    ? "✓ Índice local pronto"
                    : assetIndexStatus.state ===
                        "error"
                      ? "Índice local indisponível"
                      : "Preparando índice local"}
              </strong>
              <span>
                {assetIndexStatus.state ===
                "indexing"
                  ? `${assetIndexStatus.candidateFiles} asset(s) candidato(s) em ${assetIndexStatus.examinedFiles} arquivo(s) examinados. O editor pode continuar sendo usado.`
                  : assetIndexStatus.state ===
                      "ready"
                    ? `${assetIndexStatus.totalEntries} assets · SCO ${assetIndexStatus.sceneryObjects} · SLI ${assetIndexStatus.splines} · modelos ${assetIndexStatus.models} · texturas ${assetIndexStatus.textures} · novos ${assetIndexStatus.addedFiles} · alterados ${assetIndexStatus.updatedFiles} · removidos ${assetIndexStatus.removedFiles}`
                    : assetIndexStatus.state ===
                        "error"
                      ? "O cache falhou, mas a leitura direta do OMSI continua disponível."
                      : "O índice persistente será atualizado em segundo plano."}
              </span>
            </div>
          )}
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
          O Map Studio lista os mapas instalados no
          OMSI. Escolha o mapa pelo nome, sem precisar
          navegar manualmente pelas pastas.
        </p>
      </div>

      {!rootPath ? (
        <article className="setup-card centered">
          <h2>Selecione o OMSI primeiro</h2>
          <p>
            Antes de listar os mapas, precisamos
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
        <div className="map-catalog-shell">
          <article className="setup-card map-catalog-card">
            <div className="map-catalog-header">
              <div>
                <span className="card-kicker">
                  MAPAS INSTALADOS
                </span>
                <h2>Escolha o mapa</h2>
                <p>
                  Fonte real: <code>{rootPath}{"\\maps"}</code>
                </p>
              </div>

              <button
                type="button"
                className="secondary-action"
                onClick={
                  handleRefreshMapCatalog
                }
                disabled={
                  loadingMapCatalog ||
                  selectingMap
                }
              >
                {loadingMapCatalog
                  ? "Atualizando..."
                  : "Atualizar lista"}
              </button>
            </div>

            <div className="map-catalog-search">
              <input
                type="search"
                value={mapSearch}
                onChange={(event) =>
                  setMapSearch(
                    event.target.value
                  )
                }
                placeholder="Buscar pelo nome ou pasta do mapa..."
              />
              <span>
                {loadingMapCatalog
                  ? mapCatalogProgress?.total
                    ? `${mapCatalogProgress.completed}/${mapCatalogProgress.total} analisados`
                    : "Procurando mapas..."
                  : `${filteredAvailableMaps.length} de ${availableMaps.length} mapa(s)`}
              </span>
            </div>

            <div className="map-catalog-list">
              {filteredAvailableMaps.map(
                (entry) => (
                  <button
                    type="button"
                    key={
                      entry.directoryName
                    }
                    className={
                      selectedMap
                        ?.directoryName ===
                      entry.directoryName
                        ? "map-catalog-entry active"
                        : "map-catalog-entry"
                    }
                    onClick={() =>
                      handleOpenCatalogMap(
                        entry
                      )
                    }
                    disabled={
                      selectingMap
                    }
                  >
                    <span className="map-catalog-symbol">
                      M
                    </span>
                    <span className="map-catalog-copy">
                      <strong>
                        {entry.displayName}
                      </strong>
                      <small>
                        {entry.directoryName}
                        {" · "}
                        {entry.tileCount} tiles
                        {entry.usesWorldCoordinates
                          ? " · worldcoordinates"
                          : ""}
                      </small>
                      <small
                        title={
                          entry.directoryPath
                        }
                      >
                        {entry.directoryPath}
                      </small>
                    </span>
                    <span className="map-catalog-open">
                      Abrir →
                    </span>
                  </button>
                )
              )}

              {!loadingMapCatalog &&
                filteredAvailableMaps.length ===
                  0 && (
                  <div className="map-catalog-empty">
                    <strong>
                      Nenhum mapa encontrado
                    </strong>
                    <span>
                      Verifique se existem pastas com
                      global.cfg dentro de OMSI 2{"\\maps"}.
                    </span>
                  </div>
                )}
            </div>

            {mapCatalogProgress &&
              mapCatalogProgress.skipped >
                0 && (
                <div className="catalog-warning">
                  {mapCatalogProgress.skipped} mapa(s)
                  não puderam ser lidos e foram
                  ignorados com segurança.
                </div>
              )}
          </article>

          <aside className="info-card map-catalog-info">
            <h3>Mapa atual</h3>

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
                  onClick={() =>
                    setView("editor")
                  }
                >
                  Voltar ao editor →
                </button>
              </div>
            ) : (
              <div className="status-message">
                <strong>
                  Escolha um mapa da lista
                </strong>
                <span>
                  O mapa só é aberto depois do seu
                  clique.
                </span>
              </div>
            )}

            <div className="manual-map-fallback">
              <span>
                Se um mapa não aparecer na lista:
              </span>
              <button
                type="button"
                onClick={handleOpenMap}
                disabled={
                  selectingMap ||
                  loadingMapCatalog
                }
              >
                Abrir pasta manualmente
              </button>
            </div>

            <div className="coordinate-map-creator">
              <div>
                <strong>Criar mapa real</strong>
                <span>
                  Usa o template NewMap da sua própria instalação do OMSI e grava uma âncora de coordenadas do Map Studio.
                </span>
              </div>

              <label>
                <span>Nome do mapa</span>
                <input
                  type="text"
                  value={newMapDisplayName}
                  placeholder="Minha cidade"
                  onChange={(event) =>
                    setNewMapDisplayName(
                      event.target.value
                    )
                  }
                />
              </label>

              <label>
                <span>Pasta em maps</span>
                <input
                  type="text"
                  value={newMapDirectoryName}
                  placeholder="Minha_Cidade"
                  onChange={(event) =>
                    setNewMapDirectoryName(
                      event.target.value
                    )
                  }
                />
              </label>

              <div className="coordinate-map-row">
                <label>
                  <span>Latitude</span>
                  <input
                    type="text"
                    inputMode="decimal"
                    value={newMapLatitude}
                    placeholder="-23.5505"
                    onChange={(event) =>
                      setNewMapLatitude(
                        event.target.value
                      )
                    }
                  />
                </label>

                <label>
                  <span>Longitude</span>
                  <input
                    type="text"
                    inputMode="decimal"
                    value={newMapLongitude}
                    placeholder="-46.6333"
                    onChange={(event) =>
                      setNewMapLongitude(
                        event.target.value
                      )
                    }
                  />
                </label>
              </div>

              <button
                type="button"
                className="primary-button"
                disabled={
                  creatingCoordinateMap
                }
                onClick={
                  handleCreateCoordinateMap
                }
              >
                {creatingCoordinateMap
                  ? "Criando..."
                  : "Criar e abrir mapa"}
              </button>

              <small>
                O projeto é criado a partir do template oficial instalado. A referência visual e o relevo real podem ser carregados no editor depois.
              </small>
            </div>
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
          <dt>Malhas renderizáveis</dt>
          <dd>
            {mapLoadMode === "full"
              ? `${renderableMapGeometryCount}/${mapObjectPaths.length} · falhas ${failedMapGeometryCount} · pendentes ${unresolvedMapGeometryCount}`
              : `${renderableDiagnosticGeometryCount}/${nearbyObjectPaths.length} · falhas ${failedDiagnosticGeometryCount} · pendentes ${unresolvedDiagnosticGeometryCount}`}
          </dd>
        </div>
        <div>
          <dt>Árvores [tree]</dt>
          <dd>
            {`${treePlacementDiagnostics.textureReady}/${treePlacementDiagnostics.detected} renderizadas · dados exatos ${treePlacementDiagnostics.exact}`}
          </dd>
        </div>
        <div>
          <dt>Malhas não renderizadas</dt>
          <dd>
            {o3dErrorSummary.length > 0
              ? o3dErrorSummary
                  .map(
                    ([code, count]) =>
                      `${code === "encrypted" || code.startsWith("protected") ? "O3D protegido" : code}: ${count}`
                  )
                  .join(" · ")
              : loadedDiagnosticGeometryCount > 0
                ? "Nenhum erro conhecido"
                : "Aguardando leitura"}
          </dd>
        </div>
        {protectedMeshCount > 0 && (
          <div>
            <dt>O3D protegidos</dt>
            <dd>
              {protectedObjectPathCount} tipos de objeto · {protectedMeshCount} malhas
            </dd>
          </div>
        )}
        <div>
          <dt>Texturas de objetos/splines</dt>
          <dd>
            {`${loadedTextureAssetCount} carregadas · ${failedTextureAssetCount} falhas · ${pendingTextureAssetCount} pendentes`}
          </dd>
        </div>
        {textureErrorSummary.length > 0 && (
          <div>
            <dt>Falhas de textura</dt>
            <dd>
              {textureErrorSummary
                .map(
                  ([code, count]) =>
                    `${code}: ${count}`
                )
                .join(" · ")}
            </dd>
          </div>
        )}
        <div>
          <dt>Perfis SLI reais</dt>
          <dd>
            {`${loadedSplineProfileCount}/${splinePathsForPreload.length} · ${splineProfileDiagnostics.surfaces} superfícies · ausentes ${splineProfileDiagnostics.missing} · vazios ${splineProfileDiagnostics.empty}`}
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
                ? activeTileDetails.terrainTextureMasks!
                    .map((mask) => {
                      const layer =
                        selectedMap
                          ?.groundTextures[
                            mask.layerIndex
                          ];

                      const expected =
                        layer?.maskResolution;

                      if (!mask.isValid) {
                        return `${mask.layerIndex}: inválida (${mask.errorCode ?? "erro"})`;
                      }

                      if (
                        expected !== null &&
                        expected !== undefined &&
                        (mask.width !==
                          expected ||
                          mask.height !==
                            expected)
                      ) {
                        return `${mask.layerIndex}: ${mask.width}×${mask.height} · esperado ${expected}×${expected}`;
                      }

                      return mask.hasPixelStatistics
                        ? `${mask.layerIndex}: ${mask.width}×${mask.height} · ${Math.round(
                            mask.coverage *
                              100
                          )}%`
                        : `${mask.layerIndex}: ${mask.width}×${mask.height} · estatísticas sob demanda`;
                    })
                    .join(" · ")
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

                  const maskResolutionMatches =
                    !activeMask ||
                    layer.maskResolution ===
                      null ||
                    (activeMask.width ===
                      layer.maskResolution &&
                      activeMask.height ===
                        layer.maskResolution);

                  const maskCoverage =
                    activeMask
                      ?.hasPixelStatistics
                      ? activeMask.coverage
                      : maskAsset
                          ?.alphaCoverage;

                  const maskMinimumAlpha =
                    activeMask
                      ?.hasPixelStatistics
                      ? activeMask.minimumAlpha
                      : maskAsset
                          ?.minimumAlpha;

                  const maskMaximumAlpha =
                    activeMask
                      ?.hasPixelStatistics
                      ? activeMask.maximumAlpha
                      : maskAsset
                          ?.maximumAlpha;

                  return (
                    <label
                      key={index}
                      className="terrain-layer-item"
                    >
                      <input
                        type="checkbox"
                        disabled={
                          index > 0 &&
                          (!activeMask ||
                            !activeMask.isValid ||
                            !maskResolutionMatches ||
                            (maskMaximumAlpha !==
                              undefined &&
                              maskMaximumAlpha !==
                                null &&
                              maskMaximumAlpha ===
                                0))
                        }
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
                              ? !activeMask.isValid
                                ? `inválida · ${activeMask.errorCode ?? "erro"}`
                                : !maskResolutionMatches
                                  ? `${activeMask.width}×${activeMask.height} · esperado ${layer.maskResolution}×${layer.maskResolution} · incompatível`
                                  : maskMaximumAlpha === 0
                                  ? `${activeMask.width}×${activeMask.height} · vazia · alpha 0`
                                  : maskMinimumAlpha === 255 &&
                                      maskMaximumAlpha === 255
                                    ? `${activeMask.width}×${activeMask.height} · 100% cobertura · alpha 255 · opaca`
                                    : maskCoverage !==
                                          undefined &&
                                        maskCoverage !==
                                          null &&
                                        maskMinimumAlpha !==
                                          undefined &&
                                        maskMinimumAlpha !==
                                          null &&
                                        maskMaximumAlpha !==
                                          undefined &&
                                        maskMaximumAlpha !==
                                          null
                                      ? `${activeMask.width}×${activeMask.height} · ${formatNumber(
                                          maskCoverage *
                                            100
                                        )}% cobertura · alpha ${maskMinimumAlpha}-${maskMaximumAlpha} · ${maskAsset?.exists ? "carregada" : "presente"}`
                                      : `${activeMask.width}×${activeMask.height} · validada · estatísticas ao carregar`
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
          <dt>Upload textura base</dt>
          <dd>
            {!baseGroundMainAsset
              ? "Aguardando asset"
              : !baseGroundMainAsset.exists
                ? `Ausente · ${baseGroundMainAsset.errorCode ?? "sem detalhe"}`
                : baseGroundMainAsset.rgbaBase64 &&
                    baseGroundMainAsset.width &&
                    baseGroundMainAsset.height
                  ? `RGBA direto · ${baseGroundMainAsset.width}×${baseGroundMainAsset.height} · ${baseGroundMainAsset.pixelFormat ?? "pixels decodificados"}`
                  : `${baseGroundMainAsset.extension ?? "formato desconhecido"} · ${baseGroundMainAsset.width ?? "?"}×${baseGroundMainAsset.height ?? "?"} · ${baseGroundMainAsset.pixelFormat ?? "decoder padrão"}`}
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
      if (!placementAsset) {
        return renderMapInspector();
      }

      const placementGroup =
        getSceneryLibraryGroup(
          placementAsset,
          sceneryMetadataByPath[
            placementAsset.sceneryObjectPath
          ],
          geometryByPath[
            placementAsset.sceneryObjectPath
          ]
        );
      const placementGroupInfo =
        sceneryLibraryGroups.find(
          (group) =>
            group.id ===
            placementGroup
        );
      const placementThumbnail =
        assetThumbnailCache[
          "sco:" +
            placementAsset.sceneryObjectPath
        ];

      return (
        <>
          <div className="inspector-hero placement-inspector-hero">
            {placementThumbnail ? (
              <img
                className="inspector-placement-thumbnail"
                src={placementThumbnail}
                alt=""
                draggable={false}
              />
            ) : (
              <div className="object-symbol">
                {placementGroupInfo?.icon ??
                  "O"}
              </div>
            )}
            <div>
              <strong>
                {placementAsset.fileName}
              </strong>
              <span>
                Asset real em colocação
              </span>
            </div>
          </div>

          <div className="placement-inspector-status">
            <strong>
              Clique no mapa para posicionar
            </strong>
            <span>
              {activeConstructionTool
                ? "Categoria ativa · " +
                  (placementGroupInfo?.label ??
                    "Objetos")
                : "Objeto da biblioteca"}
            </span>
          </div>

          <dl className="property-list dense">
            <div>
              <dt>Arquivo</dt>
              <dd>
                {placementAsset.sceneryObjectPath}
              </dd>
            </div>
            <div>
              <dt>Grupo</dt>
              <dd>
                {placementGroupInfo?.label ??
                  "Outros"}
                {" · "}
                {getSceneryLibrarySubcategory(
                  placementAsset,
                  placementGroup
                )}
              </dd>
            </div>
            <div>
              <dt>Prévia 3D</dt>
              <dd>
                {geometryByPath[
                  placementAsset.sceneryObjectPath
                ]
                  ? "Carregada"
                  : "Carregando sob demanda"}
              </dd>
            </div>
            <div>
              <dt>Template seguro</dt>
              <dd>
                {placementHasKnownTemplate
                  ? "Disponível no mapa"
                  : mapLoadMode ===
                      "full"
                    ? "Ainda não encontrado no mapa"
                    : "Será validado pelo host ao salvar"}
              </dd>
            </div>
            <div>
              <dt>Destino</dt>
              <dd>
                {pendingPlacement
                  ? `Tile ${pendingPlacement.tileX},${pendingPlacement.tileY} · X ${formatNumber(pendingPlacement.x)} · Y ${formatNumber(pendingPlacement.y)} · Z ${formatNumber(pendingPlacement.z)}`
                  : "Clique no viewport"}
              </dd>
            </div>
            <div>
              <dt>Rotação</dt>
              <dd>
                {pendingPlacement
                  ? formatNumber(
                      pendingPlacement.rotation
                    ) + "°"
                  : formatNumber(
                      placementTransformDefaults.rotation
                    ) + "°"}
              </dd>
            </div>
          </dl>

          <div className="placement-inspector-actions">
            <button
              type="button"
              className="wide"
              onClick={() => {
                handlePreviewSceneryLibraryAsset(
                  placementAsset
                );
                handleExplorerPanelTab(
                  "library"
                );
              }}
            >
              Abrir prévia 3D e biblioteca
            </button>
            <button
              type="button"
              className="secondary-action wide"
              onClick={
                handleCancelPlacement
              }
            >
              Cancelar colocação
            </button>
          </div>

          <div className="transform-help">
            A colocação continua usando o .sco
            real e as regras preservativas do
            host. O Inspector apenas acompanha
            o asset ativo; ele não cria dados
            artificiais.
          </div>
        </>
      );
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
              {selectedMetadata?.tree && (
                <>
                  <div>
                    <dt>Árvore [tree]</dt>
                    <dd>
                      {selectedMetadata.tree.textureName}
                    </dd>
                  </div>
                  <div>
                    <dt>Faixa [tree]</dt>
                    <dd>
                      altura{" "}
                      {formatNumber(
                        selectedMetadata.tree.minimumHeight
                      )}–{formatNumber(
                        selectedMetadata.tree.maximumHeight
                      )} m · aspecto{" "}
                      {formatNumber(
                        selectedMetadata.tree.minimumAspect
                      )}–{formatNumber(
                        selectedMetadata.tree.maximumAspect
                      )}
                    </dd>
                  </div>
                </>
              )}
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
      if (!splineLibraryPlacementAsset) {
        return renderMapInspector();
      }

      const placementGroup =
        getSplineLibraryGroup(
          splineLibraryPlacementAsset
        );
      const placementGroupInfo =
        splineLibraryGroups.find(
          (group) =>
            group.id ===
            placementGroup
        );
      const placementThumbnail =
        assetThumbnailCache[
          "sli:" +
            splineLibraryPlacementAsset
              .splinePath
        ];
      const placementProfile =
        splineProfilesByPath[
          splineLibraryPlacementAsset
            .splinePath
        ];

      return (
        <>
          <div className="inspector-hero placement-inspector-hero">
            {placementThumbnail ? (
              <img
                className="inspector-placement-thumbnail"
                src={placementThumbnail}
                alt=""
                draggable={false}
              />
            ) : (
              <div className="object-symbol">
                {placementGroupInfo?.icon ??
                  "S"}
              </div>
            )}
            <div>
              <strong>
                {
                  splineLibraryPlacementAsset
                    .fileName
                }
              </strong>
              <span>
                Spline real em construção
              </span>
            </div>
          </div>

          <div className="placement-inspector-status">
            <strong>
              {easyRoadMode
                ? easyRoadStart
                  ? "Defina o fim / ajuste a curva"
                  : "Defina o ponto inicial"
                : "Clique no mapa para posicionar"}
            </strong>
            <span>
              {placementGroupInfo?.label ??
                "Splines"}
              {roadPlacementKind ===
                "bridge"
                ? ` · elevação ${formatNumber(roadElevationOffset)} m`
                : ""}
            </span>
          </div>

          <dl className="property-list dense">
            <div>
              <dt>Arquivo</dt>
              <dd>
                {
                  splineLibraryPlacementAsset
                    .splinePath
                }
              </dd>
            </div>
            <div>
              <dt>Grupo</dt>
              <dd>
                {placementGroupInfo?.label ??
                  "Outras"}
                {" · "}
                {getSplineLibrarySubcategory(
                  splineLibraryPlacementAsset,
                  placementGroup
                )}
              </dd>
            </div>
            <div>
              <dt>Perfil real</dt>
              <dd>
                {placementProfile
                  ? `${placementProfile.surfaces.length} superfície(s) · ${placementProfile.textures.length} textura(s)`
                  : "Carregando .sli sob demanda"}
              </dd>
            </div>
            <div>
              <dt>Tipo</dt>
              <dd>
                {splineLibraryPlacementIsHeight
                  ? "[spline_h] altura"
                  : "[spline] normal"}
              </dd>
            </div>
            <div>
              <dt>Geometria</dt>
              <dd>
                {pendingSplinePlacement
                  ? `${formatNumber(pendingSplinePlacement.length)} m · raio ${formatNumber(pendingSplinePlacement.radius)} m · ${formatNumber(pendingSplinePlacement.rotation)}°`
                  : easyRoadMode
                    ? "Definida pelos pontos início/fim"
                    : "Aguardando posição"}
              </dd>
            </div>
            <div>
              <dt>Conexão</dt>
              <dd>
                {roadEndpointSnapEnabled
                  ? roadAutoConnectEnabled
                    ? "Snap de pontas + previous/next automático"
                    : "Snap de pontas · vínculos manuais"
                  : "Snap de pontas desligado"}
              </dd>
            </div>
          </dl>

          <div className="placement-inspector-actions">
            <button
              type="button"
              className="wide"
              onClick={() => {
                handlePreviewSplineLibraryAsset(
                  splineLibraryPlacementAsset
                );
                handleExplorerPanelTab(
                  "splineLibrary"
                );
              }}
            >
              Abrir prévia 3D e biblioteca
            </button>
            <button
              type="button"
              className="secondary-action wide"
              onClick={
                handleCancelSplinePlacement
              }
            >
              Cancelar construção
            </button>
          </div>

          <div className="transform-help">
            O traçado usa o perfil .sli real,
            snapping e vínculos preservativos.
            O Inspector acompanha os parâmetros
            da construção ativa sem simular dados.
          </div>
        </>
      );
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
                className="secondary-action"
                onClick={
                  handleLevelSelectedSplineToTerrain
                }
                disabled={savingSpline}
                title="Ajustar Z e gradientes pela altura real do terreno carregado"
              >
                Nivelar rua ao terreno
              </button>

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
      <section
        className={
          isFullScreen
            ? "map-editor fullscreen-editor"
            : "map-editor"
        }
      >
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
          <div className="editor-menu-root">
            <button
              type="button"
              className={
                activeTopMenu === "file"
                  ? "active"
                  : ""
              }
              onClick={() =>
                setActiveTopMenu(
                  (current) =>
                    current === "file"
                      ? undefined
                      : "file"
                )
              }
            >
              Arquivo
            </button>

            {activeTopMenu === "file" && (
              <div className="editor-menu-popup">
                <button
                  type="button"
                  disabled={
                    previewEditCount === 0 ||
                    busy
                  }
                  onClick={() => {
                    handleSavePreviewEdits();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Salvar objeto
                  {previewEditCount > 0
                    ? ` (${previewEditCount})`
                    : ""}
                </button>
                <button
                  type="button"
                  disabled={
                    splinePreviewEditCount ===
                      0 || busy
                  }
                  onClick={() => {
                    handleSaveSplinePreview();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Salvar spline
                  {splinePreviewEditCount > 0
                    ? ` (${splinePreviewEditCount})`
                    : ""}
                </button>
                <button
                  type="button"
                  disabled={
                    previewEditCount === 0 &&
                    splinePreviewEditCount === 0
                  }
                  onClick={() => {
                    handleDiscardPreviewEdits();
                    handleDiscardSplinePreview();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Descartar prévias
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => {
                    handleOpenMap();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Abrir outro mapa…
                </button>
              </div>
            )}
          </div>

          <div className="editor-menu-root">
            <button
              type="button"
              className={
                activeTopMenu === "edit"
                  ? "active"
                  : ""
              }
              onClick={() =>
                setActiveTopMenu(
                  (current) =>
                    current === "edit"
                      ? undefined
                      : "edit"
                )
              }
            >
              Editar
            </button>

            {activeTopMenu === "edit" && (
              <div className="editor-menu-popup">
                <button
                  type="button"
                  disabled={
                    undoPreviewStack.length ===
                    0
                  }
                  onClick={() => {
                    handleUndoPreview();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Desfazer transformação · Ctrl+Z
                </button>
                <button
                  type="button"
                  disabled={
                    redoPreviewStack.length ===
                    0
                  }
                  onClick={() => {
                    handleRedoPreview();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Refazer transformação · Ctrl+Y
                </button>
                <button
                  type="button"
                  disabled={
                    restoringConstruction ||
                    constructionUndoStack.length ===
                      0
                  }
                  onClick={() => {
                    handleUndoConstruction();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Desfazer construção
                </button>
                <button
                  type="button"
                  disabled={
                    restoringConstruction ||
                    constructionRedoStack.length ===
                      0
                  }
                  onClick={() => {
                    handleRedoConstruction();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Refazer construção
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setEditorTool("select");
                    setActiveConstructionTool(
                      undefined
                    );
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Selecionar · Q
                </button>
                <button
                  type="button"
                  disabled={
                    !selectedObject &&
                    !selectedSpline
                  }
                  onClick={() => {
                    setEditorTool("move");
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Mover seleção · W
                </button>
                <button
                  type="button"
                  disabled={
                    !selectedObject &&
                    !selectedSpline
                  }
                  onClick={() => {
                    setEditorTool("rotate");
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Rotacionar seleção · E
                </button>
              </div>
            )}
          </div>

          <div className="editor-menu-root">
            <button
              type="button"
              className={
                activeTopMenu === "view"
                  ? "active"
                  : ""
              }
              onClick={() =>
                setActiveTopMenu(
                  (current) =>
                    current === "view"
                      ? undefined
                      : "view"
                )
              }
            >
              Visualizar
            </button>

            {activeTopMenu === "view" && (
              <div className="editor-menu-popup">
                <button
                  type="button"
                  onClick={() => {
                    if (isFullScreen) {
                      setFullScreenPanel(
                        fullScreenPanel ===
                          "explorer"
                          ? undefined
                          : "explorer"
                      );
                    } else {
                      setDesktopExplorerOpen(
                        (current) =>
                          !current
                      );
                    }
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  {(isFullScreen
                    ? fullScreenPanel ===
                      "explorer"
                    : desktopExplorerOpen)
                    ? "✓ "
                    : ""}
                  Explorador
                </button>
                <button
                  type="button"
                  onClick={() => {
                    if (isFullScreen) {
                      setFullScreenPanel(
                        fullScreenPanel ===
                          "inspector"
                          ? undefined
                          : "inspector"
                      );
                    } else {
                      setDesktopInspectorOpen(
                        (current) =>
                          !current
                      );
                    }
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  {(isFullScreen
                    ? fullScreenPanel ===
                      "inspector"
                    : desktopInspectorOpen)
                    ? "✓ "
                    : ""}
                  Inspetor
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setShowTileNavigator(
                      (current) => !current
                    );
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  {showTileNavigator
                    ? "✓ "
                    : ""}
                  Navegador de blocos
                </button>
                <button
                  type="button"
                  onClick={() => {
                    requestCameraAction(
                      "fit"
                    );
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Enquadrar mapa · Home
                </button>
                <button
                  type="button"
                  disabled={
                    !selectedObject &&
                    !selectedSpline
                  }
                  onClick={() => {
                    requestCameraAction(
                      "focus"
                    );
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Focar seleção · F
                </button>
                <button
                  type="button"
                  disabled={
                    mapLoadMode === "full" ||
                    busy
                  }
                  onClick={() => {
                    if (
                      mapLoadMode !== "full"
                    ) {
                      setAssetWarmupActive(
                        true
                      );
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
                    }
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  {mapLoadMode === "full"
                    ? "✓ "
                    : ""}
                  Mapa completo
                </button>
                <button
                  type="button"
                  disabled={
                    mapLoadMode ===
                      "performance" ||
                    busy
                  }
                  onClick={() => {
                    if (
                      mapLoadMode !==
                      "performance"
                    ) {
                      setAssetWarmupActive(
                        true
                      );
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
                    }
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  {mapLoadMode ===
                  "performance"
                    ? "✓ "
                    : ""}
                  Streaming automático
                </button>
                <button
                  type="button"
                  onClick={() => {
                    requestFullScreen(
                      !isFullScreen
                    );
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  {isFullScreen
                    ? "Sair da tela cheia · F11"
                    : "Tela cheia · F11"}
                </button>
              </div>
            )}
          </div>

          <div className="editor-menu-root">
            <button
              type="button"
              className={
                activeTopMenu === "map"
                  ? "active"
                  : ""
              }
              onClick={() =>
                setActiveTopMenu(
                  (current) =>
                    current === "map"
                      ? undefined
                      : "map"
                )
              }
            >
              Mapa
            </button>

            {activeTopMenu === "map" && (
              <div className="editor-menu-popup">
                <button
                  type="button"
                  onClick={() => {
                    setShowRealMapPanel(
                      true
                    );
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Mapa real por coordenadas…
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setShowMapHealthPanel(
                      (current) =>
                        !current
                    );
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  {showMapHealthPanel
                    ? "✓ "
                    : ""}
                  Saúde do mapa
                  {mapHealthIssueCount > 0
                    ? ` (${mapHealthIssueCount})`
                    : ""}
                </button>
                <button
                  type="button"
                  disabled={
                    loadingSceneryLibrary ||
                    loadingSplineLibrary
                  }
                  onClick={() => {
                    handleAuditDependencies();
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  Verificar dependências
                  {missingDependencyCount > 0
                    ? ` (${missingDependencyCount})`
                    : ""}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setShowConstructionSetPanel(
                      (current) =>
                        !current
                    );
                    setActiveTopMenu(
                      undefined
                    );
                  }}
                >
                  {showConstructionSetPanel
                    ? "✓ "
                    : ""}
                  Conjuntos de construção
                </button>
              </div>
            )}
          </div>

          <div className="editor-menu-spacer" />

          <div
            className="editor-menu-status"
            title="Estado atual da edição"
          >
            <strong>
              {previewEditCount +
                splinePreviewEditCount >
              0
                ? `${previewEditCount + splinePreviewEditCount} alteração(ões)`
                : "Mapa pronto"}
            </strong>
            <span>
              {mapLoadMode === "full"
                ? "Mapa completo"
                : "Streaming automático"}
            </span>
          </div>
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

          <div
            className="selection-tool-group"
            aria-label="Filtro de seleção"
          >
            {([
              ["all", "Tudo", "Alt+1"],
              ["object", "Objetos", "Alt+2"],
              ["spline", "Splines", "Alt+3"],
              ["terrain", "Terreno", "Alt+4"]
            ] as const).map(
              ([
                mode,
                label,
                shortcut
              ]) => (
                <button
                  type="button"
                  key={mode}
                  className={
                    selectionMode === mode
                      ? "selection-tool active"
                      : "selection-tool"
                  }
                  title={`Selecionar ${label.toLocaleLowerCase("pt-BR")} (${shortcut})`}
                  onClick={() => {
                    setSelectionMode(mode);
                    setEditorTool("select");

                    if (
                      mode === "terrain"
                    ) {
                      setShowTerrain(true);
                    }
                  }}
                >
                  {label}
                </button>
              )
            )}
          </div>

          <span className="toolbar-separator" />

          <div
            className="create-tool-group construction-dock"
            aria-label="Construção"
          >
            <span className="construction-dock-label">
              Construção
            </span>
            {([
              ["road", "═", "Ruas"],
              ["junction", "✣", "Cruzamentos"],
              ["bridge", "⌁", "Pontes"],
              ["building", "⌂", "Prédios"],
              ["tree", "♣", "Vegetação"],
              ["transit", "▤", "Transporte"],
              ["street", "⚑", "Mobiliário"],
              ["utilities", "⚙", "Infraestrutura"],
              ["terrain", "▱", "Terreno"]
            ] as const).map(
              ([tool, icon, label]) => (
                <button
                  type="button"
                  className="create-tool construction-tool"
                  key={tool}
                  title={label}
                  onClick={() =>
                    openQuickCreate(tool)
                  }
                >
                  <span aria-hidden="true">
                    {icon}
                  </span>
                  <small>
                    {label}
                  </small>
                </button>
              )
            )}
          </div>

          <button
            type="button"
            className={
              showConstructionSetPanel
                ? "construction-sets-button active"
                : "construction-sets-button"
            }
            onClick={() =>
              setShowConstructionSetPanel(
                (current) =>
                  !current
              )
            }
            title="Criar e aplicar conjuntos de construção"
          >
            ▦ Conjuntos
            {constructionSets.length >
              0 && (
              <small>
                {constructionSets.length}
              </small>
            )}
          </button>

          <button
            type="button"
            className={
              missingDependencyCount > 0
                ? "dependency-audit-button warning"
                : "dependency-audit-button"
            }
            onClick={
              handleAuditDependencies
            }
            disabled={
              loadingSceneryLibrary ||
              loadingSplineLibrary
            }
            title="Comparar assets usados no mapa com a instalação atual"
          >
            {loadingSceneryLibrary ||
            loadingSplineLibrary
              ? "Verificando..."
              : sceneryLibraryReady &&
                  splineLibraryReady
                ? missingDependencyCount > 0
                  ? "⚠ Faltam " +
                    missingDependencyCount
                  : "✓ Dependências"
                : "Verificar dependências"}
          </button>

          <button
            type="button"
            className={
              showMapHealthPanel
                ? "map-health-button active"
                : mapHealthIssueCount > 0
                  ? "map-health-button warning"
                  : "map-health-button"
            }
            onClick={() =>
              setShowMapHealthPanel(
                (current) =>
                  !current
              )
            }
            title="Resumo técnico do mapa e assets carregados"
          >
            {mapHealthIssueCount > 0
              ? "⚠ Saúde " +
                mapHealthIssueCount
              : "✓ Saúde"}
          </button>

          <div className="construction-history-controls">
            <button
              type="button"
              disabled={
                restoringConstruction ||
                constructionUndoStack.length ===
                  0
              }
              onClick={
                handleUndoConstruction
              }
              title={
                constructionUndoStack.length >
                  0
                  ? "Desfazer: " +
                    constructionUndoStack[
                      constructionUndoStack.length -
                        1
                    ].label
                  : "Nenhuma construção para desfazer"
              }
            >
              ↶ Construção
            </button>
            <button
              type="button"
              disabled={
                restoringConstruction ||
                constructionRedoStack.length ===
                  0
              }
              onClick={
                handleRedoConstruction
              }
              title={
                constructionRedoStack.length >
                  0
                  ? "Refazer: " +
                    constructionRedoStack[
                      constructionRedoStack.length -
                        1
                    ].label
                  : "Nenhuma construção para refazer"
              }
            >
              ↷
            </button>
            <span>
              {constructionUndoStack.length}
              {" "}ação(ões)
            </span>
          </div>

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
              requestFullScreen(
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
            Q/W/E · Alt+1..4 seleção · Alt+R/C/O/T/A/G/Y criar
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

              setAssetWarmupActive(true);
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

              setAssetWarmupActive(true);
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
          <aside
            data-floating-tool
            data-floating-tool-id="explorer-drawer"
            className={[
              "map-explorer city-drawer city-drawer-left",
              isFullScreen
                ? "fullscreen-drawer fullscreen-left"
                : "desktop-drawer",
              (
                isFullScreen
                  ? fullScreenPanel ===
                    "explorer"
                  : desktopExplorerOpen
              )
                ? "open"
                : "closed"
            ]
              .filter(Boolean)
              .join(" ")}
          >
            <button
              type="button"
              className="city-drawer-drag-grip"
              data-drag-handle
              title="Arraste para mover o painel"
              aria-label="Mover painel"
            >
              ⋮⋮
            </button>
            {(
              isFullScreen
                ? fullScreenPanel ===
                  "explorer"
                : desktopExplorerOpen
            ) && (
              <button
                type="button"
                className="fullscreen-drawer-close city-drawer-close"
                onClick={() => {
                  if (isFullScreen) {
                    setFullScreenPanel(
                      undefined
                    );
                  } else {
                    setDesktopExplorerOpen(
                      false
                    );
                  }
                }}
                title="Fechar painel"
              >
                ×
              </button>
            )}
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
  
                <button
                  type="button"
                  className={
                    selectionMode === "object"
                      ? "tree-node tree-node-button active"
                      : "tree-node tree-node-button"
                  }
                  onClick={() => {
                    setSelectionMode("object");
                    setEditorTool("select");
                  }}
                  title="Selecionar apenas objetos"
                >
                  <span>▣</span>
                  {mapLoadMode === "full"
                    ? "Objetos"
                    : "Objetos (área)"}
                  <strong>
                    {selectedStats?.objects ??
                      objects.length}
                  </strong>
                </button>
  
                <div className="tree-node">
                  <span>◈</span>
                  {mapLoadMode === "full"
                    ? "Malhas reais"
                    : "Malhas reais (área)"}
                  <strong>
                    {mapLoadMode === "full"
                      ? `${renderableMapGeometryCount}/${mapObjectPaths.length}`
                      : `${renderableDiagnosticGeometryCount}/${nearbyObjectPaths.length}`}
                  </strong>
                </div>
  
                <button
                  type="button"
                  className={
                    selectionMode === "spline"
                      ? "tree-node tree-node-button active"
                      : "tree-node tree-node-button"
                  }
                  onClick={() => {
                    setSelectionMode("spline");
                    setEditorTool("select");
                  }}
                  title="Selecionar apenas splines"
                >
                  <span>⌇</span>
                  {mapLoadMode === "full"
                    ? "Splines"
                    : "Splines (área)"}
                  <strong>
                    {selectedStats?.splines ??
                      splines.length}
                  </strong>
                </button>
  
                <button
                  type="button"
                  className={
                    selectionMode === "terrain"
                      ? "tree-node tree-node-button active"
                      : "tree-node tree-node-button"
                  }
                  onClick={() => {
                    setSelectionMode("terrain");
                    setEditorTool("select");
                    setShowTerrain(true);
                  }}
                  title="Selecionar terreno/tile"
                >
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
                </button>
  
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
                          setSelectionMode(
                            "object"
                          );
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
                          setSelectionMode(
                            "spline"
                          );
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

                <div className="library-smart-toolbar">
                  <div className="library-view-tabs">
                    {([
                      ["groups", "Grupos"],
                      ["favorites", "★ Favoritos"],
                      ["recent", "Recentes"],
                      ["frequent", "Mais usados"],
                      ["collection", "Coleção"]
                    ] as const).map(
                      ([value, label]) => (
                        <button
                          type="button"
                          key={value}
                          className={
                            sceneryLibraryView ===
                            value
                              ? "active"
                              : ""
                          }
                          onClick={() =>
                            setSceneryLibraryView(
                              value
                            )
                          }
                        >
                          {label}
                        </button>
                      )
                    )}
                  </div>

                  <div className="library-filter-row">
                    <select
                      value={
                        sceneryTechnicalFilter
                      }
                      onChange={(event) =>
                        setSceneryTechnicalFilter(
                          event.currentTarget
                            .value as SceneryTechnicalFilter
                        )
                      }
                      aria-label="Filtro técnico de objetos"
                    >
                      <option value="all">
                        Todos os assets
                      </option>
                      <option value="used">
                        Usados no mapa
                      </option>
                      <option value="tree">
                        Árvores [tree] detectadas
                      </option>
                      <option value="loaded">
                        Geometria carregada
                      </option>
                      <option value="problems">
                        ⚠ Com problemas
                      </option>
                    </select>

                    <select
                      value={
                        scenerySubcategory
                      }
                      onChange={(event) =>
                        setScenerySubcategory(
                          event.currentTarget
                            .value
                        )
                      }
                      aria-label="Subcategoria de objetos"
                    >
                      <option value="all">
                        Todas as subcategorias
                      </option>
                      {scenerySubcategories.map(
                        (subcategory) => (
                          <option
                            key={subcategory}
                            value={subcategory}
                          >
                            {subcategory}
                          </option>
                        )
                      )}
                    </select>

                    <select
                      value={
                        activeLibraryCollection
                      }
                      onChange={(event) =>
                        setActiveLibraryCollection(
                          event.currentTarget
                            .value
                        )
                      }
                      aria-label="Coleção de assets"
                    >
                      <option value="">
                        Sem coleção
                      </option>
                      {Object.keys(
                        libraryCollections
                      )
                        .sort((a, b) =>
                          a.localeCompare(
                            b,
                            "pt-BR"
                          )
                        )
                        .map((name) => (
                          <option
                            key={name}
                            value={name}
                          >
                            {name}
                          </option>
                        ))}
                    </select>
                  </div>

                  <div className="library-collection-create">
                    <input
                      value={newCollectionName}
                      onChange={(event) =>
                        setNewCollectionName(
                          event.currentTarget
                            .value
                        )
                      }
                      placeholder="Nova coleção..."
                      onKeyDown={(event) => {
                        if (
                          event.key === "Enter"
                        ) {
                          handleCreateLibraryCollection();
                        }
                      }}
                    />
                    <button
                      type="button"
                      disabled={
                        !newCollectionName.trim()
                      }
                      onClick={
                        handleCreateLibraryCollection
                      }
                    >
                      Criar
                    </button>
                  </div>
                </div>

                <div
                  className="library-group-tabs"
                  role="tablist"
                  aria-label="Grupos de objetos"
                >
                  {sceneryLibraryGroups.map(
                    (group) => (
                      <button
                        type="button"
                        role="tab"
                        key={group.id}
                        className={
                          sceneryLibraryGroup ===
                          group.id
                            ? "active"
                            : ""
                        }
                        aria-selected={
                          sceneryLibraryGroup ===
                          group.id
                        }
                        onClick={() => {
                          setSceneryLibraryGroup(
                            group.id
                          );
                          setScenerySubcategory(
                            "all"
                          );
                        }}
                      >
                        <span
                          className="library-group-icon"
                          aria-hidden="true"
                        >
                          {group.icon}
                        </span>
                        <span>
                          {group.label}
                        </span>
                        <small>
                          {sceneryLibraryGroupCounts[
                            group.id
                          ]}
                        </small>
                      </button>
                    )
                  )}
                </div>

                {sceneryLibraryPreviewAsset && (
                  <div className="asset-preview-card">
                    <div className="asset-preview-heading">
                      <div>
                        <strong>Prévia 3D real</strong>
                        <span>
                          {sceneryLibraryPreviewAsset.fileName}
                        </span>
                      </div>
                      <button
                        type="button"
                        className="secondary-action"
                        disabled={
                          insertingObject ||
                          placementAsset
                            ?.sceneryObjectPath ===
                            sceneryLibraryPreviewAsset
                              .sceneryObjectPath
                        }
                        onClick={() =>
                          handleSelectPlacementAsset(
                            sceneryLibraryPreviewAsset
                          )
                        }
                      >
                        {placementAsset
                          ?.sceneryObjectPath ===
                        sceneryLibraryPreviewAsset
                          .sceneryObjectPath
                          ? "Em colocação"
                          : "Colocar"}
                      </button>
                    </div>

                    {geometryByPath[
                      sceneryLibraryPreviewAsset
                        .sceneryObjectPath
                    ] && (
                      <AssetPreview3D
                        kind="object"
                        assetPath={
                          sceneryLibraryPreviewAsset
                            .sceneryObjectPath
                        }
                        geometry={
                          geometryByPath[
                            sceneryLibraryPreviewAsset
                              .sceneryObjectPath
                          ]
                        }
                        textureAssetsByKey={
                          textureAssetsByKey
                        }
                        onThumbnailReady={(
                          dataUrl
                        ) =>
                          handleAssetThumbnail(
                            "sco:" +
                              sceneryLibraryPreviewAsset
                                .sceneryObjectPath,
                            dataUrl
                          )
                        }
                      />
                    )}

                    <div className="asset-preview-details">
                      <span>
                        Grupo:{" "}
                        {
                          sceneryLibraryGroups.find(
                            (group) =>
                              group.id ===
                              getSceneryLibraryGroup(
                                sceneryLibraryPreviewAsset,
                                sceneryMetadataByPath[
                                  sceneryLibraryPreviewAsset
                                    .sceneryObjectPath
                                ],
                                geometryByPath[
                                  sceneryLibraryPreviewAsset
                                    .sceneryObjectPath
                                ]
                              )
                          )?.label
                        }
                      </span>
                      <span>
                        {geometryByPath[
                          sceneryLibraryPreviewAsset
                            .sceneryObjectPath
                        ]
                          ? geometryByPath[
                              sceneryLibraryPreviewAsset
                                .sceneryObjectPath
                            ].meshes.length +
                            " mesh(es) reais" +
                            (geometryByPath[
                              sceneryLibraryPreviewAsset
                                .sceneryObjectPath
                            ].tree
                              ? " · árvore billboard"
                              : "")
                          : "Carregando geometria real..."}
                      </span>
                      <span>
                        {sceneryMetadataByPath[
                          sceneryLibraryPreviewAsset
                            .sceneryObjectPath
                        ]?.friendlyName ??
                          sceneryLibraryPreviewAsset
                            .sceneryObjectPath}
                      </span>
                    </div>

                    <div className="asset-inspector-grid">
                      <span>
                        <strong>Uso no mapa</strong>
                        {
                          objects.filter(
                            (item) =>
                              item.sceneryObjectPath ===
                              sceneryLibraryPreviewAsset
                                .sceneryObjectPath
                          ).length
                        }
                      </span>
                      <span>
                        <strong>Uso pela biblioteca</strong>
                        {
                          sceneryUsage[
                            sceneryLibraryPreviewAsset
                              .sceneryObjectPath
                          ] ?? 0
                        }
                      </span>
                      <span>
                        <strong>Subcategoria</strong>
                        {getSceneryLibrarySubcategory(
                          sceneryLibraryPreviewAsset,
                          getSceneryLibraryGroup(
                            sceneryLibraryPreviewAsset,
                            sceneryMetadataByPath[
                              sceneryLibraryPreviewAsset
                                .sceneryObjectPath
                            ],
                            geometryByPath[
                              sceneryLibraryPreviewAsset
                                .sceneryObjectPath
                            ]
                          )
                        )}
                      </span>
                      <span
                        className={
                          sceneryPreviewTechnicalSummary
                            ?.health
                            ? "asset-health-card " +
                              sceneryPreviewTechnicalSummary
                                .health
                            : "asset-health-card"
                        }
                      >
                        <strong>Status</strong>
                        {sceneryPreviewTechnicalSummary
                          ?.health === "ok"
                          ? "✓ Asset íntegro"
                          : sceneryPreviewTechnicalSummary
                                ?.health ===
                              "error"
                            ? "⚠ Problemas detectados"
                            : sceneryPreviewTechnicalSummary
                                  ?.health ===
                                "warning"
                              ? "◌ Atenção / carregando"
                              : "Carregando 3D"}
                      </span>
                    </div>

                    {sceneryPreviewTechnicalSummary && (
                      <div className="asset-technical-summary">
                        <div>
                          <strong>
                            Geometria
                          </strong>
                          <span>
                            {sceneryPreviewTechnicalSummary.meshCount}
                            {" "}mesh(es) ·{" "}
                            {sceneryPreviewTechnicalSummary.vertexCount.toLocaleString(
                              "pt-BR"
                            )}
                            {" "}vértices ·{" "}
                            {sceneryPreviewTechnicalSummary.triangleCount.toLocaleString(
                              "pt-BR"
                            )}
                            {" "}triângulos
                          </span>
                        </div>
                        <div>
                          <strong>
                            Materiais / texturas
                          </strong>
                          <span>
                            {sceneryPreviewTechnicalSummary.materialCount}
                            {" "}material(is) ·{" "}
                            {sceneryPreviewTechnicalSummary.loadedTextureCount}
                            /{sceneryPreviewTechnicalSummary.textureCount}
                            {" "}textura(s) carregada(s)
                          </span>
                        </div>
                        <div>
                          <strong>
                            Dependências O3D
                          </strong>
                          <span>
                            {sceneryPreviewTechnicalSummary.loadedMeshCount}
                            {" "}OK ·{" "}
                            {sceneryPreviewTechnicalSummary.failedMeshCount}
                            {" "}falha(s) ·{" "}
                            {sceneryPreviewTechnicalSummary.missingMeshFileCount}
                            {" "}arquivo(s) ausente(s)
                          </span>
                        </div>
                        <div>
                          <strong>
                            Diagnóstico
                          </strong>
                          <span>
                            {sceneryPreviewTechnicalSummary.missingTextureCount}
                            {" "}textura(s) ausente(s) ·{" "}
                            {sceneryPreviewTechnicalSummary.pendingTextureCount}
                            {" "}pendente(s) ·{" "}
                            {sceneryPreviewTechnicalSummary.unsupportedCommandCount}
                            {" "}comando(s) não suportado(s)
                          </span>
                        </div>
                        <div>
                          <strong>
                            Colisão
                          </strong>
                          <span>
                            {sceneryPreviewTechnicalSummary.collisionMeshCount}
                            {" "}mesh(es) de colisão declarada(s)
                          </span>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                <div className="scenery-library-list city-library-grid">
                  {filteredSceneryLibrary.map(
                    (entry) => {
                      const group =
                        getSceneryLibraryGroup(
                          entry,
                          sceneryMetadataByPath[
                            entry.sceneryObjectPath
                          ],
                          geometryByPath[
                            entry.sceneryObjectPath
                          ]
                        );

                      const groupInfo =
                        sceneryLibraryGroups.find(
                          (candidate) =>
                            candidate.id ===
                            group
                        );
                      const subcategory =
                        getSceneryLibrarySubcategory(
                          entry,
                          group
                        );
                      const assetKey =
                        "sco:" +
                        entry.sceneryObjectPath;
                      const inCollection =
                        activeLibraryCollection
                          ? (
                              libraryCollections[
                                activeLibraryCollection
                              ] ?? []
                            ).includes(assetKey)
                          : false;

                      return (
                        <div
                          className={
                            sceneryLibraryPreviewAsset
                              ?.sceneryObjectPath ===
                            entry.sceneryObjectPath
                              ? "scenery-library-entry active"
                              : "scenery-library-entry"
                          }
                          key={
                            entry.sceneryObjectPath
                          }
                        >
                          <button
                            type="button"
                            className="library-entry-preview-button"
                            draggable
                            onDragStart={(event) => {
                              event.dataTransfer.effectAllowed =
                                "copy";
                              event.dataTransfer.setData(
                                "application/x-omsi-map-studio-scenery",
                                entry.sceneryObjectPath
                              );
                            }}
                            onClick={() =>
                              handlePreviewSceneryLibraryAsset(
                                entry
                              )
                            }
                            title="Carregar prévia 3D real · arraste para o mapa para posicionar"
                          >
                            {assetThumbnailCache[
                              assetKey
                            ] ? (
                              <img
                                className="library-entry-thumbnail"
                                src={
                                  assetThumbnailCache[
                                    assetKey
                                  ]
                                }
                                alt=""
                                draggable={false}
                              />
                            ) : (
                              <span
                                className="library-entry-icon"
                                aria-hidden="true"
                              >
                                {groupInfo?.icon ??
                                  "◇"}
                              </span>
                            )}
                            <span className="library-entry-copy">
                              <strong>
                                {entry.fileName}
                              </strong>
                              <span>
                                {groupInfo?.label ??
                                  "Outros"}
                                {" · "}
                                {subcategory}
                              </span>
                              <small>
                                {entry.sceneryObjectPath}
                              </small>
                              <span className="library-entry-badges">
                                {usedSceneryPaths.has(
                                  entry.sceneryObjectPath
                                ) && (
                                  <em>no mapa</em>
                                )}
                                {geometryByPath[
                                  entry.sceneryObjectPath
                                ] && (
                                  <em>3D</em>
                                )}
                                {(sceneryUsage[
                                  entry.sceneryObjectPath
                                ] ?? 0) > 0 && (
                                  <em>
                                    {sceneryUsage[
                                      entry.sceneryObjectPath
                                    ]}x
                                  </em>
                                )}
                              </span>
                            </span>
                          </button>

                          <div className="library-entry-actions">
                            <button
                              type="button"
                              className={
                                sceneryFavorites.includes(
                                  entry.sceneryObjectPath
                                )
                                  ? "active"
                                  : ""
                              }
                              onClick={() =>
                                toggleSceneryFavorite(
                                  entry.sceneryObjectPath
                                )
                              }
                              title="Favoritar"
                            >
                              {sceneryFavorites.includes(
                                entry.sceneryObjectPath
                              )
                                ? "★"
                                : "☆"}
                            </button>
                            <button
                              type="button"
                              disabled={
                                !activeLibraryCollection
                              }
                              className={
                                inCollection
                                  ? "active"
                                  : ""
                              }
                              onClick={() =>
                                toggleAssetInCollection(
                                  assetKey
                                )
                              }
                              title="Adicionar/remover da coleção ativa"
                            >
                              {inCollection
                                ? "✓ Coleção"
                                : "+ Coleção"}
                            </button>
                            <button
                              type="button"
                              onClick={() =>
                                handlePreviewSceneryLibraryAsset(
                                  entry
                                )
                              }
                            >
                              Prévia
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                handleSelectPlacementAsset(
                                  entry
                                );

                                if (isFullScreen) {
                                  setFullScreenPanel(
                                    "inspector"
                                  );
                                } else {
                                  setDesktopExplorerOpen(
                                    false
                                  );
                                  setDesktopInspectorOpen(
                                    true
                                  );
                                }
                              }}
                              disabled={
                                insertingObject
                              }
                              title="Selecionar para colocação no mapa"
                            >
                              Colocar
                            </button>
                          </div>
                        </div>
                      );
                    }
                  )}

                  {!loadingSceneryLibrary &&
                    filteredSceneryLibrary.length ===
                      0 && (
                      <div className="explorer-empty">
                        Nenhum .sco encontrado neste grupo.
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

                <div className="library-smart-toolbar">
                  <div className="library-view-tabs">
                    {([
                      ["groups", "Grupos"],
                      ["favorites", "★ Favoritos"],
                      ["recent", "Recentes"],
                      ["frequent", "Mais usados"],
                      ["collection", "Coleção"]
                    ] as const).map(
                      ([value, label]) => (
                        <button
                          type="button"
                          key={value}
                          className={
                            splineLibraryView ===
                            value
                              ? "active"
                              : ""
                          }
                          onClick={() =>
                            setSplineLibraryView(
                              value
                            )
                          }
                        >
                          {label}
                        </button>
                      )
                    )}
                  </div>

                  <div className="library-filter-row">
                    <select
                      value={
                        splineTechnicalFilter
                      }
                      onChange={(event) =>
                        setSplineTechnicalFilter(
                          event.currentTarget
                            .value as SplineTechnicalFilter
                        )
                      }
                      aria-label="Filtro técnico de splines"
                    >
                      <option value="all">
                        Todas as splines
                      </option>
                      <option value="used">
                        Usadas no mapa
                      </option>
                      <option value="loaded">
                        Perfil carregado
                      </option>
                      <option value="problems">
                        ⚠ Com problemas
                      </option>
                    </select>

                    <select
                      value={splineSubcategory}
                      onChange={(event) =>
                        setSplineSubcategory(
                          event.currentTarget
                            .value
                        )
                      }
                      aria-label="Subcategoria de splines"
                    >
                      <option value="all">
                        Todas as subcategorias
                      </option>
                      {splineSubcategories.map(
                        (subcategory) => (
                          <option
                            key={subcategory}
                            value={subcategory}
                          >
                            {subcategory}
                          </option>
                        )
                      )}
                    </select>

                    <select
                      value={
                        activeLibraryCollection
                      }
                      onChange={(event) =>
                        setActiveLibraryCollection(
                          event.currentTarget
                            .value
                        )
                      }
                      aria-label="Coleção de assets"
                    >
                      <option value="">
                        Sem coleção
                      </option>
                      {Object.keys(
                        libraryCollections
                      )
                        .sort((a, b) =>
                          a.localeCompare(
                            b,
                            "pt-BR"
                          )
                        )
                        .map((name) => (
                          <option
                            key={name}
                            value={name}
                          >
                            {name}
                          </option>
                        ))}
                    </select>
                  </div>
                </div>

                <div
                  className="library-group-tabs"
                  role="tablist"
                  aria-label="Grupos de splines"
                >
                  {splineLibraryGroups.map(
                    (group) => (
                      <button
                        type="button"
                        role="tab"
                        key={group.id}
                        className={
                          splineLibraryGroup ===
                          group.id
                            ? "active"
                            : ""
                        }
                        aria-selected={
                          splineLibraryGroup ===
                          group.id
                        }
                        onClick={() => {
                          setSplineLibraryGroup(
                            group.id
                          );
                          setSplineSubcategory(
                            "all"
                          );
                        }}
                      >
                        <span
                          className="library-group-icon"
                          aria-hidden="true"
                        >
                          {group.icon}
                        </span>
                        <span>
                          {group.label}
                        </span>
                        <small>
                          {splineLibraryGroupCounts[
                            group.id
                          ]}
                        </small>
                      </button>
                    )
                  )}
                </div>

                {splineLibraryPreviewAsset && (
                  <div className="asset-preview-card">
                    <div className="asset-preview-heading">
                      <div>
                        <strong>Prévia da rua/spline</strong>
                        <span>
                          {splineLibraryPreviewAsset.fileName}
                        </span>
                      </div>
                      <button
                        type="button"
                        className="secondary-action"
                        disabled={
                          insertingSpline ||
                          (
                            splineLibraryPlacementAsset
                              ?.splinePath ===
                            splineLibraryPreviewAsset
                              .splinePath
                          )
                        }
                        onClick={() =>
                          handleSelectSplineLibraryAsset(
                            splineLibraryPreviewAsset,
                            false
                          )
                        }
                      >
                        {splineLibraryPlacementAsset
                          ?.splinePath ===
                        splineLibraryPreviewAsset
                          .splinePath
                          ? "Em criação"
                          : "Criar normal"}
                      </button>
                    </div>

                    {splineProfilesByPath[
                      splineLibraryPreviewAsset
                        .splinePath
                    ] && (
                      <AssetPreview3D
                        kind="spline"
                        template={
                          splineLibraryPlacementAsset
                            ?.splinePath ===
                            splineLibraryPreviewAsset
                              .splinePath &&
                          splinePlacementTemplate
                            ? splinePlacementTemplate
                            : {
                                tileX: 0,
                                tileY: 0,
                                headerValue: "",
                                splinePath:
                                  splineLibraryPreviewAsset
                                    .splinePath,
                                splineId: -1,
                                sourceSectionOrdinal:
                                  -1,
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
                                isHeightSpline: false
                              }
                        }
                        profile={
                          splineProfilesByPath[
                            splineLibraryPreviewAsset
                              .splinePath
                          ]
                        }
                        textureAssetsByKey={
                          textureAssetsByKey
                        }
                        onThumbnailReady={(
                          dataUrl
                        ) =>
                          handleAssetThumbnail(
                            "sli:" +
                              splineLibraryPreviewAsset
                                .splinePath,
                            dataUrl
                          )
                        }
                        length={
                          splineLibraryPlacementAsset
                            ?.splinePath ===
                          splineLibraryPreviewAsset
                            .splinePath
                            ? pendingSplinePlacement
                                ?.length
                            : 20
                        }
                        radius={
                          splineLibraryPlacementAsset
                            ?.splinePath ===
                          splineLibraryPreviewAsset
                            .splinePath
                            ? pendingSplinePlacement
                                ?.radius
                            : 0
                        }
                        rotation={
                          splineLibraryPlacementAsset
                            ?.splinePath ===
                          splineLibraryPreviewAsset
                            .splinePath
                            ? pendingSplinePlacement
                                ?.rotation
                            : 0
                        }
                      />
                    )}

                    <div className="asset-preview-details">
                      <span>
                        Grupo:{" "}
                        {
                          splineLibraryGroups.find(
                            (group) =>
                              group.id ===
                              getSplineLibraryGroup(
                                splineLibraryPreviewAsset
                              )
                          )?.label
                        }
                      </span>
                      <span>
                        {splineProfilesByPath[
                          splineLibraryPreviewAsset
                            .splinePath
                        ]
                          ? splineProfilesByPath[
                              splineLibraryPreviewAsset
                                .splinePath
                            ].surfaces.length +
                            " superfície(s) · " +
                            splineProfilesByPath[
                              splineLibraryPreviewAsset
                                .splinePath
                            ].textures.length +
                            " textura(s)"
                          : "Carregando perfil .sli real..."}
                      </span>
                      <span>
                        {splineLibraryPlacementAsset
                          ?.splinePath ===
                          splineLibraryPreviewAsset
                            .splinePath &&
                        pendingSplinePlacement
                          ? "Em criação: " +
                            formatNumber(
                              pendingSplinePlacement.length
                            ) +
                            " m · " +
                            formatNumber(
                              pendingSplinePlacement.rotation
                            ) +
                            "°"
                          : "Prévia independente · 20 m · reta"}
                      </span>
                      <span>
                        {easyRoadMode
                          ? easyRoadStart
                            ? "Início marcado; clique no ponto final."
                            : "Rua fácil ativa: clique no início e no fim."
                          : "A prévia ainda não altera o arquivo do mapa."}
                      </span>
                    </div>

                    <div className="asset-inspector-grid">
                      <span>
                        <strong>Uso no mapa</strong>
                        {
                          splines.filter(
                            (item) =>
                              item.splinePath ===
                              splineLibraryPreviewAsset
                                .splinePath
                          ).length
                        }
                      </span>
                      <span>
                        <strong>Uso pela biblioteca</strong>
                        {
                          splineUsage[
                            splineLibraryPreviewAsset
                              .splinePath
                          ] ?? 0
                        }
                      </span>
                      <span>
                        <strong>Subcategoria</strong>
                        {getSplineLibrarySubcategory(
                          splineLibraryPreviewAsset,
                          getSplineLibraryGroup(
                            splineLibraryPreviewAsset
                          )
                        )}
                      </span>
                      <span
                        className={
                          splinePreviewTechnicalSummary
                            ?.health
                            ? "asset-health-card " +
                              splinePreviewTechnicalSummary
                                .health
                            : "asset-health-card"
                        }
                      >
                        <strong>Status</strong>
                        {splinePreviewTechnicalSummary
                          ?.health === "ok"
                          ? "✓ Perfil íntegro"
                          : splinePreviewTechnicalSummary
                                ?.health ===
                              "error"
                            ? "⚠ Textura ausente"
                            : splinePreviewTechnicalSummary
                                  ?.health ===
                                "warning"
                              ? "◌ Texturas carregando"
                              : "Aguardando perfil"}
                      </span>
                    </div>

                    {splinePreviewTechnicalSummary && (
                      <div className="asset-technical-summary">
                        <div>
                          <strong>
                            Perfil
                          </strong>
                          <span>
                            {splinePreviewTechnicalSummary.surfaceCount}
                            {" "}superfície(s) · largura{" "}
                            {formatNumber(
                              splinePreviewTechnicalSummary.width
                            )}
                            {" "}m
                          </span>
                        </div>
                        <div>
                          <strong>
                            Texturas
                          </strong>
                          <span>
                            {splinePreviewTechnicalSummary.loadedTextureCount}
                            /{splinePreviewTechnicalSummary.textureCount}
                            {" "}carregada(s) ·{" "}
                            {splinePreviewTechnicalSummary.missingTextureCount}
                            {" "}ausente(s) ·{" "}
                            {splinePreviewTechnicalSummary.pendingTextureCount}
                            {" "}pendente(s)
                          </span>
                        </div>
                        <div>
                          <strong>
                            Transparência
                          </strong>
                          <span>
                            {splinePreviewTechnicalSummary.alphaSurfaceCount}
                            {" "}superfície(s) com alpha
                          </span>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                <div className="scenery-library-list city-library-grid">
                  {filteredSplineLibrary.map(
                    (entry) => {
                      const group =
                        getSplineLibraryGroup(
                          entry
                        );
                      const groupInfo =
                        splineLibraryGroups.find(
                          (candidate) =>
                            candidate.id ===
                            group
                        );
                      const subcategory =
                        getSplineLibrarySubcategory(
                          entry,
                          group
                        );
                      const assetKey =
                        "sli:" +
                        entry.splinePath;
                      const inCollection =
                        activeLibraryCollection
                          ? (
                              libraryCollections[
                                activeLibraryCollection
                              ] ?? []
                            ).includes(assetKey)
                          : false;

                      return (
                        <div
                          className={
                            splineLibraryPreviewAsset
                              ?.splinePath ===
                            entry.splinePath
                              ? "scenery-library-entry active"
                              : "scenery-library-entry"
                          }
                          key={
                            entry.splinePath
                          }
                        >
                          <button
                            type="button"
                            className="library-entry-preview-button"
                            draggable
                            onDragStart={(event) => {
                              event.dataTransfer.effectAllowed =
                                "copy";
                              event.dataTransfer.setData(
                                "application/x-omsi-map-studio-spline",
                                entry.splinePath
                              );
                            }}
                            onClick={() =>
                              handlePreviewSplineLibraryAsset(
                                entry
                              )
                            }
                            title="Carregar prévia 3D real da spline · arraste para o mapa"
                          >
                            {assetThumbnailCache[
                              assetKey
                            ] ? (
                              <img
                                className="library-entry-thumbnail"
                                src={
                                  assetThumbnailCache[
                                    assetKey
                                  ]
                                }
                                alt=""
                                draggable={false}
                              />
                            ) : (
                              <span
                                className="library-entry-icon"
                                aria-hidden="true"
                              >
                                {groupInfo?.icon ??
                                  "◇"}
                              </span>
                            )}
                            <span className="library-entry-copy">
                              <strong>
                                {entry.fileName}
                              </strong>
                              <span>
                                {groupInfo?.label ??
                                  "Outras"}
                                {" · "}
                                {subcategory}
                              </span>
                              <small>
                                {entry.splinePath}
                              </small>
                              <span className="library-entry-badges">
                                {usedSplinePaths.has(
                                  entry.splinePath
                                ) && (
                                  <em>no mapa</em>
                                )}
                                {splineProfilesByPath[
                                  entry.splinePath
                                ] && (
                                  <em>perfil</em>
                                )}
                                {(splineUsage[
                                  entry.splinePath
                                ] ?? 0) > 0 && (
                                  <em>
                                    {splineUsage[
                                      entry.splinePath
                                    ]}x
                                  </em>
                                )}
                              </span>
                            </span>
                          </button>

                          <div className="library-entry-actions">
                            <button
                              type="button"
                              className={
                                splineFavorites.includes(
                                  entry.splinePath
                                )
                                  ? "active"
                                  : ""
                              }
                              onClick={() =>
                                toggleSplineFavorite(
                                  entry.splinePath
                                )
                              }
                              title="Favoritar"
                            >
                              {splineFavorites.includes(
                                entry.splinePath
                              )
                                ? "★"
                                : "☆"}
                            </button>
                            <button
                              type="button"
                              disabled={
                                !activeLibraryCollection
                              }
                              className={
                                inCollection
                                  ? "active"
                                  : ""
                              }
                              onClick={() =>
                                toggleAssetInCollection(
                                  assetKey
                                )
                              }
                              title="Adicionar/remover da coleção ativa"
                            >
                              {inCollection
                                ? "✓ Coleção"
                                : "+ Coleção"}
                            </button>
                            <button
                              type="button"
                              onClick={() =>
                                handlePreviewSplineLibraryAsset(
                                  entry
                                )
                              }
                            >
                              Prévia
                            </button>
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
                      );
                    }
                  )}

                  {!loadingSplineLibrary &&
                    filteredSplineLibrary.length ===
                      0 && (
                      <div className="explorer-empty">
                        Nenhum .sli encontrado neste grupo.
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
            <div
              className="citybuilder-hud"
              aria-label="Ferramentas principais do editor"
            >
              <div className="citybuilder-context">
                <button
                  type="button"
                  className={
                    editorTool === "select" &&
                    !activeConstructionTool
                      ? "city-hud-action active"
                      : "city-hud-action"
                  }
                  onClick={() => {
                    handleCancelPlacement();
                    handleCancelSplinePlacement();
                    setActiveConstructionTool(
                      undefined
                    );
                    setEditorTool("select");
                    setSelectionMode("all");
                  }}
                  title="Selecionar no mapa (Q)"
                >
                  <span>↖</span>
                  <small>Selecionar</small>
                </button>

                <button
                  type="button"
                  className={
                    (
                      isFullScreen
                        ? fullScreenPanel ===
                          "explorer"
                        : desktopExplorerOpen
                    )
                      ? "city-hud-action active"
                      : "city-hud-action"
                  }
                  onClick={() => {
                    if (isFullScreen) {
                      setFullScreenPanel(
                        fullScreenPanel ===
                          "explorer"
                          ? undefined
                          : "explorer"
                      );
                    } else {
                      setDesktopExplorerOpen(
                        (current) =>
                          !current
                      );
                    }
                  }}
                  title="Explorador e bibliotecas"
                >
                  <span>☰</span>
                  <small>Explorar</small>
                </button>

                <button
                  type="button"
                  className={
                    (
                      isFullScreen
                        ? fullScreenPanel ===
                          "inspector"
                        : desktopInspectorOpen
                    )
                      ? "city-hud-action active"
                      : "city-hud-action"
                  }
                  onClick={() => {
                    if (isFullScreen) {
                      setFullScreenPanel(
                        fullScreenPanel ===
                          "inspector"
                          ? undefined
                          : "inspector"
                      );
                    } else {
                      setDesktopInspectorOpen(
                        (current) =>
                          !current
                      );
                    }
                  }}
                  title="Inspetor da seleção"
                >
                  <span>ⓘ</span>
                  <small>Inspetor</small>
                </button>
              </div>

              <div className="citybuilder-main-tools">
                {([
                  ["road", "═", "Ruas"],
                  ["junction", "✣", "Cruzamentos"],
                  ["bridge", "⌁", "Pontes"],
                  ["building", "⌂", "Prédios"],
                  ["tree", "♣", "Vegetação"],
                  ["transit", "▤", "Transporte"],
                  ["street", "⚑", "Mobiliário"],
                  ["utilities", "⚙", "Infra"],
                  ["terrain", "▱", "Terreno"]
                ] as const).map(
                  ([tool, icon, label]) => (
                    <button
                      type="button"
                      key={tool}
                      className={
                        activeConstructionTool ===
                        tool
                          ? "city-construction-tool active"
                          : "city-construction-tool"
                      }
                      onClick={() =>
                        openQuickCreate(
                          tool
                        )
                      }
                      title={label}
                    >
                      <span>{icon}</span>
                      <small>{label}</small>
                    </button>
                  )
                )}
              </div>

              <div className="citybuilder-context citybuilder-context-right">
                <button
                  type="button"
                  className={
                    showConstructionSetPanel
                      ? "city-hud-action active"
                      : "city-hud-action"
                  }
                  onClick={() =>
                    setShowConstructionSetPanel(
                      (current) =>
                        !current
                    )
                  }
                  title="Conjuntos de construção"
                >
                  <span>▦</span>
                  <small>Conjuntos</small>
                </button>

                <button
                  type="button"
                  className={
                    showMapHealthPanel
                      ? "city-hud-action active"
                      : mapHealthIssueCount >
                          0
                        ? "city-hud-action warning"
                        : "city-hud-action"
                  }
                  onClick={() =>
                    setShowMapHealthPanel(
                      (current) =>
                        !current
                    )
                  }
                  title="Saúde do mapa"
                >
                  <span>
                    {mapHealthIssueCount >
                    0
                      ? "⚠"
                      : "✓"}
                  </span>
                  <small>Saúde</small>
                </button>
              </div>

              {activeConstructionTool &&
                activeConstructionTool !==
                  "terrain" && (
                <div
                  className="citybuilder-asset-shelf"
                  aria-label="Assets da categoria de construção ativa"
                >
                  <div className="citybuilder-asset-shelf-heading">
                    <span>
                      {activeConstructionTool ===
                        "road"
                        ? "═"
                        : activeConstructionTool ===
                            "bridge"
                          ? "⌁"
                          : activeConstructionTool ===
                              "junction"
                            ? "✣"
                            : activeConstructionTool ===
                                "building"
                              ? "⌂"
                              : activeConstructionTool ===
                                  "tree" ||
                                  activeConstructionTool ===
                                    "grass"
                                ? "♣"
                                : activeConstructionTool ===
                                    "transit"
                                  ? "▤"
                                  : activeConstructionTool ===
                                      "street"
                                    ? "⚑"
                                    : activeConstructionTool ===
                                        "utilities"
                                      ? "⚙"
                                      : "◇"}
                    </span>
                    <div>
                      <strong>
                        {activeConstructionTool ===
                          "road"
                          ? "Ruas"
                          : activeConstructionTool ===
                              "bridge"
                            ? "Pontes / elevados"
                            : activeConstructionTool ===
                                "junction"
                              ? "Cruzamentos"
                              : activeConstructionTool ===
                                  "building"
                                ? "Casas / prédios"
                                : activeConstructionTool ===
                                      "tree" ||
                                    activeConstructionTool ===
                                      "grass"
                                  ? "Vegetação"
                                  : activeConstructionTool ===
                                      "transit"
                                    ? "Transporte"
                                    : activeConstructionTool ===
                                        "street"
                                      ? "Mobiliário"
                                      : activeConstructionTool ===
                                          "utilities"
                                        ? "Infraestrutura"
                                        : activeConstructionTool ===
                                            "water"
                                          ? "Água / cenário"
                                          : "Objetos"}
                      </strong>
                      <small>
                        Clique para selecionar um asset real e depois posicione no mapa
                      </small>
                    </div>
                  </div>

                  <div className="citybuilder-asset-track">
                    {(
                      activeConstructionTool ===
                        "road" ||
                      activeConstructionTool ===
                        "bridge"
                    ) ? (
                      loadingSplineLibrary ? (
                        <span className="citybuilder-asset-empty">
                          Lendo Splines reais…
                        </span>
                      ) : filteredSplineLibrary
                          .slice()
                          .sort((left, right) => {
                            const leftFavorite =
                              splineFavorites.includes(
                                left.splinePath
                              )
                                ? 1
                                : 0;
                            const rightFavorite =
                              splineFavorites.includes(
                                right.splinePath
                              )
                                ? 1
                                : 0;

                            if (
                              leftFavorite !==
                              rightFavorite
                            ) {
                              return (
                                rightFavorite -
                                leftFavorite
                              );
                            }

                            const leftRecent =
                              splineRecent.indexOf(
                                left.splinePath
                              );
                            const rightRecent =
                              splineRecent.indexOf(
                                right.splinePath
                              );

                            if (
                              leftRecent !== -1 ||
                              rightRecent !== -1
                            ) {
                              if (
                                leftRecent === -1
                              ) {
                                return 1;
                              }

                              if (
                                rightRecent === -1
                              ) {
                                return -1;
                              }

                              if (
                                leftRecent !==
                                rightRecent
                              ) {
                                return (
                                  leftRecent -
                                  rightRecent
                                );
                              }
                            }

                            return (
                              (splineUsage[
                                right.splinePath
                              ] ?? 0) -
                              (splineUsage[
                                left.splinePath
                              ] ?? 0)
                            );
                          })
                          .slice(0, 14)
                          .map((entry) => {
                            const assetKey =
                              "sli:" +
                              entry.splinePath;
                            const selected =
                              splineLibraryPlacementAsset
                                ?.splinePath ===
                              entry.splinePath;
                            const groupInfo =
                              splineLibraryGroups.find(
                                (group) =>
                                  group.id ===
                                  getSplineLibraryGroup(
                                    entry
                                  )
                              );

                            return (
                              <button
                                type="button"
                                key={
                                  entry.splinePath
                                }
                                className={
                                  selected
                                    ? "citybuilder-asset-card active"
                                    : "citybuilder-asset-card"
                                }
                                onClick={() => {
                                  handleSelectSplineLibraryAsset(
                                    entry,
                                    false
                                  );

                                  if (isFullScreen) {
                                    setFullScreenPanel(
                                      "inspector"
                                    );
                                  } else {
                                    setDesktopExplorerOpen(
                                      false
                                    );
                                    setDesktopInspectorOpen(
                                      true
                                    );
                                  }
                                }}
                                onDoubleClick={() => {
                                  handlePreviewSplineLibraryAsset(
                                    entry
                                  );
                                  handleExplorerPanelTab(
                                    "splineLibrary"
                                  );
                                }}
                                title={
                                  entry.splinePath +
                                  " · duplo clique abre a prévia 3D"
                                }
                              >
                                {assetThumbnailCache[
                                  assetKey
                                ] ? (
                                  <img
                                    src={
                                      assetThumbnailCache[
                                        assetKey
                                      ]
                                    }
                                    alt=""
                                    draggable={
                                      false
                                    }
                                  />
                                ) : (
                                  <span
                                    className="citybuilder-asset-icon"
                                    aria-hidden="true"
                                  >
                                    {groupInfo?.icon ??
                                      "═"}
                                  </span>
                                )}
                                <strong>
                                  {entry.fileName}
                                </strong>
                                <small>
                                  {splineFavorites.includes(
                                    entry.splinePath
                                  )
                                    ? "★ "
                                    : ""}
                                  {getSplineLibrarySubcategory(
                                    entry,
                                    getSplineLibraryGroup(
                                      entry
                                    )
                                  )}
                                </small>
                              </button>
                            );
                          })
                    ) : loadingSceneryLibrary ? (
                      <span className="citybuilder-asset-empty">
                        Lendo Sceneryobjects reais…
                      </span>
                    ) : filteredSceneryLibrary
                        .slice()
                        .sort((left, right) => {
                          const leftFavorite =
                            sceneryFavorites.includes(
                              left.sceneryObjectPath
                            )
                              ? 1
                              : 0;
                          const rightFavorite =
                            sceneryFavorites.includes(
                              right.sceneryObjectPath
                            )
                              ? 1
                              : 0;

                          if (
                            leftFavorite !==
                            rightFavorite
                          ) {
                            return (
                              rightFavorite -
                              leftFavorite
                            );
                          }

                          const leftRecent =
                            sceneryRecent.indexOf(
                              left.sceneryObjectPath
                            );
                          const rightRecent =
                            sceneryRecent.indexOf(
                              right.sceneryObjectPath
                            );

                          if (
                            leftRecent !== -1 ||
                            rightRecent !== -1
                          ) {
                            if (
                              leftRecent === -1
                            ) {
                              return 1;
                            }

                            if (
                              rightRecent === -1
                            ) {
                              return -1;
                            }

                            if (
                              leftRecent !==
                              rightRecent
                            ) {
                              return (
                                leftRecent -
                                rightRecent
                              );
                            }
                          }

                          return (
                            (sceneryUsage[
                              right.sceneryObjectPath
                            ] ?? 0) -
                            (sceneryUsage[
                              left.sceneryObjectPath
                            ] ?? 0)
                          );
                        })
                        .slice(0, 14)
                        .map((entry) => {
                          const group =
                            getSceneryLibraryGroup(
                              entry,
                              sceneryMetadataByPath[
                                entry.sceneryObjectPath
                              ],
                              geometryByPath[
                                entry.sceneryObjectPath
                              ]
                            );
                          const groupInfo =
                            sceneryLibraryGroups.find(
                              (candidate) =>
                                candidate.id ===
                                group
                            );
                          const assetKey =
                            "sco:" +
                            entry.sceneryObjectPath;
                          const selected =
                            placementAsset
                              ?.sceneryObjectPath ===
                            entry.sceneryObjectPath;

                          return (
                            <button
                              type="button"
                              key={
                                entry.sceneryObjectPath
                              }
                              className={
                                selected
                                  ? "citybuilder-asset-card active"
                                  : "citybuilder-asset-card"
                              }
                              onClick={() => {
                                handleSelectPlacementAsset(
                                  entry
                                );

                                if (isFullScreen) {
                                  setFullScreenPanel(
                                    "inspector"
                                  );
                                } else {
                                  setDesktopExplorerOpen(
                                    false
                                  );
                                  setDesktopInspectorOpen(
                                    true
                                  );
                                }
                              }}
                              onDoubleClick={() => {
                                handlePreviewSceneryLibraryAsset(
                                  entry
                                );
                                handleExplorerPanelTab(
                                  "library"
                                );
                              }}
                              title={
                                entry.sceneryObjectPath +
                                " · duplo clique abre a prévia 3D"
                              }
                            >
                              {assetThumbnailCache[
                                assetKey
                              ] ? (
                                <img
                                  src={
                                    assetThumbnailCache[
                                      assetKey
                                    ]
                                  }
                                  alt=""
                                  draggable={
                                    false
                                  }
                                />
                              ) : (
                                <span
                                  className="citybuilder-asset-icon"
                                  aria-hidden="true"
                                >
                                  {groupInfo?.icon ??
                                    "◇"}
                                </span>
                              )}
                              <strong>
                                {entry.fileName}
                              </strong>
                              <small>
                                {sceneryFavorites.includes(
                                  entry.sceneryObjectPath
                                )
                                  ? "★ "
                                  : ""}
                                {getSceneryLibrarySubcategory(
                                  entry,
                                  group
                                )}
                              </small>
                            </button>
                          );
                        })}

                    {!loadingSplineLibrary &&
                      !loadingSceneryLibrary &&
                      (
                        (
                          activeConstructionTool ===
                            "road" ||
                          activeConstructionTool ===
                            "bridge"
                        )
                          ? filteredSplineLibrary
                              .length === 0
                          : filteredSceneryLibrary
                              .length === 0
                      ) && (
                        <span className="citybuilder-asset-empty">
                          Nenhum asset real encontrado nesta categoria.
                        </span>
                      )}
                  </div>

                  <button
                    type="button"
                    className="citybuilder-open-library"
                    onClick={() =>
                      handleExplorerPanelTab(
                        activeConstructionTool ===
                            "road" ||
                          activeConstructionTool ===
                            "bridge"
                          ? "splineLibrary"
                          : "library"
                      )
                    }
                    title="Abrir biblioteca completa com busca, grupos, filtros e prévia 3D"
                  >
                    ☰ Biblioteca
                  </button>
                </div>
              )}

              <div className="citybuilder-subbar">
                <div className="citybuilder-current-mode">
                  <strong>
                    {selectedObject
                      ? getObjectName(
                          selectedObject
                            .sceneryObjectPath
                        )
                      : selectedSpline
                        ? getObjectName(
                            selectedSpline
                              .splinePath
                          )
                        : activeConstructionTool
                          ? "Modo de construção"
                          : "Seleção livre"}
                  </strong>
                  <span>
                    {selectedObject
                      ? `Objeto #${selectedObject.objectId} · clique em outro item para trocar a seleção`
                      : selectedSpline
                        ? `Spline #${selectedSpline.splineId} · clique em outra via para trocar a seleção`
                        : activeConstructionTool
                          ? "Escolha o asset e clique no mapa para construir"
                          : "Clique diretamente em objeto ou spline no mapa"}
                  </span>
                </div>

                {easyRoadMode &&
                  (
                    roadPlacementKind ===
                      "road" ||
                    roadPlacementKind ===
                      "bridge"
                  ) && (
                  <div
                    className="citybuilder-road-guide"
                    title="Guia contextual da via ativa"
                  >
                    <span className="road-guide-primary">
                      1 Início → 2 Fim → 3 Curva
                    </span>
                    <button
                      type="button"
                      className={
                        roadEndpointSnapEnabled
                          ? "active"
                          : ""
                      }
                      onClick={() => {
                        const enabled =
                          !roadEndpointSnapEnabled;

                        setRoadEndpointSnapEnabled(
                          enabled
                        );

                        if (!enabled) {
                          setRoadStartSnap(
                            undefined
                          );
                          setRoadEndSnap(
                            undefined
                          );
                        }
                      }}
                      title="Ativar/desativar encaixe nas pontas das vias existentes"
                    >
                      Snap pontas{" "}
                      {roadEndpointSnapEnabled
                        ? "ON"
                        : "OFF"}
                    </button>
                    <button
                      type="button"
                      className={
                        roadAutoConnectEnabled
                          ? "active"
                          : ""
                      }
                      onClick={() =>
                        setRoadAutoConnectEnabled(
                          (current) =>
                            !current
                        )
                      }
                      title="Alternar atualização automática de previous/next"
                    >
                      previous/next{" "}
                      {roadAutoConnectEnabled
                        ? "AUTO"
                        : "manual"}
                    </button>
                    {(roadStartSnap ||
                      roadEndSnap) && (
                      <span className="active">
                        {roadStartSnap
                          ? "I✓"
                          : "I–"}{" "}
                        {roadEndSnap
                          ? "F✓"
                          : "F–"}
                      </span>
                    )}
                    {roadPlacementKind ===
                      "bridge" && (
                      <div className="road-guide-elevation">
                        <button
                          type="button"
                          onClick={() =>
                            handleRoadElevationOffsetChange(
                              roadElevationOffset -
                                0.5
                            )
                          }
                          title="Baixar ponte em 0,5 m"
                        >
                          −
                        </button>
                        <span className="active">
                          Elevação{" "}
                          {formatNumber(
                            roadElevationOffset
                          )}{" "}
                          m
                        </span>
                        <button
                          type="button"
                          onClick={() =>
                            handleRoadElevationOffsetChange(
                              roadElevationOffset +
                                0.5
                            )
                          }
                          title="Elevar ponte em 0,5 m"
                        >
                          +
                        </button>
                      </div>
                    )}
                  </div>
                )}

                <div className="citybuilder-selection-filters">
                  {([
                    ["all", "Tudo"],
                    ["object", "Objetos"],
                    ["spline", "Splines"],
                    ["terrain", "Terreno"]
                  ] as const).map(
                    ([mode, label]) => (
                      <button
                        type="button"
                        key={mode}
                        className={
                          selectionMode ===
                          mode
                            ? "active"
                            : ""
                        }
                        onClick={() => {
                          setSelectionMode(
                            mode
                          );
                          setEditorTool(
                            "select"
                          );
                          setActiveConstructionTool(
                            undefined
                          );

                          if (
                            mode ===
                            "terrain"
                          ) {
                            setShowTerrain(
                              true
                            );
                          }
                        }}
                      >
                        {label}
                      </button>
                    )
                  )}
                </div>

                <button
                  type="button"
                  className={
                    snapEnabled
                      ? "city-sub-action active"
                      : "city-sub-action"
                  }
                  onClick={() =>
                    setSnapEnabled(
                      (current) =>
                        !current
                    )
                  }
                  title="Alternar snap (N)"
                >
                  Snap
                </button>
              </div>
            </div>

            <>
                <div
                  className={[
                    "fullscreen-tool-dock",
                    "editor-quick-dock",
                    isFullScreen
                      ? "fullscreen-mode"
                      : "desktop-mode",
                    "floating-tool"
                  ].join(" ")}
                  data-floating-tool
                  data-floating-tool-id="editor-quick-dock"
                >
                  <button
                    type="button"
                    className="tool-drag-grip drag-handle"
                    data-drag-handle
                    title="Arraste para mover as ferramentas"
                    aria-label="Mover barra de ferramentas"
                  >
                    ⋮⋮
                  </button>
                  <div className="fullscreen-tool-group fullscreen-primary">
                    <button type="button" className={editorTool === "select" ? "active" : ""} onClick={() => setEditorTool("select")} title="Selecionar (Q)">↖ <span>Q</span></button>
                    <button type="button" className={editorTool === "move" ? "active" : ""} disabled={!selectedObject && !selectedSpline} onClick={() => setEditorTool("move")} title="Mover (W)">✥ <span>W</span></button>
                    <button type="button" className={editorTool === "rotate" ? "active" : ""} disabled={!selectedObject && !selectedSpline} onClick={() => setEditorTool("rotate")} title="Rotacionar (E)">⟳ <span>E</span></button>
                    <button type="button" onClick={() => requestCameraAction("fit")} title="Enquadrar mapa (Home)">⛶</button>
                    <button type="button" disabled={!selectedObject && !selectedSpline} onClick={() => requestCameraAction("focus")} title="Focar seleção (F)">◎ <span>F</span></button>
                  </div>
                  <div className="fullscreen-tool-divider" />
                  <div className="fullscreen-tool-group fullscreen-panels">
                    <button
                      type="button"
                      onClick={() => {
                        handleExplorerPanelTab(
                          "map"
                        );
                        if (isFullScreen) {
                          setFullScreenPanel(
                            "explorer"
                          );
                        } else {
                          setDesktopExplorerOpen(
                            true
                          );
                        }
                      }}
                      title="Abrir Explorador"
                    >
                      ☰ <span>Explorar</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        handleExplorerPanelTab(
                          "library"
                        );
                        if (isFullScreen) {
                          setFullScreenPanel(
                            "explorer"
                          );
                        } else {
                          setDesktopExplorerOpen(
                            true
                          );
                        }
                      }}
                      title="Criar/colocar objeto real"
                    >
                      ＋ <span>Objeto</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        handleExplorerPanelTab(
                          "splineLibrary"
                        );
                        if (isFullScreen) {
                          setFullScreenPanel(
                            "explorer"
                          );
                        } else {
                          setDesktopExplorerOpen(
                            true
                          );
                        }
                      }}
                      title="Criar/colocar spline real"
                    >
                      ⌇＋ <span>Spline</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        if (isFullScreen) {
                          setFullScreenPanel(
                            (current) =>
                              current ===
                              "inspector"
                                ? undefined
                                : "inspector"
                          );
                        } else {
                          setDesktopInspectorOpen(
                            (current) =>
                              !current
                          );
                        }
                      }}
                      title="Abrir Inspetor"
                    >
                      ⓘ <span>Inspetor</span>
                    </button>
                  </div>
                  <div className="fullscreen-tool-divider" />
                  <div className="fullscreen-tool-group fullscreen-history">
                    <button type="button" className={snapEnabled ? "active" : ""} onClick={() => setSnapEnabled((current) => !current)} title="Snap (N)">N</button>
                    <button type="button" disabled={undoPreviewStack.length === 0} onClick={handleUndoPreview} title="Desfazer (Ctrl+Z)">↶</button>
                    <button type="button" disabled={redoPreviewStack.length === 0} onClick={handleRedoPreview} title="Refazer (Ctrl+Y)">↷</button>
                    <button type="button" className="save" disabled={(previewEditCount === 0 && splinePreviewEditCount === 0) || busy} onClick={splinePreviewEditCount > 0 ? handleSaveSplinePreview : handleSavePreviewEdits} title="Salvar com backup (Ctrl+S)">✓ <span>Salvar</span></button>
                  </div>
                  <div className="fullscreen-tool-divider" />
                  <div className="fullscreen-tool-group compact fullscreen-layers">
                    <button type="button" className={showTerrain ? "active" : ""} onClick={() => setShowTerrain((current) => !current)} title="Terreno">T</button>
                    <button type="button" className={showGrid ? "active" : ""} onClick={() => setShowGrid((current) => !current)} title="Grade (G)">G</button>
                    <button type="button" className={showObjects ? "active" : ""} onClick={() => setShowObjects((current) => !current)} title="Objetos (O)">O</button>
                    <button type="button" className={showSplines ? "active" : ""} onClick={() => setShowSplines((current) => !current)} title="Splines (L)">L</button>
                    <button type="button" className={showSplineProfiles ? "active" : ""} onClick={() => setShowSplineProfiles((current) => !current)} title="Perfis reais das splines">P</button>
                    <button
                      type="button"
                      className={
                        isFullScreen
                          ? "exit active"
                          : ""
                      }
                      onClick={() =>
                        requestFullScreen(
                          !isFullScreen
                        )
                      }
                      title={
                        isFullScreen
                          ? "Sair da tela cheia (F11/Esc)"
                          : "Entrar em tela cheia (F11)"
                      }
                    >
                      {isFullScreen
                        ? "⤡"
                        : "⤢"}
                    </button>
                  </div>
                </div>
                {isFullScreen && (
                  <div className="fullscreen-shortcuts">
                    <strong>Atalhos</strong>
                    <span>Q selecionar</span><span>W mover</span><span>E rotacionar</span>
                    <span>1 perspectiva</span><span>2 topo</span><span>N snap</span>
                    <span>F foco</span><span>Home enquadrar</span><span>G grade</span>
                    <span>Alt+1..4 filtro seleção</span><span>Alt+R/C/O criar rua/cruz./objeto</span>
                    <span>Alt+T/A/G/Y terreno/água/grama/árvore</span>
                    <span>O objetos</span><span>L splines</span><span>Ctrl+S salvar</span>
                    <span>Ctrl+Z/Y desfazer/refazer</span><span>RMB orbitar</span>
                    <span>MMB deslocar</span><span>roda zoom</span>
                  </div>
                )}
              </>
            {showTileNavigator &&
              activeTile && (
              <div
                className="tile-navigator floating-tool"
                data-floating-tool
                aria-label="Navegação entre blocos do mapa"
              >
                <div
                  className="tile-navigator-title drag-handle"
                  data-drag-handle
                  title="Arraste para mover esta ferramenta"
                >
                  <strong>Blocos</strong>
                  <span>
                    Tile {activeTile.x},{activeTile.y}
                  </span>
                </div>

                <div className="tile-navigator-grid">
                  {[
                    [-1, -1],
                    [0, -1],
                    [1, -1],
                    [-1, 0],
                    [0, 0],
                    [1, 0],
                    [-1, 1],
                    [0, 1],
                    [1, 1]
                  ].map(
                    ([offsetX, offsetY]) => {
                      const tileX =
                        activeTile.x +
                        offsetX;
                      const tileY =
                        activeTile.y +
                        offsetY;

                      const exists =
                        selectedMap.tiles.some(
                          (tile) =>
                            tile.x === tileX &&
                            tile.y === tileY
                        );

                      const current =
                        offsetX === 0 &&
                        offsetY === 0;

                      return (
                        <button
                          type="button"
                          key={`${offsetX}:${offsetY}`}
                          className={
                            current
                              ? "active"
                              : ""
                          }
                          disabled={!exists}
                          onClick={() =>
                            focusTile(
                              tileX,
                              tileY
                            )
                          }
                          title={
                            exists
                              ? `Ir para tile ${tileX},${tileY}`
                              : `Tile ${tileX},${tileY} não existe neste mapa`
                          }
                        >
                          <span>
                            {current
                              ? "●"
                              : offsetX === 0 &&
                                  offsetY === -1
                                ? "↑"
                                : offsetX === 0 &&
                                    offsetY === 1
                                  ? "↓"
                                  : offsetX === -1 &&
                                      offsetY === 0
                                    ? "←"
                                    : offsetX === 1 &&
                                        offsetY === 0
                                      ? "→"
                                      : "·"}
                          </span>
                          <small>
                            {tileX},{tileY}
                          </small>
                        </button>
                      );
                    }
                  )}
                </div>

                <small>
                  Ctrl + setas/WASD também move 1 bloco
                </small>
              </div>
            )}

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
              selectionMode={
                selectionMode
              }
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
              showAllSplineProfiles={
                mapLoadMode === "full"
              }
              nightPreviewEnabled={
                nightPreviewEnabled
              }
              skyTextureAsset={
                textureAssetsByKey[
                  getSkyTextureAssetKey(
                    nightPreviewEnabled
                      ? "himmel05.bmp"
                      : "himmel01.bmp"
                  )
                ]
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
              pendingPlacementBatch={
                pendingPlacementBatch
              }
              onPlacementPoint={
                handlePlacementPoint
              }
              onLibraryAssetDrop={
                handleLibraryAssetDrop
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
              roadDragMode={
                easyRoadMode &&
                !splineLibraryPlacementIsHeight
              }
              roadCurveControl={
                easyRoadMode &&
                easyRoadStart &&
                easyRoadEnd
                  ? {
                      start:
                        easyRoadStart,
                      end:
                        easyRoadEnd,
                      offset:
                        easyRoadCurveOffset
                    }
                  : undefined
              }
              onRoadCurveOffsetChange={
                handleEasyRoadCurveChange
              }
              onRoadControlPointChange={
                handleRoadControlPointChange
              }
              activeTile={activeTile}
              onTerrainPoint={
                handleTerrainPoint
              }
              referenceOverlay={
                referenceOverlay
              }
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
                  setTerrainEditPoint(
                    undefined
                  );
                  setGoogleElevationGrid(
                    undefined
                  );

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

            {selectionMode === "terrain" && (
              <div
                className="terrain-edit-panel floating-tool"
                data-floating-tool
              >
                <div
                  className="panel-title-row drag-handle"
                  data-drag-handle
                  title="Arraste para mover esta ferramenta"
                >
                  <strong>Nivelamento manual</strong>
                  <span>
                    {terrainEditPoint
                      ? `Tile ${terrainEditPoint.tileX},${terrainEditPoint.tileY} · X ${formatNumber(terrainEditPoint.x)} · Y ${formatNumber(terrainEditPoint.y)} · atual ${formatNumber(terrainEditPoint.height)} m`
                      : "Clique diretamente no terreno para marcar o centro do pincel."}
                  </span>
                </div>

                <label>
                  <span>Altura alvo (m)</span>
                  <input
                    type="number"
                    step="0.1"
                    value={terrainTargetHeight}
                    onChange={(event) => {
                      const value =
                        event.currentTarget
                          .valueAsNumber;
                      if (Number.isFinite(value)) {
                        setTerrainTargetHeight(value);
                      }
                    }}
                  />
                </label>

                <label>
                  <span>Raio (m)</span>
                  <input
                    type="number"
                    min="1"
                    max="600"
                    step="1"
                    value={terrainBrushRadius}
                    onChange={(event) => {
                      const value =
                        event.currentTarget
                          .valueAsNumber;
                      if (
                        Number.isFinite(value) &&
                        value > 0
                      ) {
                        setTerrainBrushRadius(value);
                      }
                    }}
                  />
                </label>

                <label>
                  <span>Suavização</span>
                  <input
                    type="number"
                    min="0"
                    max="1"
                    step="0.05"
                    value={terrainBrushFeather}
                    onChange={(event) => {
                      const value =
                        event.currentTarget
                          .valueAsNumber;
                      if (
                        Number.isFinite(value) &&
                        value >= 0 &&
                        value <= 1
                      ) {
                        setTerrainBrushFeather(value);
                      }
                    }}
                  />
                </label>

                <button
                  type="button"
                  className="primary-button"
                  disabled={
                    !terrainEditPoint ||
                    savingTerrain
                  }
                  onClick={handleLevelTerrain}
                >
                  {savingTerrain
                    ? "Nivelando..."
                    : "Aplicar nivelamento"}
                </button>
              </div>
            )}

            {showRealMapPanel && (
              <div
                className="real-map-panel floating-tool"
                data-floating-tool
              >
              <div
                className="panel-title-row drag-handle"
                data-drag-handle
                title="Arraste para mover esta ferramenta"
              >
                <strong>Mapa real por coordenadas</strong>
                <span>
                  Google Maps + elevação como referência visual sobre o terreno.
                </span>
              </div>

              <label>
                <span>Google API key</span>
                <input
                  type="password"
                  value={googleApiKey}
                  placeholder="Static Maps + Elevation"
                  onChange={(event) =>
                    setGoogleApiKey(
                      event.target.value
                    )
                  }
                />
              </label>

              <label>
                <span>Latitude</span>
                <input
                  type="text"
                  inputMode="decimal"
                  value={googleLatitude}
                  placeholder="-23.5505"
                  onChange={(event) =>
                    setGoogleLatitude(
                      event.target.value
                    )
                  }
                />
              </label>

              <label>
                <span>Longitude</span>
                <input
                  type="text"
                  inputMode="decimal"
                  value={googleLongitude}
                  placeholder="-46.6333"
                  onChange={(event) =>
                    setGoogleLongitude(
                      event.target.value
                    )
                  }
                />
              </label>

              <label>
                <span>Zoom</span>
                <input
                  type="number"
                  min="0"
                  max="22"
                  value={googleZoom}
                  onChange={(event) => {
                    const value =
                      event.currentTarget
                        .valueAsNumber;
                    if (
                      Number.isInteger(value) &&
                      value >= 0 &&
                      value <= 22
                    ) {
                      setGoogleZoom(value);
                    }
                  }}
                />
              </label>

              <label>
                <span>Imagem</span>
                <select
                  value={googleMapType}
                  onChange={(event) =>
                    setGoogleMapType(
                      event.target.value as
                        | "roadmap"
                        | "satellite"
                        | "hybrid"
                        | "terrain"
                    )
                  }
                >
                  <option value="hybrid">Híbrido</option>
                  <option value="satellite">Satélite</option>
                  <option value="roadmap">Ruas</option>
                  <option value="terrain">Terreno</option>
                </select>
              </label>

              <button
                type="button"
                className="secondary-action"
                disabled={
                  loadingGoogleReference
                }
                onClick={
                  handleLoadGoogleReference
                }
              >
                {loadingGoogleReference
                  ? "Carregando..."
                  : "Carregar referência"}
              </button>

              {googleReference && (
                <>
                  <label className="reference-toggle">
                    <input
                      type="checkbox"
                      checked={referenceVisible}
                      onChange={(event) =>
                        setReferenceVisible(
                          event.target.checked
                        )
                      }
                    />
                    <span>Mostrar sobre o terreno</span>
                  </label>

                  <label>
                    <span>Opacidade</span>
                    <input
                      type="range"
                      min="0.1"
                      max="1"
                      step="0.05"
                      value={referenceOpacity}
                      onChange={(event) =>
                        setReferenceOpacity(
                          Number(
                            event.target.value
                          )
                        )
                      }
                    />
                  </label>

                  <div className="reference-meta">
                    <span>
                      Escala: {formatNumber(
                        googleReference
                          .metersPerPixel
                      )} m/px
                    </span>
                    <span>
                      Elevação central:{" "}
                      {googleReference
                        .centerElevation !== null
                        ? `${formatNumber(
                            googleReference
                              .centerElevation
                          )} m`
                        : "indisponível"}
                    </span>
                    <span>
                      Âncora: tile {georefAnchor.tileX},{georefAnchor.tileY} · {formatNumber(georefAnchor.x)},{formatNumber(georefAnchor.y)}
                    </span>
                  </div>

                  <div className="reference-elevation-tools">
                    <label>
                      <span>Amostras do tile</span>
                      <select
                        value={elevationSampleCount}
                        onChange={(event) =>
                          setElevationSampleCount(
                            Number(
                              event.target.value
                            )
                          )
                        }
                      >
                        <option value={9}>9×9 · rápido</option>
                        <option value={17}>17×17 · recomendado</option>
                        <option value={25}>25×25 · detalhado</option>
                        <option value={33}>33×33 · máximo</option>
                      </select>
                    </label>

                    <button
                      type="button"
                      className="secondary-action"
                      disabled={
                        !activeTile ||
                        loadingElevationGrid
                      }
                      onClick={
                        handleLoadElevationGrid
                      }
                    >
                      {loadingElevationGrid
                        ? "Lendo relevo..."
                        : activeTile
                          ? `Buscar relevo tile ${activeTile.x},${activeTile.y}`
                          : "Selecione um tile"}
                    </button>

                    {googleElevationGrid && (
                      <>
                        <div className="reference-meta">
                          <span>
                            Grade: {googleElevationGrid.rows}×{googleElevationGrid.columns}
                          </span>
                          <span>
                            Mín.: {formatNumber(
                              googleElevationGrid
                                .minimumElevation
                            )} m
                          </span>
                          <span>
                            Máx.: {formatNumber(
                              googleElevationGrid
                                .maximumElevation
                            )} m
                          </span>
                        </div>

                        <label>
                          <span>
                            Offset vertical (m)
                          </span>
                          <input
                            type="number"
                            step="0.1"
                            value={
                              elevationVerticalOffset
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
                                setElevationVerticalOffset(
                                  value
                                );
                              }
                            }}
                          />
                        </label>

                        <button
                          type="button"
                          className="primary-button"
                          disabled={
                            applyingElevationGrid
                          }
                          onClick={
                            handleApplyElevationGrid
                          }
                          title="Reamostrar a grade Google para o .terrain real do tile e criar backup"
                        >
                          {applyingElevationGrid
                            ? "Aplicando relevo..."
                            : "Aplicar relevo real ao tile"}
                        </button>
                      </>
                    )}
                  </div>

                  <button
                    type="button"
                    className="secondary-action"
                    onClick={
                      handleSaveMapGeoreference
                    }
                  >
                    Salvar coordenadas do mapa
                  </button>
                </>
              )}
            </div>
            )}

            {splinePlacementTemplate && (
              <div
                className="placement-bar floating-tool"
                data-floating-tool
              >
                <div
                  className="placement-bar-heading drag-handle"
                  data-drag-handle
                  title="Arraste para mover esta ferramenta"
                >
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

                {easyRoadMode && (
                  <div className="road-endpoint-snap-control">
                    <label className="placement-toggle">
                      <input
                        type="checkbox"
                        checked={
                          roadEndpointSnapEnabled
                        }
                        onChange={(event) => {
                          const enabled =
                            event.currentTarget
                              .checked;

                          setRoadEndpointSnapEnabled(
                            enabled
                          );

                          if (!enabled) {
                            setRoadStartSnap(
                              undefined
                            );
                            setRoadEndSnap(
                              undefined
                            );
                          }
                        }}
                      />
                      <span>
                        Encaixar nas pontas de vias existentes
                      </span>
                    </label>

                    {roadEndpointSnapEnabled && (
                      <label className="placement-field">
                        <span>
                          Alcance m
                        </span>
                        <input
                          type="number"
                          min="0.5"
                          max="30"
                          step="0.5"
                          value={
                            roadEndpointSnapDistance
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
                              setRoadEndpointSnapDistance(
                                Math.max(
                                  0.5,
                                  Math.min(
                                    30,
                                    value
                                  )
                                )
                              );
                            }
                          }}
                        />
                      </label>
                    )}

                    <label className="placement-toggle road-auto-connect-toggle">
                      <input
                        type="checkbox"
                        checked={
                          roadAutoConnectEnabled
                        }
                        onChange={(event) =>
                          setRoadAutoConnectEnabled(
                            event.currentTarget
                              .checked
                          )
                        }
                      />
                      <span>
                        Conectar vínculos previous/next automaticamente
                      </span>
                    </label>

                    <div className="road-snap-status">
                      {roadStartSnap && (
                        <span>
                          Início → spline #{roadStartSnap.splineId}
                          {" · "}
                          {roadStartSnap.endpoint ===
                            "start"
                            ? "ponta inicial"
                            : "ponta final"}
                          {" · "}
                          {formatNumber(
                            roadStartSnap.distance
                          )} m
                        </span>
                      )}
                      {roadEndSnap && (
                        <span>
                          Fim → spline #{roadEndSnap.splineId}
                          {" · "}
                          {roadEndSnap.endpoint ===
                            "start"
                            ? "ponta inicial"
                            : "ponta final"}
                          {" · "}
                          {formatNumber(
                            roadEndSnap.distance
                          )} m
                        </span>
                      )}

                      {roadAutoConnectEnabled &&
                        roadStartSnap && (
                        <span
                          className={
                            roadAutoLinkPlan
                              .previousSplineId !==
                            -1
                              ? "link-ready"
                              : "link-blocked"
                          }
                        >
                          previous:{" "}
                          {roadAutoLinkPlan
                            .previousStatus}
                        </span>
                      )}

                      {roadAutoConnectEnabled &&
                        roadEndSnap && (
                        <span
                          className={
                            roadAutoLinkPlan
                              .nextSplineId !==
                            -1
                              ? "link-ready"
                              : "link-blocked"
                          }
                        >
                          next:{" "}
                          {roadAutoLinkPlan
                            .nextStatus}
                        </span>
                      )}
                    </div>
                  </div>
                )}

                {easyRoadMode &&
                  roadPlacementKind ===
                    "bridge" && (
                  <div className="bridge-elevation-control">
                    <strong>
                      Ponte / elevado
                    </strong>
                    <label className="placement-field">
                      <span>
                        Elevação m
                      </span>
                      <input
                        type="number"
                        min="-20"
                        max="100"
                        step="0.5"
                        value={
                          roadElevationOffset
                        }
                        onChange={(event) =>
                          handleRoadElevationOffsetChange(
                            event.currentTarget
                              .valueAsNumber
                          )
                        }
                      />
                    </label>
                    <span>
                      A elevação é somada à altura real do terreno.
                    </span>
                  </div>
                )}

                {easyRoadMode &&
                  easyRoadStart &&
                  easyRoadEnd &&
                  pendingSplinePlacement &&
                  pendingSplinePlacement.length > 0 && (
                  <div className="road-curve-control">
                    <div>
                      <strong>Curva</strong>
                      <span>
                        Arraste a esfera azul para curvar. As esferas verde e vermelha movem início e fim do traçado.
                      </span>
                    </div>
                    <input
                      type="range"
                      min={
                        -Math.max(
                          2,
                          (
                            deriveRoadArc(
                              easyRoadStart,
                              easyRoadEnd,
                              0
                            )?.chordLength ??
                            10
                          ) * 0.45
                        )
                      }
                      max={
                        Math.max(
                          2,
                          (
                            deriveRoadArc(
                              easyRoadStart,
                              easyRoadEnd,
                              0
                            )?.chordLength ??
                            10
                          ) * 0.45
                        )
                      }
                      step="0.25"
                      value={
                        easyRoadCurveOffset
                      }
                      onChange={(event) =>
                        handleEasyRoadCurveChange(
                          Number(
                            event.target.value
                          )
                        )
                      }
                      aria-label="Curvatura lateral da rua"
                    />
                    <span className="road-curve-value">
                      Controle lateral: {formatNumber(
                        easyRoadCurveOffset
                      )} m · raio OMSI: {pendingSplinePlacement.radius === 0
                        ? "reta"
                        : `${formatNumber(
                            pendingSplinePlacement.radius
                          )} m`}
                    </span>
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() =>
                        handleEasyRoadCurveChange(
                          0
                        )
                      }
                    >
                      Endireitar
                    </button>
                  </div>
                )}

                {pendingSplinePlacement &&
                  pendingSplinePlacement.length > 0 && (
                  <button
                    type="button"
                    className="secondary-action"
                    disabled={insertingSpline}
                    onClick={
                      handleLevelPendingRoadToTerrain
                    }
                    title="Usar as alturas reais do terreno no início e fim da rua"
                  >
                    Nivelar ao terreno
                  </button>
                )}

                {easyRoadMode && (
                  <button
                    type="button"
                    className="secondary-action"
                    disabled={insertingSpline}
                    onClick={() => {
                      setEasyRoadStart(undefined);
                      setEasyRoadEnd(undefined);
                      setEasyRoadCurveOffset(0);
                      setRoadStartSnap(undefined);
                      setRoadEndSnap(undefined);
                      setPendingSplinePlacement(undefined);
                      setSaveNotice(
                        "Clique no início da rua, segure e arraste até o ponto final."
                      );
                    }}
                  >
                    Reiniciar traçado
                  </button>
                )}

                <button
                  type="button"
                  className="primary-button"
                  disabled={
                    !pendingSplinePlacement ||
                    pendingSplinePlacement.length <= 0 ||
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
              <div
                className="placement-bar floating-tool"
                data-floating-tool
              >
                <div
                  className="placement-bar-heading drag-handle"
                  data-drag-handle
                  title="Arraste para mover esta ferramenta"
                >
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

                <div className="placement-mode-panel">
                  <label className="placement-field placement-preset-field">
                    <span>Preset</span>
                    <select
                      value={
                        constructionPreset
                      }
                      onChange={(event) =>
                        applyConstructionPreset(
                          event.currentTarget
                            .value as ConstructionPresetId
                        )
                      }
                    >
                      {constructionPresets.map(
                        (preset) => (
                          <option
                            key={preset.id}
                            value={preset.id}
                          >
                            {preset.label}
                          </option>
                        )
                      )}
                    </select>
                  </label>

                  <div className="placement-mode-tabs">
                    {([
                      ["single", "Único"],
                      ["repeat", "Repetir"],
                      ["line", "Linha"],
                      ["area", "Pincel"],
                      ["matrix", "Matriz"],
                      ["circle", "Círculo"],
                      ["lot", "Lotes"]
                    ] as const).map(
                      ([mode, label]) => (
                        <button
                          type="button"
                          key={mode}
                          className={
                            placementMode ===
                            mode
                              ? "active"
                              : ""
                          }
                          disabled={
                            insertingObject
                          }
                          onClick={() => {
                            setPlacementMode(
                              mode
                            );
                            setConstructionPreset(
                              "none"
                            );
                            setPendingPlacementBatch(
                              []
                            );
                            setPlacementLineStart(
                              undefined
                            );
                          }}
                        >
                          {label}
                        </button>
                      )
                    )}
                  </div>

                  {(placementMode ===
                      "line" ||
                    placementMode ===
                      "lot" ||
                    placementMode ===
                      "repeat") && (
                    <label className="placement-field">
                      <span>Espaço m</span>
                      <input
                        type="number"
                        min="0.5"
                        max="100"
                        step="0.5"
                        value={
                          placementSpacing
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
                            setPlacementSpacing(
                              Math.max(
                                0.5,
                                value
                              )
                            );
                          }
                        }}
                      />
                    </label>
                  )}

                  {placementMode ===
                    "area" && (
                    <>
                      <label className="placement-field">
                        <span>Raio m</span>
                        <input
                          type="number"
                          min="1"
                          max="150"
                          step="1"
                          value={
                            placementBrushRadius
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
                              setPlacementBrushRadius(
                                Math.max(
                                  1,
                                  value
                                )
                              );
                            }
                          }}
                        />
                      </label>
                      <label className="placement-field">
                        <span>Qtd.</span>
                        <input
                          type="number"
                          min="1"
                          max="256"
                          step="1"
                          value={
                            placementBrushCount
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
                              setPlacementBrushCount(
                                Math.max(
                                  1,
                                  Math.min(
                                    256,
                                    Math.floor(
                                      value
                                    )
                                  )
                                )
                              );
                            }
                          }}
                        />
                      </label>
                    </>
                  )}

                  {placementMode ===
                    "matrix" && (
                    <>
                      <label className="placement-field">
                        <span>Linhas</span>
                        <input
                          type="number"
                          min="1"
                          max="16"
                          step="1"
                          value={placementMatrixRows}
                          onChange={(event) =>
                            setPlacementMatrixRows(
                              Math.max(
                                1,
                                Math.min(
                                  16,
                                  Math.floor(
                                    event.currentTarget
                                      .valueAsNumber ||
                                      1
                                  )
                                )
                              )
                            )
                          }
                        />
                      </label>
                      <label className="placement-field">
                        <span>Colunas</span>
                        <input
                          type="number"
                          min="1"
                          max="16"
                          step="1"
                          value={placementMatrixColumns}
                          onChange={(event) =>
                            setPlacementMatrixColumns(
                              Math.max(
                                1,
                                Math.min(
                                  16,
                                  Math.floor(
                                    event.currentTarget
                                      .valueAsNumber ||
                                      1
                                  )
                                )
                              )
                            )
                          }
                        />
                      </label>
                      <label className="placement-field">
                        <span>Esp. X</span>
                        <input
                          type="number"
                          min="0.5"
                          step="0.5"
                          value={placementMatrixSpacingX}
                          onChange={(event) =>
                            setPlacementMatrixSpacingX(
                              Math.max(
                                0.5,
                                event.currentTarget
                                  .valueAsNumber ||
                                  0.5
                              )
                            )
                          }
                        />
                      </label>
                      <label className="placement-field">
                        <span>Esp. Y</span>
                        <input
                          type="number"
                          min="0.5"
                          step="0.5"
                          value={placementMatrixSpacingY}
                          onChange={(event) =>
                            setPlacementMatrixSpacingY(
                              Math.max(
                                0.5,
                                event.currentTarget
                                  .valueAsNumber ||
                                  0.5
                              )
                            )
                          }
                        />
                      </label>
                    </>
                  )}

                  {placementMode ===
                    "circle" && (
                    <>
                      <label className="placement-field">
                        <span>Raio m</span>
                        <input
                          type="number"
                          min="0.5"
                          step="0.5"
                          value={placementCircleRadius}
                          onChange={(event) =>
                            setPlacementCircleRadius(
                              Math.max(
                                0.5,
                                event.currentTarget
                                  .valueAsNumber ||
                                  0.5
                              )
                            )
                          }
                        />
                      </label>
                      <label className="placement-field">
                        <span>Qtd.</span>
                        <input
                          type="number"
                          min="1"
                          max="256"
                          step="1"
                          value={placementCircleCount}
                          onChange={(event) =>
                            setPlacementCircleCount(
                              Math.max(
                                1,
                                Math.min(
                                  256,
                                  Math.floor(
                                    event.currentTarget
                                      .valueAsNumber ||
                                      1
                                  )
                                )
                              )
                            )
                          }
                        />
                      </label>
                      <label className="placement-toggle">
                        <input
                          type="checkbox"
                          checked={placementCircleTangent}
                          onChange={(event) =>
                            setPlacementCircleTangent(
                              event.currentTarget
                                .checked
                            )
                          }
                        />
                        <span>
                          Orientar tangente
                        </span>
                      </label>
                    </>
                  )}

                  {placementMode ===
                    "lot" && (
                    <label className="placement-field">
                      <span>Recuo m</span>
                      <input
                        type="number"
                        min="-50"
                        max="50"
                        step="0.5"
                        value={placementLotSetback}
                        onChange={(event) => {
                          const value =
                            event.currentTarget
                              .valueAsNumber;

                          if (
                            Number.isFinite(
                              value
                            )
                          ) {
                            setPlacementLotSetback(
                              value
                            );
                          }
                        }}
                      />
                    </label>
                  )}

                  <label className="placement-toggle">
                    <input
                      type="checkbox"
                      checked={
                        placementRandomRotation
                      }
                      onChange={(event) =>
                        setPlacementRandomRotation(
                          event.currentTarget
                            .checked
                        )
                      }
                    />
                    <span>
                      Rotação variada
                    </span>
                  </label>

                  <label className="placement-toggle">
                    <input
                      type="checkbox"
                      checked={
                        placementAlignRoad
                      }
                      onChange={(event) =>
                        setPlacementAlignRoad(
                          event.currentTarget
                            .checked
                        )
                      }
                    />
                    <span>
                      Encaixar/alinha à rua
                    </span>
                  </label>

                  {placementAlignRoad && (
                    <label className="placement-field">
                      <span>Alcance m</span>
                      <input
                        type="number"
                        min="1"
                        max="50"
                        step="1"
                        value={
                          placementRoadSnapDistance
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
                            setPlacementRoadSnapDistance(
                              Math.max(
                                1,
                                value
                              )
                            );
                          }
                        }}
                      />
                    </label>
                  )}

                  {(placementMode ===
                      "line" ||
                    placementMode ===
                      "lot") &&
                    placementLineStart && (
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => {
                        setPlacementLineStart(
                          undefined
                        );
                        setPendingPlacementBatch(
                          []
                        );
                        setSaveNotice(
                          "Linha reiniciada. Clique no novo ponto inicial."
                        );
                      }}
                    >
                      Reiniciar linha
                    </button>
                  )}

                  {pendingPlacementBatch.length >
                    0 && (
                    <span className="placement-batch-count">
                      {pendingPlacementBatch.length}
                      {" "}objeto(s) na operação
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
                    : pendingPlacementBatch.length >
                        1
                      ? `Salvar lote (${pendingPlacementBatch.length})`
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

            {showMapHealthPanel && (
              <div
                className="map-health-panel floating-tool"
                data-floating-tool
              >
                <button
                  type="button"
                  className="tool-drag-grip drag-handle"
                  data-drag-handle
                  title="Mover saúde do mapa"
                >
                  ⋮⋮
                </button>

                <div className="map-health-heading">
                  <div>
                    <strong>
                      Saúde do mapa
                    </strong>
                    <span>
                      Diagnóstico confirmado dos dados carregados
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() =>
                      setShowMapHealthPanel(
                        false
                      )
                    }
                  >
                    ×
                  </button>
                </div>

                <div className="map-health-score">
                  <strong>
                    {mapHealthIssueCount === 0
                      ? "✓ Sem problemas confirmados"
                      : "⚠ " +
                        mapHealthIssueCount +
                        " grupo(s) de problema"}
                  </strong>
                  <span>
                    Assets ainda não carregados não são classificados como defeituosos.
                  </span>
                </div>

                <div className="map-health-grid">
                  <button
                    type="button"
                    className={
                      missingDependencyCount >
                      0
                        ? "warning"
                        : ""
                    }
                    onClick={() => {
                      handleAuditDependencies();
                      setShowMapHealthPanel(
                        false
                      );
                    }}
                  >
                    <strong>
                      {missingDependencyCount}
                    </strong>
                    <span>
                      dependência(s) ausente(s)
                    </span>
                  </button>

                  <button
                    type="button"
                    className={
                      sceneryProblemPaths.size >
                      0
                        ? "warning"
                        : ""
                    }
                    onClick={() => {
                      setSceneryLibraryView(
                        "groups"
                      );
                      setSceneryLibraryGroup(
                        "all"
                      );
                      setScenerySubcategory(
                        "all"
                      );
                      setSceneryTechnicalFilter(
                        "problems"
                      );
                      handleExplorerPanelTab(
                        "library"
                      );
                      setShowMapHealthPanel(
                        false
                      );
                      if (isFullScreen) {
                        setFullScreenPanel(
                          "explorer"
                        );
                      }
                    }}
                  >
                    <strong>
                      {sceneryProblemPaths.size}
                    </strong>
                    <span>
                      .sco com problema confirmado
                    </span>
                  </button>

                  <button
                    type="button"
                    className={
                      splineProblemPaths.size >
                      0
                        ? "warning"
                        : ""
                    }
                    onClick={() => {
                      setSplineLibraryView(
                        "groups"
                      );
                      setSplineLibraryGroup(
                        "all"
                      );
                      setSplineSubcategory(
                        "all"
                      );
                      setSplineTechnicalFilter(
                        "problems"
                      );
                      handleExplorerPanelTab(
                        "splineLibrary"
                      );
                      setShowMapHealthPanel(
                        false
                      );
                      if (isFullScreen) {
                        setFullScreenPanel(
                          "explorer"
                        );
                      }
                    }}
                  >
                    <strong>
                      {splineProblemPaths.size}
                    </strong>
                    <span>
                      .sli com problema confirmado
                    </span>
                  </button>

                  <div>
                    <strong>
                      {failedTextureAssetCount}
                    </strong>
                    <span>
                      textura(s) solicitada(s) com falha
                    </span>
                  </div>

                  <div>
                    <strong>
                      {pendingTextureAssetCount}
                    </strong>
                    <span>
                      textura(s) ainda carregando
                    </span>
                  </div>

                  <div>
                    <strong>
                      {failedMapGeometryCount}
                    </strong>
                    <span>
                      geometria(s) de mapa sem visual renderizável
                    </span>
                  </div>
                </div>
              </div>
            )}

            {showConstructionSetPanel && (
              <div
                className="construction-set-panel floating-tool"
                data-floating-tool
              >
                <button
                  type="button"
                  className="tool-drag-grip drag-handle"
                  data-drag-handle
                  title="Mover conjuntos"
                >
                  ⋮⋮
                </button>

                <div className="construction-set-heading">
                  <div>
                    <strong>
                      Conjuntos de construção
                    </strong>
                    <span>
                      Via real + objetos reais
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() =>
                      setShowConstructionSetPanel(
                        false
                      )
                    }
                  >
                    ×
                  </button>
                </div>

                <div className="construction-set-create">
                  <input
                    value={
                      newConstructionSetName
                    }
                    onChange={(event) =>
                      setNewConstructionSetName(
                        event.currentTarget
                          .value
                      )
                    }
                    placeholder="Nome do novo conjunto"
                    onKeyDown={(event) => {
                      if (
                        event.key ===
                        "Enter"
                      ) {
                        handleCreateConstructionSet();
                      }
                    }}
                  />
                  <button
                    type="button"
                    disabled={
                      !newConstructionSetName
                        .trim()
                    }
                    onClick={
                      handleCreateConstructionSet
                    }
                  >
                    Criar
                  </button>
                </div>

                <select
                  className="construction-set-select"
                  value={
                    activeConstructionSetId
                  }
                  onChange={(event) =>
                    setActiveConstructionSetId(
                      event.currentTarget
                        .value
                    )
                  }
                >
                  <option value="">
                    Selecione um conjunto
                  </option>
                  {constructionSets.map(
                    (set) => (
                      <option
                        key={set.id}
                        value={set.id}
                      >
                        {set.name}
                      </option>
                    )
                  )}
                </select>

                {activeConstructionSet && (
                  <>
                    <label className="construction-set-field">
                      <span>Nome</span>
                      <input
                        value={
                          activeConstructionSet
                            .name
                        }
                        onChange={(event) =>
                          updateActiveConstructionSet(
                            (current) => ({
                              ...current,
                              name:
                                event
                                  .currentTarget
                                  .value
                            })
                          )
                        }
                      />
                    </label>

                    <div className="construction-set-road">
                      <strong>
                        Via-base
                      </strong>
                      <code>
                        {activeConstructionSet
                          .splinePath ??
                          "Nenhuma .sli definida"}
                      </code>
                      <button
                        type="button"
                        disabled={
                          !splineLibraryPreviewAsset
                        }
                        onClick={
                          handleUsePreviewSplineInSet
                        }
                      >
                        Usar spline em prévia
                      </button>
                    </div>

                    <div className="construction-set-add-object">
                      <strong>
                        Companheiros
                      </strong>
                      <button
                        type="button"
                        disabled={
                          !sceneryLibraryPreviewAsset ||
                          activeConstructionSet
                            .companions
                            .length >= 16
                        }
                        onClick={
                          handleAddPreviewObjectToSet
                        }
                      >
                        + Adicionar objeto em prévia
                      </button>
                    </div>

                    <div className="construction-set-companions">
                      {activeConstructionSet
                        .companions.map(
                          (
                            companion,
                            index
                          ) => (
                            <div
                              className="construction-set-companion"
                              key={
                                companion.id
                              }
                            >
                              <div>
                                <strong>
                                  #{index + 1}
                                  {" · "}
                                  {getObjectName(
                                    companion
                                      .sceneryObjectPath
                                  )}
                                </strong>
                                <code>
                                  {companion
                                    .sceneryObjectPath}
                                </code>
                              </div>

                              <label>
                                <span>Lado</span>
                                <select
                                  value={
                                    companion.side
                                  }
                                  onChange={(event) =>
                                    handleUpdateConstructionSetCompanion(
                                      companion.id,
                                      {
                                        side:
                                          event
                                            .currentTarget
                                            .value as ConstructionSetSide
                                      }
                                    )
                                  }
                                >
                                  <option value="left">
                                    Esquerda
                                  </option>
                                  <option value="right">
                                    Direita
                                  </option>
                                  <option value="both">
                                    Ambos
                                  </option>
                                </select>
                              </label>

                              <label>
                                <span>Espaço m</span>
                                <input
                                  type="number"
                                  min="1"
                                  max="200"
                                  step="0.5"
                                  value={
                                    companion.spacing
                                  }
                                  onChange={(event) =>
                                    handleUpdateConstructionSetCompanion(
                                      companion.id,
                                      {
                                        spacing:
                                          Math.max(
                                            1,
                                            event
                                              .currentTarget
                                              .valueAsNumber ||
                                              1
                                          )
                                      }
                                    )
                                  }
                                />
                              </label>

                              <label>
                                <span>Afast. m</span>
                                <input
                                  type="number"
                                  min="0"
                                  max="80"
                                  step="0.5"
                                  value={
                                    companion
                                      .lateralOffset
                                  }
                                  onChange={(event) =>
                                    handleUpdateConstructionSetCompanion(
                                      companion.id,
                                      {
                                        lateralOffset:
                                          Math.max(
                                            0,
                                            event
                                              .currentTarget
                                              .valueAsNumber ||
                                              0
                                          )
                                      }
                                    )
                                  }
                                />
                              </label>

                              <label>
                                <span>Rot. °</span>
                                <input
                                  type="number"
                                  step="1"
                                  value={
                                    companion
                                      .rotationOffset
                                  }
                                  onChange={(event) =>
                                    handleUpdateConstructionSetCompanion(
                                      companion.id,
                                      {
                                        rotationOffset:
                                          event
                                            .currentTarget
                                            .valueAsNumber ||
                                          0
                                      }
                                    )
                                  }
                                />
                              </label>

                              <button
                                type="button"
                                className="danger-action"
                                onClick={() =>
                                  handleRemoveConstructionSetCompanion(
                                    companion.id
                                  )
                                }
                              >
                                Remover
                              </button>
                            </div>
                          )
                        )}

                      {activeConstructionSet
                        .companions.length ===
                        0 && (
                        <span className="construction-set-empty">
                          Adicione árvores, postes ou outros .sco usando o item atualmente em prévia.
                        </span>
                      )}
                    </div>

                    {activeConstructionSetMissingTemplates
                      .length > 0 && (
                      <div className="construction-set-warning">
                        <strong>
                          ⚠ Templates ausentes no mapa
                        </strong>
                        <span>
                          {activeConstructionSetMissingTemplates
                            .slice(0, 3)
                            .join(" · ")}
                        </span>
                      </div>
                    )}

                    <label className="placement-toggle">
                      <input
                        type="checkbox"
                        checked={
                          applyConstructionSet
                        }
                        onChange={(event) =>
                          setApplyConstructionSet(
                            event.currentTarget
                              .checked
                          )
                        }
                      />
                      <span>
                        Aplicar companheiros após salvar a via
                      </span>
                    </label>

                    <div className="construction-set-actions">
                      <button
                        type="button"
                        className="primary-button"
                        disabled={
                          !activeConstructionSet
                            .splinePath ||
                          activeConstructionSetMissingTemplates
                            .length > 0
                        }
                        onClick={
                          handleBuildActiveConstructionSet
                        }
                      >
                        Construir conjunto
                      </button>
                      <button
                        type="button"
                        className="danger-action"
                        onClick={
                          handleDeleteConstructionSet
                        }
                      >
                        Excluir conjunto
                      </button>
                    </div>
                  </>
                )}
              </div>
            )}

            {activeConstructionTool ===
              "junction" && (
              <div
                className="junction-assistant-panel floating-tool"
                data-floating-tool
              >
                <button
                  type="button"
                  className="tool-drag-grip drag-handle"
                  data-drag-handle
                  title="Mover assistente"
                >
                  ⋮⋮
                </button>
                <div className="junction-assistant-heading">
                  <strong>
                    Assistente de cruzamentos
                  </strong>
                  <span>
                    {junctionSuggestions.length}
                    {" "}encontro(s) detectado(s)
                  </span>
                </div>
                <div className="junction-assistant-list">
                  {junctionSuggestions
                    .slice(0, 12)
                    .map(
                      (
                        suggestion,
                        index
                      ) => (
                        <button
                          type="button"
                          key={
                            suggestion.key
                          }
                          className={
                            junctionPlacementTarget &&
                            junctionPlacementTarget
                              .tileX ===
                              suggestion.tileX &&
                            junctionPlacementTarget
                              .tileY ===
                              suggestion.tileY &&
                            Math.abs(
                              junctionPlacementTarget
                                .x -
                                suggestion.x
                            ) < 0.1 &&
                            Math.abs(
                              junctionPlacementTarget
                                .y -
                                suggestion.y
                            ) < 0.1
                              ? "active"
                              : ""
                          }
                          onClick={() =>
                            handleUseJunctionSuggestion(
                              suggestion
                            )
                          }
                        >
                          <strong>
                            #{index + 1}
                            {" · "}
                            Splines {suggestion.splineA}/{suggestion.splineB}
                          </strong>
                          <span>
                            Tile {suggestion.tileX},{suggestion.tileY}
                            {" · "}
                            {formatNumber(
                              suggestion.x
                            )},{" "}
                            {formatNumber(
                              suggestion.y
                            )}
                          </span>
                        </button>
                      )
                    )}
                  {junctionSuggestions.length ===
                    0 && (
                    <span className="junction-assistant-empty">
                      Nenhum cruzamento geométrico detectado nas splines carregadas.
                    </span>
                  )}
                </div>
              </div>
            )}

            {sceneryLibraryReady &&
              splineLibraryReady &&
              missingDependencyCount > 0 && (
              <div
                className="dependency-warning-panel floating-tool"
                data-floating-tool
              >
                <button
                  type="button"
                  className="tool-drag-grip drag-handle"
                  data-drag-handle
                  title="Mover aviso"
                >
                  ⋮⋮
                </button>
                <div>
                  <strong>
                    ⚠ {missingDependencyCount}
                    {" "}dependência(s) ausente(s)
                  </strong>
                  <span>
                    O mapa referencia arquivos que não existem no catálogo atual do OMSI.
                  </span>
                </div>
                <div className="dependency-warning-list">
                  {missingSceneryDependencies
                    .slice(0, 5)
                    .map((path) => (
                      <button
                        type="button"
                        key={"sco:" + path}
                        className={
                          dependencyReplacementTarget
                            ?.kind === "object" &&
                          dependencyReplacementTarget
                            ?.path === path
                            ? "active"
                            : ""
                        }
                        onClick={() =>
                          handleSelectMissingDependency(
                            "object",
                            path
                          )
                        }
                        title="Selecionar para substituir por um .sco real"
                      >
                        <code>
                          SCO · {path}
                        </code>
                        <span>
                          reparar
                        </span>
                      </button>
                    ))}
                  {missingSplineDependencies
                    .slice(
                      0,
                      Math.max(
                        0,
                        8 -
                          Math.min(
                            5,
                            missingSceneryDependencies.length
                          )
                      )
                    )
                    .map((path) => (
                      <button
                        type="button"
                        key={"sli:" + path}
                        className={
                          dependencyReplacementTarget
                            ?.kind === "spline" &&
                          dependencyReplacementTarget
                            ?.path === path
                            ? "active"
                            : ""
                        }
                        onClick={() =>
                          handleSelectMissingDependency(
                            "spline",
                            path
                          )
                        }
                        title="Selecionar para substituir por uma .sli real"
                      >
                        <code>
                          SLI · {path}
                        </code>
                        <span>
                          reparar
                        </span>
                      </button>
                    ))}
                  {missingDependencyCount >
                    8 && (
                    <small>
                      + {missingDependencyCount - 8}
                      {" "}outro(s)
                    </small>
                  )}
                </div>

                {dependencyReplacementTarget && (
                  <div className="dependency-replacement-editor">
                    <strong>
                      Reparar dependência
                    </strong>

                    <div>
                      <span>Ausente</span>
                      <code>
                        {dependencyReplacementTarget.path}
                      </code>
                    </div>

                    <div>
                      <span>
                        Substituto em prévia
                      </span>
                      <code>
                        {dependencyReplacementTarget.kind ===
                        "object"
                          ? sceneryLibraryPreviewAsset
                              ?.sceneryObjectPath ??
                            "Escolha um .sco na biblioteca"
                          : splineLibraryPreviewAsset
                              ?.splinePath ??
                            "Escolha uma .sli na biblioteca"}
                      </code>
                    </div>

                    <div className="dependency-replacement-impact">
                      <span>
                        Referências carregadas:{" "}
                        {dependencyReplacementTarget.kind ===
                        "object"
                          ? objects.filter(
                              (item) =>
                                normalizeAssetClassifierText(
                                  item.sceneryObjectPath
                                ) ===
                                normalizeAssetClassifierText(
                                  dependencyReplacementTarget.path
                                )
                            ).length
                          : splines.filter(
                              (item) =>
                                normalizeAssetClassifierText(
                                  item.splinePath
                                ) ===
                                normalizeAssetClassifierText(
                                  dependencyReplacementTarget.path
                                )
                            ).length}
                      </span>
                      <span>
                        O host verificará todos os tiles, inclusive os não carregados no viewport.
                      </span>
                    </div>

                    <div className="dependency-replacement-actions">
                      <button
                        type="button"
                        className="primary-button"
                        disabled={
                          replacingDependency ||
                          (
                            dependencyReplacementTarget.kind ===
                            "object"
                              ? !sceneryLibraryPreviewAsset
                              : !splineLibraryPreviewAsset
                          )
                        }
                        onClick={
                          handleReplaceMissingDependency
                        }
                      >
                        {replacingDependency
                          ? "Substituindo..."
                          : "Substituir referências"}
                      </button>
                      <button
                        type="button"
                        className="secondary-action"
                        disabled={
                          replacingDependency
                        }
                        onClick={() =>
                          setDependencyReplacementTarget(
                            undefined
                          )
                        }
                      >
                        Cancelar
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}

            <div
              className="viewport-toolbar floating-tool"
              data-floating-tool
            >
              <button
                type="button"
                className="tool-drag-grip drag-handle"
                data-drag-handle
                title="Arraste para mover esta barra"
                aria-label="Mover ferramentas do viewport"
              >
                ⋮⋮
              </button>
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

            <div
              className="viewport-layers floating-tool"
              data-floating-tool
            >
              <button
                type="button"
                className="tool-drag-grip drag-handle"
                data-drag-handle
                title="Arraste para mover este painel"
                aria-label="Mover painel de camadas"
              >
                ⋮⋮
              </button>
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

          </section>

          <aside
            data-floating-tool
            data-floating-tool-id="inspector-drawer"
            className={[
              "object-inspector city-drawer city-drawer-right",
              isFullScreen
                ? "fullscreen-drawer fullscreen-right"
                : "desktop-drawer",
              (
                isFullScreen
                  ? fullScreenPanel ===
                    "inspector"
                  : desktopInspectorOpen
              )
                ? "open"
                : "closed"
            ]
              .filter(Boolean)
              .join(" ")}
          >
            <button
              type="button"
              className="city-drawer-drag-grip"
              data-drag-handle
              title="Arraste para mover o painel"
              aria-label="Mover painel"
            >
              ⋮⋮
            </button>
            {(
              isFullScreen
                ? fullScreenPanel ===
                  "inspector"
                : desktopInspectorOpen
            ) && (
              <button
                type="button"
                className="fullscreen-drawer-close city-drawer-close"
                onClick={() => {
                  if (isFullScreen) {
                    setFullScreenPanel(
                      undefined
                    );
                  } else {
                    setDesktopInspectorOpen(
                      false
                    );
                  }
                }}
                title="Fechar painel"
              >
                ×
              </button>
            )}
            <div className="inspector-heading">
              <strong>Inspetor</strong>
              <span>
                {selectedObject
                  ? "Objeto selecionado"
                  : selectedSpline
                    ? "Spline selecionada"
                    : splineLibraryPlacementAsset
                      ? "Spline em construção"
                      : placementAsset
                        ? "Asset em colocação"
                        : "Mapa aberto"}
              </span>
            </div>

            {selectedSpline ||
            splineLibraryPlacementAsset
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
                    ? `Preparando recursos em segundo plano · O3D ${loadedMapGeometryCount}/${mapObjectPaths.length}`
                    : `Preparando recursos da área · O3D ${loadedNearbyGeometryCount}/${nearbyObjectPaths.length}`
                  : loadingSplineFor ||
                    preloadingSplineProfileFor
                    ? `Preparando recursos em segundo plano · SLI ${loadedSplineProfileCount}/${splinePathsForPreload.length}`
                  : loadingMetadataFor ||
                    loadingGeometryFor
                    ? "Preparando recurso selecionado em segundo plano..."
                    : Object.keys(
                          requestedGroundTextureKeys
                        ).length > 0 ||
                        Object.keys(
                          requestedTerrainMaskKeys
                        ).length > 0
                      ? "Preparando texturas de terreno em segundo plano..."
                      : protectedMeshCount > 0 &&
                        failedDiagnosticGeometryCount > 0
                        ? `Carregamento concluído · ${protectedObjectPathCount} tipos usam O3D protegido (${protectedMeshCount} malhas)`
                        : failedDiagnosticGeometryCount > 0
                          ? `Carregamento concluído com ${failedDiagnosticGeometryCount} tipo(s) sem prévia real`
                          : "Pronto"}
          </span>

          <span>
            Modo:{" "}
            {mapLoadMode === "full"
              ? "Mapa completo"
              : "Streaming automático"}
            <b>·</b>
            Objetos:{" "}
            {selectedStats?.objects ??
              objects.length}
            <b>·</b>
            Splines:{" "}
            {selectedStats?.splines ??
              splines.length}
            <b>·</b>
            Malhas reais:{" "}
            {mapLoadMode === "full"
              ? `${renderableMapGeometryCount}/${mapObjectPaths.length}`
              : `${renderableDiagnosticGeometryCount}/${nearbyObjectPaths.length}`}
            {failedDiagnosticGeometryCount > 0
              ? ` (falhas: ${failedDiagnosticGeometryCount})`
              : ""}
            <b>·</b>
            Perfis SLI:{" "}
            {loadedSplineProfileCount}/
            {splinePathsForPreload.length}
            {" "}· superfícies:{" "}
            {splineProfileDiagnostics.surfaces}
            <b>·</b>
            Texturas:{" "}
            {loadedTextureAssetCount} OK
            {" · "}
            {failedTextureAssetCount} falhas
            {" · "}
            {pendingTextureAssetCount} pendentes
            <b>·</b>
            Auto solicitadas:{" "}
            {Object.keys(
              autoPrefetchedTextureKeys
            ).length}
            {" · limite atual: "}
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
    <main
      className={[
        "studio-shell",
        interactionLocked
          ? "interaction-locked"
          : "",
        isFullScreen &&
        view === "editor"
          ? "is-fullscreen"
          : "",
        sidebarCollapsed
          ? "sidebar-collapsed"
          : ""
      ]
        .filter(Boolean)
        .join(" ")}
      aria-busy={interactionLocked}
    >
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

      {loadingOverlay && (
          <div
            className={
              interactionLocked
                ? "global-loading-lock"
                : "global-loading-lock visual-warmup"
            }
            role="status"
            aria-live="polite"
            aria-label={
              loadingOverlay.title
            }
          >
            <div className="loading-lock-card">
              <div
                className="loading-orbit"
                aria-hidden="true"
              >
                <span />
                <span />
                <span />
              </div>

              <div className="loading-lock-copy">
                <span className="loading-lock-kicker">
                  OMSI MAP STUDIO
                </span>
                <strong>
                  {loadingOverlay.title}
                </strong>
                <p>
                  {loadingOverlay.detail}
                </p>
              </div>

              <div
                className={
                  loadingPercentage ===
                  undefined
                    ? "loading-progress indeterminate"
                    : "loading-progress"
                }
                role={
                  loadingPercentage ===
                  undefined
                    ? undefined
                    : "progressbar"
                }
                aria-valuemin={
                  loadingPercentage ===
                  undefined
                    ? undefined
                    : 0
                }
                aria-valuemax={
                  loadingPercentage ===
                  undefined
                    ? undefined
                    : 100
                }
                aria-valuenow={
                  loadingPercentage
                }
              >
                <span
                  style={
                    loadingPercentage ===
                    undefined
                      ? undefined
                      : {
                          width: `${loadingPercentage}%`
                        }
                  }
                />
              </div>

              <div className="loading-lock-meta">
                <span>
                  {loadingPercentage ===
                  undefined
                    ? "Processando..."
                    : `${loadingPercentage}%`}
                </span>
                <span>
                  {interactionLocked
                    ? "Interações bloqueadas até concluir"
                    : "Recursos reais continuam carregando em segundo plano"}
                </span>
              </div>
            </div>
          </div>
        )}
    </main>
  );
}
