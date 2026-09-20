import { useEffect, useRef, useState } from "react";
import { ArcRotateCamera } from "@babylonjs/core/Cameras/arcRotateCamera";
import { Engine } from "@babylonjs/core/Engines/engine";
import { GizmoManager } from "@babylonjs/core/Gizmos/gizmoManager";
import { HemisphericLight } from "@babylonjs/core/Lights/hemisphericLight";
import { Material } from "@babylonjs/core/Materials/material";
import { StandardMaterial } from "@babylonjs/core/Materials/standardMaterial";
import { Texture } from "@babylonjs/core/Materials/Textures/texture";
import { RawTexture } from "@babylonjs/core/Materials/Textures/rawTexture";
import "@babylonjs/core/Materials/Textures/Loaders/ddsTextureLoader";
import "@babylonjs/core/Materials/Textures/Loaders/tgaTextureLoader";
import { Color3, Matrix, Quaternion, Vector3 } from "@babylonjs/core/Maths/math";
import { Mesh } from "@babylonjs/core/Meshes/mesh";
import { MeshBuilder } from "@babylonjs/core/Meshes/meshBuilder";
import { VertexData } from "@babylonjs/core/Meshes/mesh.vertexData";
import { TransformNode } from "@babylonjs/core/Meshes/transformNode";
import { Scene } from "@babylonjs/core/scene";
import type { Node } from "@babylonjs/core/node";
import type {
  OmsiPlacedObject,
  OmsiPlacedSpline,
  OmsiSplineDefinition,
  OmsiSceneryMaterialOverride,
  OmsiSceneryObjectGeometry,
  OmsiTextureAsset,
  OmsiTile
} from "../bridge/desktopBridge";
import {
  getSceneryTextureAssetKey,
  getSplineTextureAssetKey,
  sceneryTreeTextureMeshToken
} from "../bridge/desktopBridge";

type ViewportDiagnostic = {
  item: string;
  mesh: string;
  material: string;
  texture: string;
  uv: string;
  position: string;
  renderLift: string;
  origin: string;
};

type ViewportProps = {
  tiles: OmsiTile[];
  cameraStateKey: string;
  editorTool: "select" | "move" | "rotate";
  selectionMode:
    | "all"
    | "object"
    | "spline"
    | "terrain";
  snapEnabled: boolean;
  moveSnap: number;
  rotationSnap: number;
  showGrid: boolean;
  showTerrain: boolean;
  terrainMainTextureAsset?: OmsiTextureAsset;
  terrainMainTextureRepeating?: number;
  terrainOverlays: Array<{
    tileX: number;
    tileY: number;
    layerIndex: number;
    textureRepeating: number;
    maskIsFull: boolean;
    textureAsset?: OmsiTextureAsset;
    maskAsset?: OmsiTextureAsset;
  }>;
  showObjects: boolean;
  showSplines: boolean;
  showSplineProfiles: boolean;
  showAllSplineProfiles: boolean;
  nightPreviewEnabled: boolean;
  skyTextureAsset?: OmsiTextureAsset;
  cameraAction?: {
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
  placementAssetPath?: string;
  placementGeometry?: OmsiSceneryObjectGeometry;
  pendingPlacement?: {
    tileX: number;
    tileY: number;
    x: number;
    y: number;
    z: number;
    rotation: number;
    pitch: number;
    bank: number;
  };
  pendingPlacementBatch?: Array<{
    tileX: number;
    tileY: number;
    x: number;
    y: number;
    z: number;
    rotation: number;
    pitch: number;
    bank: number;
  }>;
  onPlacementPoint?: (
    placement: {
      tileX: number;
      tileY: number;
      x: number;
      y: number;
      z: number;
      rotation: number;
      pitch: number;
      bank: number;
    }
  ) => void;
  onLibraryAssetDrop?: (
    payload: {
      kind: "object" | "spline";
      assetPath: string;
      point: {
        tileX: number;
        tileY: number;
        x: number;
        y: number;
      };
    }
  ) => void;
  onThumbnailReady?: (
    dataUrl: string
  ) => void;
  splinePlacementTemplate?: OmsiPlacedSpline;
  splinePlacementProfile?: OmsiSplineDefinition;
  pendingSplinePlacement?: {
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
  onSplinePlacementPoint?: (
    point: {
      targetTileX: number;
      targetTileY: number;
      x: number;
      y: number;
    }
  ) => void;
  roadDragMode?: boolean;
  roadCurveControl?: {
    start: {
      targetTileX: number;
      targetTileY: number;
      x: number;
      y: number;
    };
    end: {
      targetTileX: number;
      targetTileY: number;
      x: number;
      y: number;
    };
    offset: number;
  };
  onRoadCurveOffsetChange?: (
    offset: number
  ) => void;
  onRoadControlPointChange?: (
    control: "start" | "end",
    point: {
      targetTileX: number;
      targetTileY: number;
      x: number;
      y: number;
    }
  ) => void;
  objects: OmsiPlacedObject[];
  splines: OmsiPlacedSpline[];
  activeTile?: {
    x: number;
    y: number;
  };
  onActiveTileChange?: (
    tile: {
      x: number;
      y: number;
    }
  ) => void;
  onTerrainPoint?: (
    point: {
      tileX: number;
      tileY: number;
      x: number;
      y: number;
      height: number;
    }
  ) => void;
  referenceOverlay?: {
    base64Data: string;
    mimeType: string;
    width: number;
    height: number;
    metersPerPixel: number;
    anchorWorldX: number;
    anchorWorldZ: number;
    opacity: number;
    attribution: string;
  };
  usesWorldCoordinates: boolean;
  selectedObject?: OmsiPlacedObject;
  selectedGeometry?: OmsiSceneryObjectGeometry;
  objectGeometryByPath: Record<
    string,
    OmsiSceneryObjectGeometry
  >;
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >;
  splineProfilesByPath: Record<
    string,
    OmsiSplineDefinition
  >;
  selectedSpline?: OmsiPlacedSpline;
  selectedSplineProfile?: OmsiSplineDefinition;
  onSelectObject: (placedObject: OmsiPlacedObject | undefined) => void;
  onSelectSpline: (placedSpline: OmsiPlacedSpline | undefined) => void;
  onPreviewObjectTransform: (
    placedObject: OmsiPlacedObject
  ) => void;
  onPreviewSplineTransform: (
    placedSpline: OmsiPlacedSpline
  ) => void;
};

function hasRenderableTerrain(
  tile: OmsiTile,
  tileSize: number
) {
  const terrain = tile.terrain;

  if (
    tileSize !== 300 ||
    !terrain ||
    terrain.cellCount <= 0
  ) {
    return false;
  }

  const sampleCount =
    terrain.cellCount + 1;

  return (
    terrain.heights.length ===
      sampleCount * sampleCount &&
    terrain.heights.every(
      Number.isFinite
    )
  );
}

function createMeshFromVertexData(
  scene: Scene,
  name: string,
  positions: number[],
  indices: number[],
  diffuseColor: Color3,
  alpha = 1,
  uvs?: number[],
  textureAsset?: OmsiTextureAsset,
  textureRepeating?: number
) {
  if (
    positions.length === 0 ||
    indices.length === 0
  ) {
    return;
  }

  const mesh =
    new Mesh(
      name,
      scene
    );

  const vertexData =
    new VertexData();

  vertexData.positions =
    positions;

  vertexData.indices =
    indices;

  if (
    uvs &&
    uvs.length ===
      (positions.length / 3) * 2
  ) {
    vertexData.uvs = uvs;
  }

  VertexData.ComputeNormals(
    positions,
    indices,
    (vertexData.normals = [])
  );

  vertexData.applyToMesh(
    mesh,
    false
  );

  const material =
    new StandardMaterial(
      `${name}-material`,
      scene
    );

  material.diffuseColor =
    diffuseColor;

  material.specularColor =
    Color3.Black();

  material.alpha = alpha;

  const texture =
    createTextureFromAsset(
      scene,
      textureAsset
    );

  if (texture) {
    const repeating =
      typeof textureRepeating ===
        "number" &&
      Number.isFinite(
        textureRepeating
      ) &&
      textureRepeating > 0
        ? textureRepeating
        : 1;

    texture.uScale = repeating;
    texture.vScale = repeating;
    texture.wrapU =
      Texture.WRAP_ADDRESSMODE;
    texture.wrapV =
      Texture.WRAP_ADDRESSMODE;

    // Ground layer 0 is opaque. Browser-decodable formats such as
    // BMP/PNG/JPEG must not be forced through Babylon's plugin loader path;
    // createTextureFromAsset only forces DDS/TGA now. Keep the terrain
    // preview albedo-oriented so an arbitrary editor light cannot darken the
    // real OMSI texture into an almost black surface.
    texture.hasAlpha = false;

    material.diffuseColor =
      Color3.Black();

    material.diffuseTexture =
      texture;

    material.emissiveColor =
      Color3.White();

    material.emissiveTexture =
      texture;

    material.useAlphaFromDiffuseTexture =
      false;

    material.transparencyMode =
      Material.MATERIAL_OPAQUE;

    material.backFaceCulling = false;
    material.disableLighting = true;
  }

  mesh.material = material;
  mesh.isPickable = false;
  return mesh;
}

function createTileSurface(
  scene: Scene,
  tiles: OmsiTile[],
  tileSize: number,
  showFlatSurface: boolean,
  showTerrain: boolean,
  terrainMainTextureAsset:
    | OmsiTextureAsset
    | undefined,
  terrainMainTextureRepeating:
    | number
    | undefined,
  terrainOverlays: Array<{
    tileX: number;
    tileY: number;
    layerIndex: number;
    textureRepeating: number;
    maskIsFull: boolean;
    textureAsset?: OmsiTextureAsset;
    maskAsset?: OmsiTextureAsset;
  }>
) {
  if (tiles.length === 0) {
    return;
  }

  const terrainPositions:
    number[] = [];
  const terrainIndices:
    number[] = [];

  const terrainUvs:
    number[] = [];

  const flatPositions:
    number[] = [];
  const flatIndices:
    number[] = [];

  for (const tile of tiles) {
    if (
      tile.detailsLoaded &&
      !tile.fileExists
    ) {
      continue;
    }

    if (
      showTerrain &&
      hasRenderableTerrain(
        tile,
        tileSize
      )
    ) {
      const terrain =
        tile.terrain!;

      const sampleCount =
        terrain.cellCount + 1;

      const spacing =
        tileSize /
        terrain.cellCount;

      const vertexOffset =
        terrainPositions.length / 3;

      for (
        let row = 0;
        row < sampleCount;
        row += 1
      ) {
        for (
          let column = 0;
          column < sampleCount;
          column += 1
        ) {
          terrainPositions.push(
            tile.x * tileSize +
              column * spacing,
            terrain.heights[
              row * sampleCount +
                column
            ],
            tile.y * tileSize +
              row * spacing
          );

          terrainUvs.push(
            column /
              terrain.cellCount,
            row /
              terrain.cellCount
          );
        }
      }

      for (
        let row = 0;
        row < terrain.cellCount;
        row += 1
      ) {
        for (
          let column = 0;
          column < terrain.cellCount;
          column += 1
        ) {
          const topLeft =
            vertexOffset +
            row * sampleCount +
            column;

          const topRight =
            topLeft + 1;

          const bottomLeft =
            topLeft + sampleCount;

          const bottomRight =
            bottomLeft + 1;

          terrainIndices.push(
            topLeft,
            bottomRight,
            topRight,
            topLeft,
            bottomLeft,
            bottomRight
          );
        }
      }

      continue;
    }

    if (!showFlatSurface) {
      continue;
    }

    const x0 =
      tile.x * tileSize;
    const z0 =
      tile.y * tileSize;
    const x1 =
      x0 + tileSize;
    const z1 =
      z0 + tileSize;

    const vertexOffset =
      flatPositions.length / 3;

    flatPositions.push(
      x0, -0.04, z0,
      x1, -0.04, z0,
      x1, -0.04, z1,
      x0, -0.04, z1
    );

    flatIndices.push(
      vertexOffset,
      vertexOffset + 2,
      vertexOffset + 1,
      vertexOffset,
      vertexOffset + 3,
      vertexOffset + 2
    );
  }

  const terrainMesh =
    createMeshFromVertexData(
      scene,
      "omsi-editor-terrain",
      terrainPositions,
      terrainIndices,
      new Color3(
        0.16,
        0.24,
        0.12
      ),
      1,
      terrainUvs,
      terrainMainTextureAsset,
      terrainMainTextureRepeating
    );

  if (terrainMesh) {
    terrainMesh.isPickable = true;
    terrainMesh.metadata = {
      ...(terrainMesh.metadata ?? {}),
      mapStudioKind: "terrain"
    };
  }

  for (const overlay of
    terrainOverlays) {
    if (
      !overlay.textureAsset
        ?.exists ||
      (!overlay.maskIsFull &&
        (!overlay.maskAsset?.exists ||
          overlay.maskAsset
            .alphaOnly !== true ||
          !overlay.maskAsset.width ||
          !overlay.maskAsset.height))
    ) {
      continue;
    }

    const tile =
      tiles.find(
        (candidate) =>
          candidate.x ===
            overlay.tileX &&
          candidate.y ===
            overlay.tileY
      );

    if (
      !tile ||
      !hasRenderableTerrain(
        tile,
        tileSize
      )
    ) {
      continue;
    }

    const terrain =
      tile.terrain!;

    const sampleCount =
      terrain.cellCount + 1;

    const spacing =
      tileSize /
      terrain.cellCount;

    const positions:
      number[] = [];

    const indices:
      number[] = [];

    const uvs:
      number[] = [];

    const heightOffset =
      0.01 +
      Math.min(
        20,
        overlay.layerIndex
      ) *
        0.0005;

    for (
      let row = 0;
      row < sampleCount;
      row += 1
    ) {
      for (
        let column = 0;
        column < sampleCount;
        column += 1
      ) {
        positions.push(
          tile.x * tileSize +
            column * spacing,
          terrain.heights[
            row * sampleCount +
              column
          ] +
            heightOffset,
          tile.y * tileSize +
            row * spacing
        );

        uvs.push(
          column /
            terrain.cellCount,
          row /
            terrain.cellCount
        );
      }
    }

    for (
      let row = 0;
      row < terrain.cellCount;
      row += 1
    ) {
      for (
        let column = 0;
        column < terrain.cellCount;
        column += 1
      ) {
        const topLeft =
          row * sampleCount +
          column;

        const topRight =
          topLeft + 1;

        const bottomLeft =
          topLeft + sampleCount;

        const bottomRight =
          bottomLeft + 1;

        indices.push(
          topLeft,
          bottomRight,
          topRight,
          topLeft,
          bottomLeft,
          bottomRight
        );
      }
    }

    const mesh =
      new Mesh(
        `omsi-terrain-layer-${overlay.layerIndex}-${tile.x}-${tile.y}`,
        scene
      );

    const vertexData =
      new VertexData();

    vertexData.positions =
      positions;
    vertexData.indices =
      indices;
    vertexData.uvs = uvs;

    VertexData.ComputeNormals(
      positions,
      indices,
      (vertexData.normals = [])
    );

    vertexData.applyToMesh(
      mesh,
      false
    );

    const material =
      new StandardMaterial(
        `omsi-terrain-layer-material-${overlay.layerIndex}-${tile.x}-${tile.y}`,
        scene
      );

    material.diffuseColor =
      Color3.White();

    material.specularColor =
      Color3.Black();

    const mainTexture =
      createTextureFromAsset(
        scene,
        overlay.textureAsset
      );

    const maskTexture =
      overlay.maskIsFull
        ? undefined
        : createTextureFromAsset(
            scene,
            overlay.maskAsset
          );

    if (
      !mainTexture ||
      (!overlay.maskIsFull &&
        !maskTexture)
    ) {
      mesh.dispose();
      continue;
    }

    mainTexture.hasAlpha = false;
    mainTexture.uScale =
      overlay.textureRepeating;
    mainTexture.vScale =
      overlay.textureRepeating;
    mainTexture.wrapU =
      Texture.WRAP_ADDRESSMODE;
    mainTexture.wrapV =
      Texture.WRAP_ADDRESSMODE;

    if (maskTexture) {
      maskTexture.hasAlpha = true;
      maskTexture.uScale = 1;
      maskTexture.vScale = 1;
      maskTexture.wrapU =
        Texture.CLAMP_ADDRESSMODE;
      maskTexture.wrapV =
        Texture.CLAMP_ADDRESSMODE;
    }

    material.diffuseColor =
      Color3.Black();

    material.diffuseTexture =
      mainTexture;

    material.emissiveColor =
      Color3.White();

    material.emissiveTexture =
      mainTexture;

    // Terrain paint should display the source albedo and validated A8 mask,
    // not an approximation produced by the editor light.
    material.backFaceCulling = false;
    material.disableLighting = true;

    if (maskTexture) {
      material.opacityTexture =
        maskTexture;
      material.transparencyMode =
        Material.MATERIAL_ALPHABLEND;
      material.disableDepthWrite =
        true;
    }

    mesh.material = material;
    mesh.isPickable = false;
  }

  const flatSurface =
    createMeshFromVertexData(
      scene,
      "omsi-editor-tile-surface",
      flatPositions,
      flatIndices,
      new Color3(
        0.045,
        0.095,
        0.14
      ),
      0.92
    );

  if (flatSurface) {
    flatSurface.isPickable = true;
    flatSurface.metadata = {
      ...(flatSurface.metadata ?? {}),
      mapStudioKind: "terrain"
    };
  }
}

function createReferenceOverlay(
  scene: Scene,
  tiles: OmsiTile[],
  overlay: NonNullable<
    ViewportProps["referenceOverlay"]
  >
) {
  if (
    !overlay.base64Data ||
    overlay.width <= 0 ||
    overlay.height <= 0 ||
    overlay.metersPerPixel <= 0
  ) {
    return;
  }

  const widthMeters =
    overlay.width *
    overlay.metersPerPixel;

  const heightMeters =
    overlay.height *
    overlay.metersPerPixel;

  const segments = 24;
  const sampleCount =
    segments + 1;

  const positions: number[] = [];
  const indices: number[] = [];
  const uvs: number[] = [];

  for (
    let row = 0;
    row < sampleCount;
    row += 1
  ) {
    const v =
      row / segments;

    for (
      let column = 0;
      column < sampleCount;
      column += 1
    ) {
      const u =
        column / segments;

      const worldX =
        overlay.anchorWorldX +
        (u - 0.5) *
          widthMeters;

      const worldZ =
        overlay.anchorWorldZ +
        (v - 0.5) *
          heightMeters;

      const height =
        getTerrainHeightAtWorldPoint(
          tiles,
          worldX,
          worldZ
        );

      positions.push(
        worldX,
        height + 0.08,
        worldZ
      );

      uvs.push(
        u,
        1 - v
      );
    }
  }

  for (
    let row = 0;
    row < segments;
    row += 1
  ) {
    for (
      let column = 0;
      column < segments;
      column += 1
    ) {
      const topLeft =
        row *
        sampleCount +
        column;

      const topRight =
        topLeft + 1;

      const bottomLeft =
        topLeft +
        sampleCount;

      const bottomRight =
        bottomLeft + 1;

      indices.push(
        topLeft,
        bottomRight,
        topRight,
        topLeft,
        bottomLeft,
        bottomRight
      );
    }
  }

  const mesh =
    new Mesh(
      "mapstudio-reference-overlay",
      scene
    );

  const data =
    new VertexData();

  data.positions = positions;
  data.indices = indices;
  data.uvs = uvs;

  VertexData.ComputeNormals(
    positions,
    indices,
    (data.normals = [])
  );

  data.applyToMesh(
    mesh,
    false
  );

  const texture =
    new Texture(
      `data:${overlay.mimeType};base64,${overlay.base64Data}`,
      scene,
      false,
      false,
      Texture.BILINEAR_SAMPLINGMODE
    );

  texture.hasAlpha = true;
  texture.wrapU =
    Texture.CLAMP_ADDRESSMODE;
  texture.wrapV =
    Texture.CLAMP_ADDRESSMODE;

  const material =
    new StandardMaterial(
      "mapstudio-reference-overlay-material",
      scene
    );

  material.diffuseColor =
    Color3.White();
  material.diffuseTexture =
    texture;
  material.emissiveColor =
    Color3.White();
  material.emissiveTexture =
    texture;
  material.specularColor =
    Color3.Black();
  material.alpha =
    Math.min(
      1,
      Math.max(
        0.05,
        overlay.opacity
      )
    );
  material.backFaceCulling = false;
  material.disableLighting = true;
  material.transparencyMode =
    Material.MATERIAL_ALPHABLEND;
  material.disableDepthWrite = true;
  material.zOffset = -3;

  mesh.material = material;
  mesh.isPickable = false;
}

function createActiveTileOutline(
  activeTile: {
    x: number;
    y: number;
  },
  tileSize: number
) {
  return createTileOutline(
    {
      x: activeTile.x,
      y: activeTile.y
    },
    tileSize
  );
}

function createTileOutline(
  tile: Pick<OmsiTile, "x" | "y">,
  tileSize: number
) {
  const x0 = tile.x * tileSize;
  const z0 = tile.y * tileSize;
  const x1 = x0 + tileSize;
  const z1 = z0 + tileSize;

  return [
    new Vector3(x0, 0, z0),
    new Vector3(x1, 0, z0),
    new Vector3(x1, 0, z1),
    new Vector3(x0, 0, z1),
    new Vector3(x0, 0, z0)
  ];
}

function getTerrainHeightAtLocalPoint(
  tile: OmsiTile,
  localX: number,
  localY: number
) {
  if (
    !hasRenderableTerrain(
      tile,
      300
    )
  ) {
    return 0;
  }

  const terrain = tile.terrain!;
  const cellCount = terrain.cellCount;
  const sampleCount =
    cellCount + 1;

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

  const column0 =
    Math.floor(gridX);
  const row0 =
    Math.floor(gridY);
  const column1 =
    Math.min(
      cellCount,
      column0 + 1
    );
  const row1 =
    Math.min(
      cellCount,
      row0 + 1
    );

  const fractionX =
    gridX - column0;
  const fractionY =
    gridY - row0;

  const height00 =
    terrain.heights[
      row0 * sampleCount +
        column0
    ];
  const height10 =
    terrain.heights[
      row0 * sampleCount +
        column1
    ];
  const height01 =
    terrain.heights[
      row1 * sampleCount +
        column0
    ];
  const height11 =
    terrain.heights[
      row1 * sampleCount +
        column1
    ];

  const top =
    height00 +
    (height10 - height00) *
      fractionX;

  const bottom =
    height01 +
    (height11 - height01) *
      fractionX;

  return (
    top +
    (bottom - top) *
      fractionY
  );
}

function getTerrainHeightAtWorldPoint(
  tiles: OmsiTile[],
  worldX: number,
  worldZ: number
) {
  const tileX =
    Math.floor(
      worldX / 300
    );
  const tileY =
    Math.floor(
      worldZ / 300
    );

  const tile =
    tiles.find(
      (candidate) =>
        candidate.x === tileX &&
        candidate.y === tileY
    );

  if (!tile) {
    return 0;
  }

  return getTerrainHeightAtLocalPoint(
    tile,
    worldX - tileX * 300,
    worldZ - tileY * 300
  );
}

function getTerrainHeightAtObject(
  placedObject: OmsiPlacedObject,
  tiles: OmsiTile[]
) {
  const tile =
    tiles.find(
      (candidate) =>
        candidate.x === placedObject.tileX &&
        candidate.y === placedObject.tileY
    );

  if (
    !tile ||
    !hasRenderableTerrain(
      tile,
      300
    )
  ) {
    return 0;
  }

  const terrain = tile.terrain!;
  const cellCount = terrain.cellCount;
  const sampleCount = cellCount + 1;

  const gridX =
    Math.min(
      cellCount,
      Math.max(
        0,
        (placedObject.x / 300) *
          cellCount
      )
    );

  const gridY =
    Math.min(
      cellCount,
      Math.max(
        0,
        (placedObject.y / 300) *
          cellCount
      )
    );

  const column0 = Math.floor(gridX);
  const row0 = Math.floor(gridY);
  const column1 =
    Math.min(
      cellCount,
      column0 + 1
    );
  const row1 =
    Math.min(
      cellCount,
      row0 + 1
    );

  const fractionX =
    gridX - column0;
  const fractionY =
    gridY - row0;

  const height00 =
    terrain.heights[
      row0 * sampleCount +
        column0
    ];
  const height10 =
    terrain.heights[
      row0 * sampleCount +
        column1
    ];
  const height01 =
    terrain.heights[
      row1 * sampleCount +
        column0
    ];
  const height11 =
    terrain.heights[
      row1 * sampleCount +
        column1
    ];

  const top =
    height00 +
    (height10 - height00) *
      fractionX;

  const bottom =
    height01 +
    (height11 - height01) *
      fractionX;

  return (
    top +
    (bottom - top) *
      fractionY
  );
}

function getObjectTerrainOffset(
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined,
  tiles: OmsiTile[]
) {
  if (geometry?.usesAbsoluteHeight) {
    return 0;
  }

  return getTerrainHeightAtObject(
    placedObject,
    tiles
  );
}

function getObjectWorldPosition(
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined,
  tiles: OmsiTile[]
) {
  return new Vector3(
    placedObject.tileX * 300 + placedObject.x,
    placedObject.z +
      getObjectTerrainOffset(
        placedObject,
        geometry,
        tiles
      ),
    placedObject.tileY * 300 + placedObject.y
  );
}

function createObjectMarkerLines(
  objects: OmsiPlacedObject[],
  geometryByPath: Record<
    string,
    OmsiSceneryObjectGeometry
  >,
  tiles: OmsiTile[]
) {
  const markerRadius = 1.5;
  const markerHeight = 3;

  return objects.flatMap((placedObject) => {
    const position =
      getObjectWorldPosition(
        placedObject,
        geometryByPath[
          placedObject.sceneryObjectPath
        ],
        tiles
      );

    return [
      [
        new Vector3(position.x - markerRadius, position.y, position.z),
        new Vector3(position.x + markerRadius, position.y, position.z)
      ],
      [
        new Vector3(position.x, position.y, position.z - markerRadius),
        new Vector3(position.x, position.y, position.z + markerRadius)
      ],
      [
        position,
        new Vector3(position.x, position.y + markerHeight, position.z)
      ]
    ];
  });
}


function getSplineFrame(
  placedSpline: OmsiPlacedSpline,
  distance: number
) {
  const length =
    Math.max(0, placedSpline.length);

  const clampedDistance =
    Math.min(
      length,
      Math.max(0, distance)
    );

  const yaw =
    placedSpline.rotation *
    degreesToRadians;

  const hasCurve =
    Math.abs(placedSpline.radius) >
    0.001;

  const angle =
    hasCurve
      ? clampedDistance /
        placedSpline.radius
      : 0;

  const localX =
    hasCurve
      ? placedSpline.radius *
        (1 - Math.cos(angle))
      : 0;

  const localZ =
    hasCurve
      ? placedSpline.radius *
        Math.sin(angle)
      : clampedDistance;

  const cosYaw = Math.cos(yaw);
  const sinYaw = Math.sin(yaw);

  const worldOffsetX =
    localX * cosYaw +
    localZ * sinYaw;

  const worldOffsetZ =
    -localX * sinYaw +
    localZ * cosYaw;

  const gradientStart =
    placedSpline.gradientStart / 100;

  const gradientEnd =
    placedSpline.gradientEnd / 100;

  const gradientDelta =
    gradientEnd -
    gradientStart;

  const heightOffset =
    clampedDistance *
      gradientStart +
    (length > 0
      ? 0.5 *
        clampedDistance *
        clampedDistance /
        length *
        gradientDelta
      : 0);

  const heading =
    yaw + angle;

  return {
    center: new Vector3(
      placedSpline.tileX * 300 +
        placedSpline.x +
        worldOffsetX,
      placedSpline.z +
        heightOffset,
      placedSpline.tileY * 300 +
        placedSpline.y +
        worldOffsetZ
    ),
    widthAxis: new Vector3(
      Math.cos(heading),
      0,
      -Math.sin(heading)
    )
  };
}

function getSplineAxisLine(
  placedSpline: OmsiPlacedSpline
) {
  const length =
    Math.max(0, placedSpline.length);

  if (length < 0.01) {
    return [];
  }

  const segmentCount =
    Math.min(
      96,
      Math.max(
        2,
        Math.ceil(length / 10)
      )
    );

  const points: Vector3[] = [];

  for (
    let index = 0;
    index <= segmentCount;
    index += 1
  ) {
    const distance =
      length *
      (index / segmentCount);

    const frame =
      getSplineFrame(
        placedSpline,
        distance
      );

    points.push(
      frame.center.add(
        new Vector3(0, 0.16, 0)
      )
    );
  }

  return points;
}

function isSameObject(
  left: OmsiPlacedObject | undefined,
  right: OmsiPlacedObject | undefined
) {
  return Boolean(
    left &&
    right &&
    left.tileX === right.tileX &&
    left.tileY === right.tileY &&
    left.objectId === right.objectId &&
    left.sourceSectionOrdinal ===
      right.sourceSectionOrdinal &&
    left.sceneryObjectPath ===
      right.sceneryObjectPath
  );
}

function isSameSpline(
  left: OmsiPlacedSpline | undefined,
  right: OmsiPlacedSpline | undefined
) {
  return Boolean(
    left &&
    right &&
    left.tileX === right.tileX &&
    left.tileY === right.tileY &&
    left.splineId === right.splineId &&
    left.splinePath === right.splinePath
  );
}

function createSelectedSplineProfile(
  scene: Scene,
  placedSpline: OmsiPlacedSpline,
  definition: OmsiSplineDefinition,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >,
  parent?: TransformNode,
  namePrefix =
    "selected-spline-profile"
) {
  const length =
    Math.max(0, placedSpline.length);

  if (
    length < 0.01 ||
    !definition.exists ||
    definition.surfaces.length === 0
  ) {
    return;
  }

  const segmentCount =
    Math.min(
      128,
      Math.max(
        4,
        Math.ceil(length / 5)
      )
    );

  for (
    let surfaceIndex = 0;
    surfaceIndex <
      definition.surfaces.length;
    surfaceIndex += 1
  ) {
    const surface =
      definition.surfaces[
        surfaceIndex
      ];

    const positions: number[] = [];
    const uvs: number[] = [];
    const indices: number[] = [];

    for (
      let segmentIndex = 0;
      segmentIndex <= segmentCount;
      segmentIndex += 1
    ) {
      const distance =
        length *
        (segmentIndex /
          segmentCount);

      const frame =
        getSplineFrame(
          placedSpline,
          distance
        );

      for (const point of [
        surface.from,
        surface.to
      ]) {
        const world =
          frame.center
            .add(
              frame.widthAxis.scale(
                point.x
              )
            )
            .add(
              new Vector3(
                0,
                point.z + 0.12,
                0
              )
            );

        positions.push(
          world.x,
          world.y,
          world.z
        );

        uvs.push(
          point.textureX,
          distance *
            point.textureScale
        );
      }

      if (segmentIndex > 0) {
        const current =
          segmentIndex * 2;

        const previous =
          current - 2;

        indices.push(
          previous,
          current,
          previous + 1,
          previous + 1,
          current,
          current + 1
        );
      }
    }

    const mesh = new Mesh(
      `${namePrefix}-${surfaceIndex}`,
      scene
    );

    const vertexData =
      new VertexData();

    vertexData.positions =
      positions;

    vertexData.uvs =
      uvs;

    vertexData.indices =
      indices;

    const normals: number[] = [];

    VertexData.ComputeNormals(
      positions,
      indices,
      normals
    );

    vertexData.normals =
      normals;

    vertexData.applyToMesh(
      mesh,
      false
    );

    const material =
      new StandardMaterial(
        `${namePrefix}-material-${surfaceIndex}`,
        scene
      );

    material.diffuseColor =
      new Color3(
        0.28,
        0.34,
        0.4
      );

    material.emissiveColor =
      new Color3(
        0.035,
        0.08,
        0.11
      );

    material.specularColor =
      new Color3(
        0.06,
        0.06,
        0.06
      );

    material.backFaceCulling =
      false;

    material.twoSidedLighting =
      true;

    material.disableLighting = true;

    // Keep road/profile geometry in front of the terrain depth plane
    // without changing the actual OMSI spline coordinates.
    material.zOffset = -2;

    const resolvedTextureAsset =
      surface.textureName
        ? textureAssetsByKey[
            getSplineTextureAssetKey(
              placedSpline.splinePath,
              surface.textureName
            )
          ]
        : undefined;

    if (surface.textureName) {
      const texture =
        createTextureFromAsset(
          scene,
          resolvedTextureAsset
        );

      if (texture) {
        // Render the real SLI albedo directly. Using a black diffuse
        // multiplier plus an emissive copy made valid road textures
        // depend on Babylon's emissive path and could leave the whole
        // profile black even though the asset had loaded correctly.
        material.diffuseColor =
          Color3.White();

        material.diffuseTexture =
          texture;

        // Keep the real OMSI road texture visible even with editor
        // lighting disabled. The same source asset and UVs are used;
        // no synthetic material or colour is introduced.
        material.emissiveColor =
          Color3.White();

        material.emissiveTexture =
          texture;

        texture.wrapU =
          Texture.WRAP_ADDRESSMODE;
        texture.wrapV =
          Texture.WRAP_ADDRESSMODE;

        if (surface.alphaMode === 1) {
          texture.hasAlpha = true;
          material.useAlphaFromDiffuseTexture =
            true;
          material.transparencyMode =
            Material.MATERIAL_ALPHATEST;
          material.alphaCutOff = 0.4;
        } else if (
          surface.alphaMode === 2
        ) {
          texture.hasAlpha = true;
          material.useAlphaFromDiffuseTexture =
            true;
          material.transparencyMode =
            Material.MATERIAL_ALPHABLEND;
        } else {
          material.useAlphaFromDiffuseTexture =
            false;
          material.transparencyMode =
            Material.MATERIAL_OPAQUE;
        }
      }
    }

    mesh.material = material;
    mesh.isPickable = true;
    mesh.metadata = {
      ...(mesh.metadata ?? {}),
      mapStudioKind: "spline",
      placedSpline,
      mapStudioSource: "SLI",
      mapStudioOrigin:
        placedSpline.splinePath,
      mapStudioTextureName:
        surface.textureName ?? null,
      mapStudioResolvedTexture:
        (() => {
          const asset = resolvedTextureAsset;

          if (!asset) {
            return "asset pendente/ausente";
          }

          if (!asset.exists) {
            return asset.errorCode ??
              "asset ausente";
          }

          const dimensions =
            asset.width &&
            asset.height
              ? `${asset.width}x${asset.height}`
              : "dimensões n/d";

          return [
            asset.resolvedPath ??
              "caminho não exposto",
            dimensions,
            asset.pixelFormat ??
              asset.sourceExtension ??
              asset.extension ??
              "formato n/d"
          ].join(" · ");
        })(),
      mapStudioRenderLift: 0.12
    };

    if (parent) {
      mesh.parent = parent;
    }
  }
}

const maxMapSplineProfiles = 500;
const mapSplineProfileTileRadius = 1;

function createMapSplineProfiles(
  scene: Scene,
  splines: OmsiPlacedSpline[],
  activeTile:
    | { x: number; y: number }
    | undefined,
  selectedSpline:
    | OmsiPlacedSpline
    | undefined,
  splineProfilesByPath: Record<
    string,
    OmsiSplineDefinition
  >,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >,
  showAllSplineProfiles: boolean
) {
  let rendered = 0;

  for (
    let splineIndex = 0;
    splineIndex < splines.length &&
    rendered < maxMapSplineProfiles;
    splineIndex += 1
  ) {
    const placedSpline =
      splines[splineIndex];

    if (
      !showAllSplineProfiles &&
      activeTile &&
      Math.max(
        Math.abs(
          placedSpline.tileX -
            activeTile.x
        ),
        Math.abs(
          placedSpline.tileY -
            activeTile.y
        )
      ) >
        mapSplineProfileTileRadius
    ) {
      continue;
    }

    if (
      isSameSpline(
        placedSpline,
        selectedSpline
      )
    ) {
      continue;
    }

    const definition =
      splineProfilesByPath[
        placedSpline.splinePath
      ];

    if (
      !definition?.exists ||
      definition.surfaces.length === 0
    ) {
      continue;
    }

    createSelectedSplineProfile(
      scene,
      placedSpline,
      definition,
      textureAssetsByKey,
      undefined,
      `map-spline-profile-${splineIndex}`
    );

    rendered += 1;
  }

  return rendered;
}

function createSelectedMarkerLines(
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined,
  tiles: OmsiTile[]
) {
  const position =
    getObjectWorldPosition(
      placedObject,
      geometry,
      tiles
    );
  const radius = 4.5;
  const height = 7.5;
  const bottomY =
    position.y + 0.18;
  const topY =
    bottomY + height;

  const bottom = [
    new Vector3(position.x - radius, bottomY, position.z - radius),
    new Vector3(position.x + radius, bottomY, position.z - radius),
    new Vector3(position.x + radius, bottomY, position.z + radius),
    new Vector3(position.x - radius, bottomY, position.z + radius),
    new Vector3(position.x - radius, bottomY, position.z - radius)
  ];

  const top = [
    new Vector3(position.x - radius, topY, position.z - radius),
    new Vector3(position.x + radius, topY, position.z - radius),
    new Vector3(position.x + radius, topY, position.z + radius),
    new Vector3(position.x - radius, topY, position.z + radius),
    new Vector3(position.x - radius, topY, position.z - radius)
  ];

  return [
    bottom,
    top,
    [
      bottom[0],
      top[0]
    ],
    [
      bottom[1],
      top[1]
    ],
    [
      bottom[2],
      top[2]
    ],
    [
      bottom[3],
      top[3]
    ],
    [
      new Vector3(
        position.x - 1.4,
        bottomY,
        position.z
      ),
      new Vector3(
        position.x + 1.4,
        bottomY,
        position.z
      )
    ],
    [
      new Vector3(
        position.x,
        bottomY,
        position.z - 1.4
      ),
      new Vector3(
        position.x,
        bottomY,
        position.z + 1.4
      )
    ]
  ];
}


const degreesToRadians = Math.PI / 180;

function clamp01(value: number) {
  return Math.min(1, Math.max(0, value));
}

function createTextureFromAsset(
  scene: Scene,
  asset:
    | OmsiTextureAsset
    | undefined
) {
  if (
    !asset?.exists ||
    !asset.extension ||
    (
      !asset.rgbaBase64 &&
      !asset.base64Data
    )
  ) {
    return undefined;
  }

  const mimeType =
    asset.mimeType ??
    "application/octet-stream";

  if (
    asset.rgbaBase64 &&
    asset.width &&
    asset.height
  ) {
    try {
      const binary =
        window.atob(
          asset.rgbaBase64
        );

      const pixels =
        new Uint8Array(
          binary.length
        );

      for (
        let index = 0;
        index < binary.length;
        index += 1
      ) {
        pixels[index] =
          binary.charCodeAt(index);
      }

      const rawTexture =
        RawTexture.CreateRGBATexture(
          pixels,
          asset.width,
          asset.height,
          scene,
          true,
          false,
          Texture.TRILINEAR_SAMPLINGMODE
        );

      rawTexture.name =
        `raw-${asset.sourceExtension ?? asset.extension}-${asset.width}x${asset.height}`;

      rawTexture.hasAlpha = false;
      rawTexture.gammaSpace = true;

      return rawTexture;
    } catch (error) {
      console.error(
        "OMSI Map Studio: raw texture upload failed",
        {
          extension:
            asset.extension,
          sourceExtension:
            asset.sourceExtension,
          width: asset.width,
          height: asset.height,
          error
        }
      );
    }
  }

  if (!asset.base64Data) {
    return undefined;
  }

  const forcedExtension =
    asset.extension === ".dds" ||
    asset.extension === ".tga"
      ? asset.extension
      : undefined;

  const dataUri =
    `data:${mimeType};base64,${asset.base64Data}`;

  const onTextureError = () => {
    console.error(
      "OMSI Map Studio: texture upload failed",
      {
        extension:
          asset.extension,
        sourceExtension:
          asset.sourceExtension,
        mimeType,
        resolvedPath:
          asset.resolvedPath
      }
    );
  };

  const texture =
    forcedExtension
      ? new Texture(
          dataUri,
          scene,
          {
            forcedExtension,
            noMipmap: false,
            invertY: false,
            samplingMode:
              Texture.TRILINEAR_SAMPLINGMODE,
            onError:
              onTextureError
          }
        )
      : new Texture(
          dataUri,
          scene,
          false,
          false,
          Texture.TRILINEAR_SAMPLINGMODE,
          undefined,
          onTextureError
        );

  texture.name =
    asset.resolvedPath ??
    `inline-${asset.sourceExtension ?? asset.extension}`;

  texture.hasAlpha = true;

  return texture;
}

function normalizeTextureFileName(
  value: string
) {
  return value
    .replace(/\\/g, "/")
    .split("/")
    .at(-1)
    ?.toLocaleLowerCase("en-US") ??
    value.toLocaleLowerCase(
      "en-US"
    );
}

function findMaterialOverride(
  overrides:
    OmsiSceneryMaterialOverride[],
  materials:
    OmsiSceneryObjectGeometry["meshes"][number]["geometry"]["materials"],
  materialIndex: number,
  textureName:
    | string
    | null
    | undefined
) {
  if (
    materialIndex < 0 ||
    !textureName
  ) {
    return undefined;
  }

  const normalized =
    normalizeTextureFileName(
      textureName
    );

  const occurrenceIndex =
    materials
      .slice(0, materialIndex + 1)
      .filter(
        (material) =>
          material.textureName &&
          normalizeTextureFileName(
            material.textureName
          ) === normalized
      ).length - 1;

  return overrides.find(
    (override) =>
      override.materialIndex ===
        occurrenceIndex &&
      normalizeTextureFileName(
        override.textureName
      ) === normalized
  );
}

function createPreviewMaterial(
  scene: Scene,
  namePrefix: string,
  meshIndex: number,
  materialIndex: number,
  materialData:
    | OmsiSceneryObjectGeometry["meshes"][number]["geometry"]["materials"][number]
    | undefined,
  textureAsset:
    | OmsiTextureAsset
    | undefined,
  materialOverride:
    | OmsiSceneryMaterialOverride
    | undefined,
  bumpTextureAsset:
    | OmsiTextureAsset
    | undefined,
  nightTextureAsset:
    | OmsiTextureAsset
    | undefined,
  environmentTextureAsset:
    | OmsiTextureAsset
    | undefined,
  transparencyTextureAsset:
    | OmsiTextureAsset
    | undefined,
  nightPreviewEnabled: boolean
) {
  const material = new StandardMaterial(
    `${namePrefix}-material-${meshIndex}-${materialIndex}`,
    scene
  );

  material.backFaceCulling = false;
  material.twoSidedLighting = true;

  if (!materialData) {
    material.diffuseColor =
      new Color3(0.72, 0.76, 0.82);
    material.specularColor =
      new Color3(0.12, 0.12, 0.12);
    return material;
  }

  material.diffuseColor = new Color3(
    clamp01(materialData.diffuseR),
    clamp01(materialData.diffuseG),
    clamp01(materialData.diffuseB)
  );

  // OMSI scenery transparency is controlled by the SCO material
  // directives such as [matl_alpha]. The embedded O3D diffuse alpha
  // is not used as a blanket object opacity by the reference importer.
  // Keeping it at 1 avoids valid O3D meshes disappearing completely.
  material.alpha = 1;

  material.specularColor = new Color3(
    clamp01(materialData.specularR),
    clamp01(materialData.specularG),
    clamp01(materialData.specularB)
  );

  material.emissiveColor = new Color3(
    clamp01(materialData.emissionR),
    clamp01(materialData.emissionG),
    clamp01(materialData.emissionB)
  );

  material.specularPower = Math.max(
    1,
    materialData.specularPower
  );

  const texture =
    createTextureFromAsset(
      scene,
      textureAsset
    );

  if (texture) {
    material.diffuseTexture =
      texture;

    // OMSI/O3D rendering uses the diffuse texture RGB as the
    // surface colour when a main texture is present. Babylon's
    // StandardMaterial multiplies diffuseTexture by diffuseColor,
    // which was making valid OMSI textures appear dark/black.
    material.diffuseColor =
      Color3.White();

    const alphaMode =
      materialOverride?.alphaMode;

    if (alphaMode === 0) {
      material.useAlphaFromDiffuseTexture =
        false;

      material.transparencyMode =
        Material.MATERIAL_OPAQUE;
    } else if (alphaMode === 1) {
      texture.hasAlpha = true;

      material.useAlphaFromDiffuseTexture =
        true;

      material.transparencyMode =
        Material.MATERIAL_ALPHATEST;

      material.alphaCutOff = 0.4;
    } else if (alphaMode === 2) {
      texture.hasAlpha = true;

      material.useAlphaFromDiffuseTexture =
        true;

      material.transparencyMode =
        Material.MATERIAL_ALPHABLEND;
    } else {
      material.useAlphaFromDiffuseTexture =
        false;
    }
  }

  const transparencyTexture =
    createTextureFromAsset(
      scene,
      transparencyTextureAsset
    );

  if (transparencyTexture) {
    // OMSI [matl_transmap] supplies a dedicated transparency mask.
    // It is separate from [matl_alpha], which reads alpha from the
    // diffuse texture. Grayscale BMP/TGA transmaps need their RGB
    // intensity interpreted as opacity in Babylon.
    transparencyTexture.hasAlpha = true;
    transparencyTexture.getAlphaFromRGB = true;

    material.opacityTexture =
      transparencyTexture;

    material.transparencyMode =
      Material.MATERIAL_ALPHATESTANDBLEND;

    material.alphaCutOff = 0.4;
  }

  const bumpTexture =
    createTextureFromAsset(
      scene,
      bumpTextureAsset
    );

  if (bumpTexture) {
    material.bumpTexture =
      bumpTexture;

    bumpTexture.level =
      materialOverride
        ?.bumpMapStrength ??
      1;
  }

  if (
    materialOverride
      ?.environmentMapTextureName
  ) {
    const environmentTexture =
      createTextureFromAsset(
        scene,
        environmentTextureAsset
      );

    if (environmentTexture) {
      environmentTexture
        .coordinatesMode =
        Texture.SPHERICAL_MODE;

      environmentTexture.level =
        clamp01(
          materialOverride
            .environmentMapStrength ??
          1
        );

      material.reflectionTexture =
        environmentTexture;
    }
  }

  if (
    nightPreviewEnabled &&
    materialOverride
      ?.nightMapTextureName
  ) {
    const nightTexture =
      createTextureFromAsset(
        scene,
        nightTextureAsset
      );

    if (nightTexture) {
      material.emissiveTexture =
        nightTexture;
      material.emissiveColor =
        Color3.White();
    }
  }

  if (materialOverride?.noZWrite) {
    material.disableDepthWrite =
      true;
  }

  if (materialOverride?.noZCheck) {
    material.depthFunction =
      Engine.ALWAYS;
  }

  return material;
}

type ObjectLodInstance = {
  root: TransformNode;
  meshes: Mesh[];
  radius: number;
  thresholds: number[];
};

function getPlacedTreeVisual(
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined
) {
  if (!geometry?.tree) {
    return undefined;
  }

  const values =
    placedObject.extraValues;

  if (
    !values ||
    values.length < 4
  ) {
    return undefined;
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
    return undefined;
  }

  return {
    textureName,
    height,
    aspect,
    width:
      height * aspect
  };
}

function getTreeTextureAsset(
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >
) {
  const tree =
    getPlacedTreeVisual(
      placedObject,
      geometry
    );

  if (!tree) {
    return undefined;
  }

  return textureAssetsByKey[
    getSceneryTextureAssetKey(
      placedObject.sceneryObjectPath,
      sceneryTreeTextureMeshToken,
      tree.textureName
    )
  ];
}

function hasRenderableTree(
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >
) {
  const tree =
    getPlacedTreeVisual(
      placedObject,
      geometry
    );

  const asset =
    getTreeTextureAsset(
      placedObject,
      geometry,
      textureAssetsByKey
    );

  return Boolean(
    tree &&
    asset?.exists &&
    asset.base64Data
  );
}

function createPlacedTree(
  scene: Scene,
  name: string,
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >,
  materialCache?: Map<
    string,
    StandardMaterial
  >
) {
  const tree =
    getPlacedTreeVisual(
      placedObject,
      geometry
    );

  const asset =
    getTreeTextureAsset(
      placedObject,
      geometry,
      textureAssetsByKey
    );

  if (
    !tree ||
    !asset?.exists ||
    !asset.base64Data
  ) {
    return undefined;
  }

  const materialKey =
    getSceneryTextureAssetKey(
      placedObject.sceneryObjectPath,
      sceneryTreeTextureMeshToken,
      tree.textureName
    );

  let material =
    materialCache?.get(
      materialKey
    );

  if (!material) {
    const texture =
      createTextureFromAsset(
        scene,
        asset
      );

    if (!texture) {
      return undefined;
    }

    texture.hasAlpha = true;

    // OMSI [tree] billboard images use the opposite vertical texture
    // origin from Babylon's plane UV convention. Keep the generic
    // scenery/O3D texture path unchanged and flip only tree billboards.
    texture.vScale = -1;
    texture.vOffset = 1;

    material =
      new StandardMaterial(
        `${name}-material`,
        scene
      );

    material.diffuseColor =
      Color3.Black();

    material.diffuseTexture =
      texture;

    material.emissiveColor =
      Color3.White();

    material.emissiveTexture =
      texture;

    material.useAlphaFromDiffuseTexture =
      true;

    material.transparencyMode =
      Material.MATERIAL_ALPHATESTANDBLEND;

    material.alphaCutOff = 0.25;
    material.backFaceCulling = false;
    material.disableLighting = true;

    materialCache?.set(
      materialKey,
      material
    );
  }

  const mesh =
    MeshBuilder.CreatePlane(
      name,
      {
        width: tree.width,
        height: tree.height
      },
      scene
    );

  mesh.position.y =
    tree.height / 2;

  mesh.billboardMode =
    Mesh.BILLBOARDMODE_Y;

  mesh.material = material;
  mesh.isPickable = false;
  mesh.metadata = {
    ...(mesh.metadata ?? {}),
    mapStudioTree: true,
    treeTexture:
      tree.textureName,
    treeHeight:
      tree.height,
    treeAspect:
      tree.aspect
  };

  return mesh;
}

function getGeometryRadius(
  geometry: OmsiSceneryObjectGeometry
) {
  let radiusSquared = 0;

  for (const meshReference of
    geometry.meshes) {
    const positions =
      meshReference.geometry.positions;

    for (
      let index = 0;
      index + 2 < positions.length;
      index += 3
    ) {
      const x = positions[index];
      const y = positions[index + 1];
      const z = positions[index + 2];

      radiusSquared = Math.max(
        radiusSquared,
        x * x + y * y + z * z
      );
    }
  }

  return Math.max(
    0.5,
    Math.sqrt(radiusSquared)
  );
}

function getMeshLodThreshold(
  mesh: Mesh
) {
  const value =
    mesh.metadata
      ?.mapStudioLodThreshold;

  return typeof value === "number" &&
    Number.isFinite(value)
    ? value
    : null;
}

function createObjectLodInstance(
  root: TransformNode,
  meshes: Mesh[],
  geometry: OmsiSceneryObjectGeometry
):
  | ObjectLodInstance
  | undefined {
  const thresholds =
    Array.from(
      new Set(
        geometry.meshes
          .map(
            (meshReference) =>
              meshReference.lodThreshold
          )
          .filter(
            (
              value
            ): value is number =>
              typeof value === "number" &&
              Number.isFinite(value)
          )
      )
    ).sort(
      (left, right) =>
        right - left
    );

  if (thresholds.length === 0) {
    return undefined;
  }

  return {
    root,
    meshes,
    radius:
      getGeometryRadius(
        geometry
      ),
    thresholds
  };
}

function updateObjectLod(
  instance: ObjectLodInstance,
  camera: ArcRotateCamera
) {
  const distance =
    Vector3.Distance(
      camera.position,
      instance.root
        .getAbsolutePosition()
    );

  const angularDiameter =
    distance <= 0.0001
      ? Math.PI
      : 2 *
        Math.atan2(
          instance.radius,
          distance
        );

  const screenFraction =
    camera.fov > 0
      ? angularDiameter /
        camera.fov
      : 1;

  const selectedThreshold =
    instance.thresholds.find(
      (threshold) =>
        screenFraction >=
        threshold
    ) ??
    instance.thresholds[0];

  for (const mesh of
    instance.meshes) {
    const threshold =
      getMeshLodThreshold(
        mesh
      );

    mesh.setEnabled(
      threshold === null ||
      threshold ===
        selectedThreshold
    );
  }
}

function getHorizontalSurfaceRenderLift(
  positions: number[]
) {
  if (positions.length < 9) {
    return 0;
  }

  let minX = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;
  let minZ = Number.POSITIVE_INFINITY;
  let maxZ = Number.NEGATIVE_INFINITY;

  for (
    let index = 0;
    index + 2 < positions.length;
    index += 3
  ) {
    const x = positions[index];
    const y = positions[index + 1];
    const z = positions[index + 2];

    minX = Math.min(minX, x);
    maxX = Math.max(maxX, x);
    minY = Math.min(minY, y);
    maxY = Math.max(maxY, y);
    minZ = Math.min(minZ, z);
    maxZ = Math.max(maxZ, z);
  }

  const verticalSpan = maxY - minY;
  const horizontalSpan =
    Math.max(
      maxX - minX,
      maxZ - minZ
    );

  return (
    verticalSpan <= 0.35 &&
    horizontalSpan >= 2
  )
    ? 0.10
    : 0;
}

function createGeometryMeshes(
  scene: Scene,
  namePrefix: string,
  sceneryObjectPath: string,
  geometry: OmsiSceneryObjectGeometry,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >,
  nightPreviewEnabled: boolean
) {
  const meshes: Mesh[] = [];

  for (const [meshIndex, meshReference]
    of geometry.meshes.entries()) {
    const meshGeometry =
      meshReference.geometry;

    if (
      !meshGeometry.isLoaded ||
      meshGeometry.positions.length === 0 ||
      meshGeometry.indices.length === 0
    ) {
      continue;
    }

    const triangleCount =
      Math.floor(
        meshGeometry.indices.length / 3
      );

    const groups =
      new Map<number, number[]>();

    for (
      let triangleIndex = 0;
      triangleIndex < triangleCount;
      triangleIndex += 1
    ) {
      const materialIndex =
        meshGeometry
          .triangleMaterialIndices[
            triangleIndex
          ] ?? -1;

      const groupedIndices =
        groups.get(materialIndex) ?? [];

      const indexOffset =
        triangleIndex * 3;

      groupedIndices.push(
        meshGeometry.indices[indexOffset],
        meshGeometry.indices[indexOffset + 1],
        meshGeometry.indices[indexOffset + 2]
      );

      groups.set(
        materialIndex,
        groupedIndices
      );
    }

    if (groups.size === 0) {
      groups.set(
        -1,
        meshGeometry.indices
      );
    }

    for (const [
      materialIndex,
      groupedIndices
    ] of groups) {
      const mesh = new Mesh(
        `${namePrefix}-mesh-${meshIndex}-${materialIndex}`,
        scene
      );

      const vertexData =
        new VertexData();

      vertexData.positions =
        meshGeometry.positions;

      vertexData.normals =
        meshGeometry.normals;

      vertexData.uvs =
        meshGeometry.uvs;

      vertexData.indices =
        groupedIndices;

      vertexData.applyToMesh(
        mesh,
        false
      );

      mesh.material =
        createPreviewMaterial(
          scene,
          namePrefix,
          meshIndex,
          materialIndex,
          materialIndex >= 0
            ? meshGeometry.materials[
                materialIndex
              ]
            : undefined,
          materialIndex >= 0
            ? (() => {
                const materialData =
                  meshGeometry.materials[
                    materialIndex
                  ];

                if (!materialData?.textureName) {
                  return undefined;
                }

                return textureAssetsByKey[
                  getSceneryTextureAssetKey(
                    sceneryObjectPath,
                    meshReference.declaredPath,
                    materialData.textureName
                  )
                ];
              })()
            : undefined,
          materialIndex >= 0
            ? findMaterialOverride(
                meshReference
                  .materialOverrides,
                meshGeometry.materials,
                materialIndex,
                meshGeometry.materials[
                  materialIndex
                ]?.textureName
              )
            : undefined,
          materialIndex >= 0
            ? (() => {
                const materialOverride =
                  findMaterialOverride(
                    meshReference
                      .materialOverrides,
                    meshGeometry.materials,
                    materialIndex,
                    meshGeometry.materials[
                      materialIndex
                    ]?.textureName
                  );

                const textureName =
                  materialOverride
                    ?.bumpMapTextureName;

                if (!textureName) {
                  return undefined;
                }

                return textureAssetsByKey[
                  getSceneryTextureAssetKey(
                    sceneryObjectPath,
                    meshReference
                      .declaredPath,
                    textureName
                  )
                ];
              })()
            : undefined,
          materialIndex >= 0
            ? (() => {
                const materialOverride =
                  findMaterialOverride(
                    meshReference
                      .materialOverrides,
                    meshGeometry.materials,
                    materialIndex,
                    meshGeometry.materials[
                      materialIndex
                    ]?.textureName
                  );

                const textureName =
                  materialOverride
                    ?.nightMapTextureName;

                if (!textureName) {
                  return undefined;
                }

                return textureAssetsByKey[
                  getSceneryTextureAssetKey(
                    sceneryObjectPath,
                    meshReference
                      .declaredPath,
                    textureName
                  )
                ];
              })()
            : undefined,
          materialIndex >= 0
            ? (() => {
                const materialOverride =
                  findMaterialOverride(
                    meshReference
                      .materialOverrides,
                    meshGeometry.materials,
                    materialIndex,
                    meshGeometry.materials[
                      materialIndex
                    ]?.textureName
                  );

                const textureName =
                  materialOverride
                    ?.environmentMapTextureName;

                if (!textureName) {
                  return undefined;
                }

                return textureAssetsByKey[
                  getSceneryTextureAssetKey(
                    sceneryObjectPath,
                    meshReference
                      .declaredPath,
                    textureName
                  )
                ];
              })()
            : undefined,
          materialIndex >= 0
            ? (() => {
                const materialOverride =
                  findMaterialOverride(
                    meshReference
                      .materialOverrides,
                    meshGeometry.materials,
                    materialIndex,
                    meshGeometry.materials[
                      materialIndex
                    ]?.textureName
                  );

                const textureName =
                  materialOverride
                    ?.transMapSource
                    ?.trim();

                if (
                  !textureName ||
                  textureName.startsWith(
                    "\\S:"
                  )
                ) {
                  return undefined;
                }

                return textureAssetsByKey[
                  getSceneryTextureAssetKey(
                    sceneryObjectPath,
                    meshReference
                      .declaredPath,
                    textureName
                  )
                ];
              })()
            : undefined,
          nightPreviewEnabled
        );

      const normalizedRenderType =
        geometry.renderType
          ?.trim()
          .toLocaleLowerCase("en-US");

      const isOmsiSurfaceRenderType =
        normalizedRenderType ===
          "surface" ||
        normalizedRenderType ===
          "on_surface" ||
        normalizedRenderType ===
          "presurface";

      // Intersections and road scenery commonly declare their real
      // OMSI [rendertype]. Respect that source metadata in the preview:
      // surface materials use their actual diffuse texture unlit so
      // pack-specific normals cannot make a valid crossing disappear
      // into black. Coordinates and geometry remain untouched.
      if (
        isOmsiSurfaceRenderType &&
        mesh.material instanceof
          StandardMaterial &&
        mesh.material.diffuseTexture
      ) {
        mesh.material.disableLighting =
          true;
        mesh.material.diffuseColor =
          Color3.White();
        mesh.material.emissiveColor =
          Color3.White();
        mesh.material.emissiveTexture =
          mesh.material.diffuseTexture;
      }

      const meshTransform =
        meshReference.transform;

      // SCO per-mesh transforms are expressed in the model/O3D
      // coordinate system, not in the map tile coordinate system.
      // O3D is already Y-up, just like Babylon, so these transforms
      // must stay on their native axes. Only the placed [object]
      // container converts OMSI map Z-up coordinates to the viewport.
      const renderLift =
        getHorizontalSurfaceRenderLift(
          meshGeometry.positions
        );

      mesh.position.set(
        meshTransform.positionX,
        meshTransform.positionY +
          renderLift,
        meshTransform.positionZ
      );

      if (renderLift > 0) {
        mesh.metadata = {
          ...(mesh.metadata ?? {}),
          mapStudioRenderLift:
            renderLift
        };

        if (
          mesh.material instanceof
            StandardMaterial
        ) {
          mesh.material.zOffset = -2;

          // Thin horizontal O3D surfaces (junction/road meshes) often
          // carry downward-facing or pack-specific normals. The editor
          // must still show the real albedo rather than turning the
          // surface black because of the preview light. This changes
          // lighting only; OMSI coordinates and the existing render
          // lift are left untouched.
          if (mesh.material.diffuseTexture) {
            mesh.material.disableLighting =
              true;
            mesh.material.diffuseColor =
              Color3.White();
            mesh.material.emissiveColor =
              Color3.White();
            mesh.material.emissiveTexture =
              mesh.material.diffuseTexture;
          }
        }
      }

      mesh.scaling.set(
        meshTransform.scaleX,
        meshTransform.scaleY,
        meshTransform.scaleZ
      );

      mesh.rotationQuaternion =
        Quaternion.RotationYawPitchRoll(
          meshTransform.rotationY *
            degreesToRadians,
          meshTransform.rotationX *
            degreesToRadians,
          meshTransform.rotationZ *
            degreesToRadians
        );

      const diagnosticMaterialData =
        materialIndex >= 0
          ? meshGeometry.materials[
              materialIndex
            ]
          : undefined;

      const diagnosticTextureAsset =
        diagnosticMaterialData
          ?.textureName
          ? textureAssetsByKey[
              getSceneryTextureAssetKey(
                sceneryObjectPath,
                meshReference.declaredPath,
                diagnosticMaterialData
                  .textureName
              )
            ]
          : undefined;

      mesh.metadata = {
        ...(mesh.metadata ?? {}),
        mapStudioLodThreshold:
          meshReference
            .lodThreshold,
        mapStudioSource: "SCO/O3D",
        mapStudioRenderType:
          geometry.renderType ?? null,
        mapStudioOrigin:
          `${sceneryObjectPath} -> ${meshReference.declaredPath}`,
        mapStudioTextureName:
          diagnosticMaterialData
            ?.textureName ?? null,
        mapStudioResolvedTexture:
          (() => {
          const asset = diagnosticTextureAsset;

          if (!asset) {
            return "asset pendente/ausente";
          }

          if (!asset.exists) {
            return asset.errorCode ??
              "asset ausente";
          }

          const dimensions =
            asset.width &&
            asset.height
              ? `${asset.width}x${asset.height}`
              : "dimensões n/d";

          return [
            asset.resolvedPath ??
              "caminho não exposto",
            dimensions,
            asset.pixelFormat ??
              asset.sourceExtension ??
              asset.extension ??
              "formato n/d"
          ].join(" · ");
        })()
      };

      mesh.isPickable = false;
      meshes.push(mesh);
    }
  }

  return meshes;
}

function configureObjectRoot(
  root: TransformNode,
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined,
  tiles: OmsiTile[]
) {
  root.position.copyFrom(
    getObjectWorldPosition(
      placedObject,
      geometry,
      tiles
    )
  );

  // OMSI map object rotations are stored in Z/Y/X order in a
  // Z-up coordinate system. After mapping OMSI (X,Y,Z) to the
  // Babylon viewport (X,Z,Y), the equivalent Babylon axes are:
  // yaw(Y)=OMSI Z, pitch(X)=OMSI X, roll(Z)=OMSI Y.
  root.rotationQuaternion =
    Quaternion.RotationYawPitchRoll(
      placedObject.rotation *
        degreesToRadians,
      placedObject.pitch *
        degreesToRadians,
      placedObject.bank *
        degreesToRadians
    );
}

function getSplineWorldStart(
  placedSpline: OmsiPlacedSpline
) {
  return new Vector3(
    placedSpline.tileX * 300 +
      placedSpline.x,
    placedSpline.z,
    placedSpline.tileY * 300 +
      placedSpline.y
  );
}

function createSplineEditRoot(
  scene: Scene,
  placedSpline: OmsiPlacedSpline,
  definition:
    | OmsiSplineDefinition
    | undefined,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >
) {
  const root =
    new TransformNode(
      "selected-spline-edit-root",
      scene
    );

  root.position.copyFrom(
    getSplineWorldStart(
      placedSpline
    )
  );

  root.rotationQuaternion =
    Quaternion.RotationYawPitchRoll(
      placedSpline.rotation *
        degreesToRadians,
      0,
      0
    );

  root.metadata = {
    ...(root.metadata ?? {}),
    mapStudioKind: "spline",
    placedSpline
  };

  const localSpline: OmsiPlacedSpline = {
    ...placedSpline,
    tileX: 0,
    tileY: 0,
    x: 0,
    y: 0,
    z: 0,
    rotation: 0
  };

  const points =
    getSplineAxisLine(
      localSpline
    );

  if (points.length >= 2) {
    const axis =
      MeshBuilder.CreateLines(
        "selected-spline-edit-axis",
        { points },
        scene
      );

    axis.color =
      new Color3(
        0.25,
        1,
        0.65
      );

    axis.isPickable = false;
    axis.parent = root;
  }

  if (definition) {
    createSelectedSplineProfile(
      scene,
      localSpline,
      definition,
      textureAssetsByKey,
      root
    );

    // The profile is built from a local zero-based spline so it can follow
    // the edit root. Keep the original placed spline identity on its
    // pickable meshes; otherwise a click on the W/E preview could select
    // the synthetic local copy instead of the real map item.
    for (const mesh of root.getChildMeshes()) {
      if (
        mesh.metadata?.mapStudioKind ===
        "spline"
      ) {
        mesh.isPickable = true;
        mesh.renderOutline = true;
        mesh.outlineColor =
          new Color3(0.3, 0.82, 1);
        mesh.outlineWidth = 0.035;
        mesh.metadata = {
          ...(mesh.metadata ?? {}),
          placedSpline
        };
      }
    }
  }

  return root;
}

function hasRenderableGeometry(
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined
) {
  return Boolean(
    geometry?.meshes.some(
      (meshReference) =>
        meshReference.geometry.isLoaded &&
        meshReference.geometry
          .positions.length > 0 &&
        meshReference.geometry
          .indices.length > 0
    )
  );
}

function hasProtectedGeometry(
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined
) {
  return Boolean(
    geometry?.meshes.some(
      (meshReference) => {
        const code =
          meshReference.geometry
            .errorCode;

        return Boolean(
          code === "encrypted" ||
          code?.startsWith(
            "protected"
          )
        );
      }
    )
  );
}

function hasRenderableObjectVisual(
  placedObject: OmsiPlacedObject,
  geometry:
    | OmsiSceneryObjectGeometry
    | undefined,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >
) {
  return (
    hasRenderableGeometry(
      geometry
    ) ||
    hasRenderableTree(
      placedObject,
      geometry,
      textureAssetsByKey
    )
  );
}

function createSelectedGeometry(
  scene: Scene,
  placedObject: OmsiPlacedObject,
  geometry: OmsiSceneryObjectGeometry,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >,
  nightPreviewEnabled: boolean,
  lodInstances:
    ObjectLodInstance[],
  tiles: OmsiTile[]
) {
  const root = new TransformNode(
    "selected-object-geometry-root",
    scene
  );

  configureObjectRoot(
    root,
    placedObject,
    geometry,
    tiles
  );

  // Keep the editable preview attached to the same real OMSI item.
  // This lets click selection keep working after switching to move/rotate
  // instead of resolving only the untouched map instance behind it.
  root.metadata = {
    ...(root.metadata ?? {}),
    mapStudioKind: "object",
    placedObject
  };

  const meshes =
    geometry.tree
      ? []
      : createGeometryMeshes(
          scene,
          "selected-object",
          placedObject.sceneryObjectPath,
          geometry,
          textureAssetsByKey,
          nightPreviewEnabled
        );

  const tree =
    createPlacedTree(
      scene,
      "selected-object-tree",
      placedObject,
      geometry,
      textureAssetsByKey
    );

  if (tree) {
    tree.parent = root;
    tree.isPickable = true;
    tree.renderOutline = true;
    tree.outlineColor =
      new Color3(0.3, 0.82, 1);
    tree.outlineWidth = 0.035;
    tree.metadata = {
      ...(tree.metadata ?? {}),
      mapStudioKind: "object",
      placedObject
    };
  }

  for (const mesh of meshes) {
    mesh.parent = root;
    mesh.isPickable = true;
    mesh.renderOutline = true;
    mesh.outlineColor =
      new Color3(0.3, 0.82, 1);
    mesh.outlineWidth = 0.035;
    mesh.metadata = {
      ...(mesh.metadata ?? {}),
      mapStudioKind: "object",
      placedObject
    };
  }

  const lodInstance =
    createObjectLodInstance(
      root,
      meshes,
      geometry
    );

  if (lodInstance) {
    lodInstances.push(
      lodInstance
    );
  }

  return root;
}

function createMapObjectGeometry(
  scene: Scene,
  objects: OmsiPlacedObject[],
  geometryByPath: Record<
    string,
    OmsiSceneryObjectGeometry
  >,
  textureAssetsByKey: Record<
    string,
    OmsiTextureAsset
  >,
  nightPreviewEnabled: boolean,
  lodInstances:
    ObjectLodInstance[],
  tiles: OmsiTile[]
) {
  const placementsByPath =
    new Map<
      string,
      OmsiPlacedObject[]
    >();

  const treeMaterialCache =
    new Map<
      string,
      StandardMaterial
    >();

  for (const placedObject of objects) {
    const current =
      placementsByPath.get(
        placedObject.sceneryObjectPath
      ) ?? [];

    current.push(
      placedObject
    );

    placementsByPath.set(
      placedObject.sceneryObjectPath,
      current
    );
  }

  for (const [
    sceneryObjectPath,
    placements
  ] of placementsByPath) {
    const geometry =
      geometryByPath[
        sceneryObjectPath
      ];

    if (
      placements.length === 0 ||
      !geometry ||
      !placements.some(
        (placedObject) =>
          hasRenderableObjectVisual(
            placedObject,
            geometry,
            textureAssetsByKey
          )
      )
    ) {
      continue;
    }

    const sourceRoot =
      new TransformNode(
        `map-object-root-${sceneryObjectPath}-0`,
        scene
      );

    configureObjectRoot(
      sourceRoot,
      placements[0],
      geometry,
      tiles
    );

    sourceRoot.metadata = {
      mapStudioKind: "object",
      placedObject: placements[0]
    };

    // OMSI [tree] scenery uses its generated billboard as the
    // actual visual. Packs commonly include treehelper.x only as an
    // editor helper; rendering it together with the billboard creates
    // the large gray helper planes seen in the map preview.
    const sourceMeshes =
      geometry.tree
        ? []
        : createGeometryMeshes(
            scene,
            `map-object-${sceneryObjectPath}`,
            sceneryObjectPath,
            geometry,
            textureAssetsByKey,
            nightPreviewEnabled
          );

    for (const source of sourceMeshes) {
      source.parent = sourceRoot;
      source.isPickable = true;
      source.metadata = {
        ...(source.metadata ?? {}),
        mapStudioKind: "object",
        placedObject: placements[0]
      };
    }

    const sourceTree =
      createPlacedTree(
        scene,
        `map-tree-${sceneryObjectPath}-0`,
        placements[0],
        geometry,
        textureAssetsByKey,
        treeMaterialCache
      );

    if (sourceTree) {
      sourceTree.parent =
        sourceRoot;
      sourceTree.isPickable = true;
      sourceTree.metadata = {
        ...(sourceTree.metadata ?? {}),
        mapStudioKind: "object",
        placedObject: placements[0]
      };
    }

    const sourceLodInstance =
      createObjectLodInstance(
        sourceRoot,
        sourceMeshes,
        geometry
      );

    if (sourceLodInstance) {
      lodInstances.push(
        sourceLodInstance
      );
    }

    for (
      let placementIndex = 1;
      placementIndex <
        placements.length;
      placementIndex += 1
    ) {
      const root =
        new TransformNode(
          `map-object-root-${sceneryObjectPath}-${placementIndex}`,
          scene
        );

      configureObjectRoot(
        root,
        placements[
          placementIndex
        ],
        geometry,
        tiles
      );

      root.metadata = {
        mapStudioKind: "object",
        placedObject:
          placements[placementIndex]
      };

      const cloneMeshes:
        Mesh[] = [];

      for (
        let meshIndex = 0;
        meshIndex <
          sourceMeshes.length;
        meshIndex += 1
      ) {
        const source =
          sourceMeshes[
            meshIndex
          ];

        const clone =
          source.clone(
            `map-object-instance-${placementIndex}-${meshIndex}`,
            root
          );

        if (clone) {
          clone.metadata = {
            ...(source.metadata ?? {}),
            mapStudioKind: "object",
            placedObject:
              placements[placementIndex]
          };

          clone.isPickable = true;
          cloneMeshes.push(clone);
        }
      }

      const tree =
        createPlacedTree(
          scene,
          `map-tree-${sceneryObjectPath}-${placementIndex}`,
          placements[
            placementIndex
          ],
          geometry,
          textureAssetsByKey,
          treeMaterialCache
        );

      if (tree) {
        tree.parent = root;
        tree.isPickable = true;
        tree.metadata = {
          ...(tree.metadata ?? {}),
          mapStudioKind: "object",
          placedObject:
            placements[placementIndex]
        };
      }

      const lodInstance =
        createObjectLodInstance(
          root,
          cloneMeshes,
          geometry
        );

      if (lodInstance) {
        lodInstances.push(
          lodInstance
        );
      }
    }
  }
}


function createOmsiSky(
  scene: Scene,
  asset:
    | OmsiTextureAsset
    | undefined,
  radius: number
) {
  const texture =
    createTextureFromAsset(
      scene,
      asset
    );

  if (!texture) {
    return;
  }

  texture.hasAlpha = false;
  texture.uScale = -1;
  texture.uOffset = 1;
  texture.wrapU =
    Texture.WRAP_ADDRESSMODE;
  texture.wrapV =
    Texture.CLAMP_ADDRESSMODE;

  const sky =
    MeshBuilder.CreateSphere(
      "omsi-sky",
      {
        diameter:
          Math.max(
            4000,
            radius * 2.8
          ),
        segments: 32,
        sideOrientation:
          Mesh.BACKSIDE
      },
      scene
    );

  sky.infiniteDistance = true;
  sky.isPickable = false;
  sky.alwaysSelectAsActiveMesh = true;
  sky.renderingGroupId = 0;

  const material =
    new StandardMaterial(
      "omsi-sky-material",
      scene
    );

  // Sky BMPs are delivered as browser-decodable PNG payloads by the
  // host. Render them as an unlit diffuse albedo instead of relying on
  // an emissive-only material, which could collapse to a white dome in
  // WebView2 while the texture object itself existed.
  material.diffuseColor =
    Color3.White();
  material.diffuseTexture =
    texture;
  material.specularColor =
    Color3.Black();
  material.emissiveColor =
    Color3.Black();
  material.emissiveTexture =
    null;
  material.disableLighting = true;
  material.backFaceCulling = false;
  material.disableDepthWrite = true;
  material.depthFunction = Engine.LEQUAL;

  sky.material = material;
  sky.freezeWorldMatrix();
}

export function Viewport({
  tiles,
  cameraStateKey,
  editorTool,
  selectionMode,
  snapEnabled,
  moveSnap,
  rotationSnap,
  showGrid,
  showTerrain,
  terrainMainTextureAsset,
  terrainMainTextureRepeating,
  terrainOverlays,
  showObjects,
  showSplines,
  showSplineProfiles,
  showAllSplineProfiles,
  nightPreviewEnabled,
  skyTextureAsset,
  cameraAction,
  placementAssetPath,
  placementGeometry,
  pendingPlacement,
  pendingPlacementBatch,
  onPlacementPoint,
  onLibraryAssetDrop,
  onThumbnailReady,
  splinePlacementTemplate,
  splinePlacementProfile,
  pendingSplinePlacement,
  onSplinePlacementPoint,
  roadDragMode,
  roadCurveControl,
  onRoadCurveOffsetChange,
  onRoadControlPointChange,
  objects,
  splines,
  activeTile,
  onActiveTileChange,
  onTerrainPoint,
  referenceOverlay,
  usesWorldCoordinates,
  selectedObject,
  selectedGeometry,
  objectGeometryByPath,
  textureAssetsByKey,
  splineProfilesByPath,
  selectedSpline,
  selectedSplineProfile,
  onSelectObject,
  onSelectSpline,
  onPreviewObjectTransform,
  onPreviewSplineTransform
}: ViewportProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const thumbnailCallbackRef =
    useRef(onThumbnailReady);
  thumbnailCallbackRef.current =
    onThumbnailReady;
  const captureThumbnail =
    Boolean(onThumbnailReady);

  const [
    viewportDiagnostic,
    setViewportDiagnostic
  ] = useState<
    ViewportDiagnostic | undefined
  >(undefined);

  const cameraStateRef = useRef<
    | {
        alpha: number;
        beta: number;
        radius: number;
        target: {
          x: number;
          y: number;
          z: number;
        };
      }
    | undefined
  >(undefined);

  const lastCameraActionTokenRef =
    useRef<number | undefined>(
      undefined
    );

  const roadDragPointerRef =
    useRef<number | undefined>(
      undefined
    );

  const roadDragLastEmitRef =
    useRef(0);

  const roadCurveDragPointerRef =
    useRef<number | undefined>(
      undefined
    );

  const roadCurveLastEmitRef =
    useRef(0);

  const roadEndpointDragPointerRef =
    useRef<number | undefined>(
      undefined
    );

  const roadEndpointDragControlRef =
    useRef<
      "start" | "end" | undefined
    >(undefined);

  const roadEndpointLastEmitRef =
    useRef(0);

  const lastMapItemClickRef =
    useRef<
      | {
          kind: "object" | "spline";
          key: string;
          at: number;
        }
      | undefined
    >(undefined);

  const cameraStateKeyRef =
    useRef(cameraStateKey);

  if (
    cameraStateKeyRef.current !==
    cameraStateKey
  ) {
    cameraStateKeyRef.current =
      cameraStateKey;
    cameraStateRef.current = undefined;
    lastCameraActionTokenRef.current =
      undefined;
  }

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const engine = new Engine(
      canvas,
      true,
      {
        preserveDrawingBuffer:
          captureThumbnail,
        stencil: true
      }
    );
    const scene = new Scene(engine);
    scene.clearColor.set(0.045, 0.055, 0.07, 1);

    const tileSize = usesWorldCoordinates ? 1 : 300;
    const tileXs = tiles.map((tile) => tile.x);
    const tileYs = tiles.map((tile) => tile.y);

    const minX = tileXs.length ? Math.min(...tileXs) : 0;
    const maxX = tileXs.length ? Math.max(...tileXs) : 0;
    const minY = tileYs.length ? Math.min(...tileYs) : 0;
    const maxY = tileYs.length ? Math.max(...tileYs) : 0;

    let terrainMinimum =
      Number.POSITIVE_INFINITY;

    let terrainMaximum =
      Number.NEGATIVE_INFINITY;

    if (
      showTerrain &&
      !usesWorldCoordinates
    ) {
      for (const tile of tiles) {
        if (
          !hasRenderableTerrain(
            tile,
            tileSize
          )
        ) {
          continue;
        }

        for (const height of
          tile.terrain!.heights) {
          terrainMinimum =
            Math.min(
              terrainMinimum,
              height
            );

          terrainMaximum =
            Math.max(
              terrainMaximum,
              height
            );
        }
      }
    }

    const terrainCenterHeight =
      Number.isFinite(
        terrainMinimum
      ) &&
      Number.isFinite(
        terrainMaximum
      )
        ? (
            terrainMinimum +
            terrainMaximum
          ) / 2
        : 0;

    const target = tiles.length
      ? new Vector3(
          ((minX + maxX + 1) * tileSize) / 2,
          terrainCenterHeight,
          ((minY + maxY + 1) * tileSize) / 2
        )
      : Vector3.Zero();

    const mapSpan = Math.max(maxX - minX + 1, maxY - minY + 1);
    const radius = tiles.length
      ? usesWorldCoordinates
        ? Math.max(5, mapSpan * 0.85)
        : Math.max(450, mapSpan * tileSize * 0.85)
      : 95;

    const camera = new ArcRotateCamera(
      "editor-camera",
      -Math.PI / 2,
      Math.PI / 3,
      radius,
      target,
      scene
    );

    camera.lowerRadiusLimit =
      usesWorldCoordinates ? 0.5 : 5;
    camera.upperRadiusLimit =
      Math.max(
        usesWorldCoordinates ? 50 : 900,
        radius * 4
      );

    const hasNewCameraAction =
      cameraAction !== undefined &&
      cameraAction.token !==
        lastCameraActionTokenRef.current;

    if (
      !hasNewCameraAction &&
      cameraStateRef.current
    ) {
      const state =
        cameraStateRef.current;

      camera.alpha = state.alpha;
      camera.beta = state.beta;
      camera.radius =
        Math.min(
          camera.upperRadiusLimit ??
            state.radius,
          Math.max(
            camera.lowerRadiusLimit ??
              state.radius,
            state.radius
          )
        );
      camera.setTarget(
        new Vector3(
          state.target.x,
          state.target.y,
          state.target.z
        )
      );
    } else if (
      cameraAction?.type ===
        "perspective"
    ) {
      camera.alpha =
        -Math.PI / 2;
      camera.beta =
        Math.PI / 3;
    } else if (
      cameraAction?.type === "top"
    ) {
      camera.alpha =
        -Math.PI / 2;
      camera.beta =
        0.01;
    } else if (
      cameraAction?.type === "focus"
    ) {
      if (selectedObject) {
        camera.setTarget(
          getObjectWorldPosition(
            selectedObject,
            objectGeometryByPath[
              selectedObject
                .sceneryObjectPath
            ],
            tiles
          )
        );
        camera.radius =
          usesWorldCoordinates
            ? 4
            : 55;
      } else if (selectedSpline) {
        camera.setTarget(
          getSplineFrame(
            selectedSpline,
            selectedSpline.length / 2
          ).center
        );
        camera.radius =
          usesWorldCoordinates
            ? 4
            : Math.max(
                40,
                Math.min(
                  180,
                  selectedSpline.length *
                    1.5
                )
              );
      } else if (
        pendingPlacement
      ) {
        camera.setTarget(
          new Vector3(
            pendingPlacement.tileX *
              300 +
              pendingPlacement.x,
            pendingPlacement.z,
            pendingPlacement.tileY *
              300 +
              pendingPlacement.y
          )
        );
        camera.radius = 45;
      } else if (
        pendingSplinePlacement
      ) {
        const previewSpline:
          OmsiPlacedSpline = {
            tileX:
              pendingSplinePlacement
                .targetTileX,
            tileY:
              pendingSplinePlacement
                .targetTileY,
            headerValue: "",
            splinePath:
              splinePlacementTemplate
                ?.splinePath ?? "",
            splineId: -1,
            sourceSectionOrdinal: -1,
            previousSplineId: -1,
            nextSplineId: -1,
            x:
              pendingSplinePlacement.x,
            y:
              pendingSplinePlacement.y,
            z:
              pendingSplinePlacement.z,
            rotation:
              pendingSplinePlacement
                .rotation,
            length:
              pendingSplinePlacement
                .length,
            radius:
              pendingSplinePlacement
                .radius,
            gradientStart:
              pendingSplinePlacement
                .gradientStart,
            gradientEnd:
              pendingSplinePlacement
                .gradientEnd,
            isHeightSpline:
              splinePlacementTemplate
                ?.isHeightSpline ??
              false
          };

        camera.setTarget(
          getSplineFrame(
            previewSpline,
            previewSpline.length / 2
          ).center
        );

        camera.radius =
          Math.max(
            40,
            Math.min(
              180,
              Math.max(
                20,
                previewSpline.length
              ) *
                1.5
            )
          );
      }
    } else if (
      cameraAction?.type === "tile" &&
      Number.isFinite(
        cameraAction.tileX
      ) &&
      Number.isFinite(
        cameraAction.tileY
      ) &&
      !usesWorldCoordinates
    ) {
      const tileX =
        cameraAction.tileX!;
      const tileY =
        cameraAction.tileY!;

      const centerX =
        tileX * 300 +
        150;
      const centerZ =
        tileY * 300 +
        150;

      const centerHeight =
        getTerrainHeightAtWorldPoint(
          tiles,
          centerX,
          centerZ
        );

      camera.setTarget(
        new Vector3(
          centerX,
          centerHeight,
          centerZ
        )
      );

      const previousRadius =
        cameraStateRef.current
          ?.radius;

      camera.radius =
        Math.min(
          camera.upperRadiusLimit ??
            radius,
          Math.max(
            90,
            previousRadius ??
              240
          )
        );
    } else if (
      cameraAction?.type === "fit"
    ) {
      camera.setTarget(target);
      camera.radius = radius;
    }

    if (cameraAction) {
      lastCameraActionTokenRef.current =
        cameraAction.token;
    }

    createOmsiSky(
      scene,
      skyTextureAsset,
      camera.upperRadiusLimit ??
        radius * 4
    );

    const light = new HemisphericLight("editor-light", new Vector3(0, 1, 0), scene);
    light.intensity = 0.9;

    const objectLodInstances:
      ObjectLodInstance[] = [];

    let selectionMarker: ReturnType<typeof MeshBuilder.CreateLineSystem> | undefined;
    let splineSelectionMarker: ReturnType<typeof MeshBuilder.CreateLineSystem> | undefined;

    const showSelection = (placedObject: OmsiPlacedObject | undefined) => {
      selectionMarker?.dispose();
      selectionMarker = undefined;

      if (!placedObject) {
        return;
      }

      selectionMarker = MeshBuilder.CreateLineSystem(
        "omsi-selected-object",
        {
          lines:
            createSelectedMarkerLines(
              placedObject,
              objectGeometryByPath[
                placedObject
                  .sceneryObjectPath
              ],
              tiles
            )
        },
        scene
      );
      selectionMarker.color =
        new Color3(
          0.16,
          0.78,
          1
        );
      selectionMarker.isPickable = false;
      selectionMarker.renderingGroupId = 3;
    };

    const showSplineSelection = (
      placedSpline:
        | OmsiPlacedSpline
        | undefined
    ) => {
      splineSelectionMarker?.dispose();
      splineSelectionMarker =
        undefined;

      if (!placedSpline) {
        return;
      }

      const axis =
        getSplineAxisLine(
          placedSpline
        );

      if (axis.length < 2) {
        return;
      }

      const lifted =
        axis.map((point) =>
          point.add(
            new Vector3(
              0,
              0.38,
              0
            )
          )
        );

      const start =
        lifted[0];
      const end =
        lifted[
          lifted.length - 1
        ];
      const markerRadius = 2.2;

      splineSelectionMarker =
        MeshBuilder.CreateLineSystem(
          "omsi-selected-spline",
          {
            lines: [
              lifted,
              [
                new Vector3(
                  start.x -
                    markerRadius,
                  start.y,
                  start.z
                ),
                new Vector3(
                  start.x +
                    markerRadius,
                  start.y,
                  start.z
                )
              ],
              [
                new Vector3(
                  start.x,
                  start.y,
                  start.z -
                    markerRadius
                ),
                new Vector3(
                  start.x,
                  start.y,
                  start.z +
                    markerRadius
                )
              ],
              [
                new Vector3(
                  end.x -
                    markerRadius,
                  end.y,
                  end.z
                ),
                new Vector3(
                  end.x +
                    markerRadius,
                  end.y,
                  end.z
                )
              ],
              [
                new Vector3(
                  end.x,
                  end.y,
                  end.z -
                    markerRadius
                ),
                new Vector3(
                  end.x,
                  end.y,
                  end.z +
                    markerRadius
                )
              ]
            ]
          },
          scene
        );

      splineSelectionMarker.color =
        new Color3(
          0.16,
          0.78,
          1
        );
      splineSelectionMarker.isPickable =
        false;
      splineSelectionMarker
        .renderingGroupId = 3;
    };

    if (tiles.length) {
      if (
        showGrid ||
        showTerrain
      ) {
        createTileSurface(
          scene,
          tiles,
          tileSize,
          showGrid,
          showTerrain &&
            !usesWorldCoordinates,
          terrainMainTextureAsset,
          terrainMainTextureRepeating,
          terrainOverlays
        );

        if (
          referenceOverlay &&
          !usesWorldCoordinates
        ) {
          createReferenceOverlay(
            scene,
            tiles,
            referenceOverlay
          );
        }
      }

      const existingLines = showGrid
        ? tiles
        .filter(
          (tile) =>
            !tile.detailsLoaded ||
            tile.fileExists
        )
        .map((tile) => createTileOutline(tile, tileSize))
        : [];

      if (existingLines.length) {
        const existingGrid = MeshBuilder.CreateLineSystem(
          "omsi-tile-grid",
          { lines: existingLines },
          scene
        );
        existingGrid.color = new Color3(0.55, 0.68, 0.82);
        existingGrid.isPickable = false;
      }

      const missingLines = showGrid
        ? tiles
        .filter(
          (tile) =>
            tile.detailsLoaded &&
            !tile.fileExists
        )
        .map((tile) => createTileOutline(tile, tileSize))
        : [];

      if (missingLines.length) {
        const missingGrid = MeshBuilder.CreateLineSystem(
          "omsi-missing-tile-grid",
          { lines: missingLines },
          scene
        );
        missingGrid.color = new Color3(0.9, 0.35, 0.35);
        missingGrid.isPickable = false;
      }

      if (
        showGrid &&
        activeTile &&
        !usesWorldCoordinates
      ) {
        const activeGrid =
          MeshBuilder.CreateLines(
            "omsi-active-tile",
            {
              points:
                createActiveTileOutline(
                  activeTile,
                  tileSize
                )
            },
            scene
          );

        activeGrid.color =
          new Color3(
            0.2,
            0.95,
            0.65
          );

        activeGrid.isPickable =
          false;
      }

      if (
        showSplines &&
        !usesWorldCoordinates &&
        splines.length
      ) {
        if (showSplineProfiles) {
          createMapSplineProfiles(
            scene,
            splines,
            activeTile,
            selectedSpline,
            splineProfilesByPath,
            textureAssetsByKey,
            showAllSplineProfiles
          );
        }

        splines.forEach(
          (
            placedSpline,
            splineIndex
          ) => {
            if (
              editorTool !== "select" &&
              isSameSpline(
                placedSpline,
                selectedSpline
              )
            ) {
              return;
            }

            const points =
              getSplineAxisLine(
                placedSpline
              );

            if (points.length < 2) {
              return;
            }

            const splineAxis =
              MeshBuilder.CreateLines(
                `omsi-spline-axis-${splineIndex}`,
                {
                  points
                },
                scene
              );

            splineAxis.color =
              isSameSpline(
                placedSpline,
                selectedSpline
              )
                ? new Color3(
                    0.25,
                    1,
                    0.65
                  )
                : new Color3(
                    0.25,
                    0.62,
                    1
                  );

            splineAxis.isPickable =
              true;

            splineAxis.intersectionThreshold =
              5;

            splineAxis.metadata = {
              mapStudioKind:
                "spline",
              splineIndex,
              placedSpline
            };
          }
        );
      }

      if (
        showObjects &&
        !usesWorldCoordinates &&
        objects.length
      ) {
        createMapObjectGeometry(
          scene,
          objects,
          objectGeometryByPath,
          textureAssetsByKey,
          nightPreviewEnabled,
          objectLodInstances,
          tiles
        );

        const markerObjects =
          objects.filter(
            (placedObject) =>
              !hasRenderableObjectVisual(
                placedObject,
                objectGeometryByPath[
                  placedObject
                    .sceneryObjectPath
                ],
                textureAssetsByKey
              )
          );

        if (markerObjects.length) {
          const protectedMarkers =
            markerObjects.filter(
              (placedObject) =>
                hasProtectedGeometry(
                  objectGeometryByPath[
                    placedObject
                      .sceneryObjectPath
                  ]
                )
            );

          const missingMarkers =
            markerObjects.filter(
              (placedObject) =>
                !hasProtectedGeometry(
                  objectGeometryByPath[
                    placedObject
                      .sceneryObjectPath
                  ]
                )
            );

          const createPickableObjectMarkers = (
            items: OmsiPlacedObject[],
            prefix: string,
            color: Color3
          ) => {
            for (const item of items) {
              const marker =
                MeshBuilder.CreateLineSystem(
                  `${prefix}-${item.objectId}`,
                  {
                    lines:
                      createObjectMarkerLines(
                        [item],
                        objectGeometryByPath,
                        tiles
                      )
                  },
                  scene
                );

              marker.color = color;
              marker.isPickable = true;
              marker.intersectionThreshold =
                6;
              marker.metadata = {
                ...(marker.metadata ?? {}),
                mapStudioKind: "object",
                placedObject: item,
                mapStudioSource:
                  "fallback-marker",
                mapStudioOrigin:
                  item.sceneryObjectPath
              };
            }
          };

          createPickableObjectMarkers(
            protectedMarkers,
            "omsi-protected-object-marker",
            new Color3(
              0.48,
              0.66,
              0.82
            )
          );

          createPickableObjectMarkers(
            missingMarkers,
            "omsi-missing-object-marker",
            new Color3(
              0.95,
              0.78,
              0.38
            )
          );
        }
      }
    } else {
      const ground = MeshBuilder.CreateGround(
        "editor-grid",
        { width: 300, height: 300, subdivisions: 30 },
        scene
      );

      const material = new StandardMaterial("editor-grid-material", scene);
      material.diffuseColor = new Color3(0.08, 0.1, 0.12);
      material.specularColor = Color3.Black();
      material.wireframe = true;
      ground.material = material;
      ground.isPickable = false;
    }

    let placementPreview:
      OmsiPlacedObject | undefined;

    let placementPreviewRoot:
      TransformNode | undefined;

    if (
      placementAssetPath &&
      pendingPlacement &&
      !usesWorldCoordinates
    ) {
      placementPreview = {
        tileX:
          pendingPlacement.tileX,
        tileY:
          pendingPlacement.tileY,
        headerValue: "",
        sceneryObjectPath:
          placementAssetPath,
        objectId: -1,
        sourceSectionOrdinal: -1,
        x: pendingPlacement.x,
        y: pendingPlacement.y,
        z: pendingPlacement.z,
        rotation:
          pendingPlacement.rotation,
        pitch:
          pendingPlacement.pitch,
        bank:
          pendingPlacement.bank
      };

      if (
        showObjects &&
        placementGeometry &&
        hasRenderableGeometry(
          placementGeometry
        )
      ) {
        placementPreviewRoot =
          createSelectedGeometry(
            scene,
            placementPreview,
            placementGeometry,
            textureAssetsByKey,
            nightPreviewEnabled,
            objectLodInstances,
            tiles
          );

        for (const mesh of
          placementPreviewRoot
            .getChildMeshes()) {
          mesh.visibility = 0.72;
          mesh.isPickable = false;
        }
      }

      const placementMarker =
        MeshBuilder.CreateLineSystem(
          "omsi-new-object-preview",
          {
            lines:
              createSelectedMarkerLines(
                placementPreview,
                placementGeometry,
                tiles
              )
          },
          scene
        );

      placementMarker.color =
        new Color3(
          1,
          0.55,
          0.15
        );

      placementMarker.isPickable =
        false;
    }

    if (
      placementAssetPath &&
      placementGeometry &&
      pendingPlacementBatch &&
      pendingPlacementBatch.length > 0 &&
      !usesWorldCoordinates
    ) {
      for (
        const [
          batchIndex,
          batchPlacement
        ] of pendingPlacementBatch
          .slice(0, 96)
          .entries()
      ) {
        const previewObject:
          OmsiPlacedObject = {
            tileX:
              batchPlacement.tileX,
            tileY:
              batchPlacement.tileY,
            headerValue: "",
            sceneryObjectPath:
              placementAssetPath,
            objectId:
              -1000 - batchIndex,
            sourceSectionOrdinal: -1,
            x: batchPlacement.x,
            y: batchPlacement.y,
            z: batchPlacement.z,
            rotation:
              batchPlacement.rotation,
            pitch:
              batchPlacement.pitch,
            bank:
              batchPlacement.bank
          };

        if (
          showObjects &&
          hasRenderableGeometry(
            placementGeometry
          )
        ) {
          const batchRoot =
            createSelectedGeometry(
              scene,
              previewObject,
              placementGeometry,
              textureAssetsByKey,
              nightPreviewEnabled,
              objectLodInstances,
              tiles
            );

          for (const mesh of
            batchRoot.getChildMeshes()) {
            mesh.visibility = 0.38;
            mesh.isPickable = false;
          }
        }

        const batchMarker =
          MeshBuilder.CreateLineSystem(
            `omsi-batch-object-preview-${batchIndex}`,
            {
              lines:
                createSelectedMarkerLines(
                  previewObject,
                  placementGeometry,
                  tiles
                )
            },
            scene
          );

        batchMarker.color =
          new Color3(
            0.2,
            0.8,
            0.45
          );
        batchMarker.isPickable =
          false;
      }
    }

    let splinePlacementPreview:
      OmsiPlacedSpline | undefined;

    let splinePlacementPreviewRoot:
      TransformNode | undefined;

    if (
      splinePlacementTemplate &&
      pendingSplinePlacement &&
      !usesWorldCoordinates
    ) {
      splinePlacementPreview = {
        ...splinePlacementTemplate,
        tileX:
          pendingSplinePlacement
            .targetTileX,
        tileY:
          pendingSplinePlacement
            .targetTileY,
        splineId: -1,
        sourceSectionOrdinal: -1,
        previousSplineId: -1,
        nextSplineId: -1,
        x: pendingSplinePlacement.x,
        y: pendingSplinePlacement.y,
        z: pendingSplinePlacement.z,
        rotation:
          pendingSplinePlacement
            .rotation,
        length:
          pendingSplinePlacement
            .length,
        radius:
          pendingSplinePlacement
            .radius,
        gradientStart:
          pendingSplinePlacement
            .gradientStart,
        gradientEnd:
          pendingSplinePlacement
            .gradientEnd
      };

      splinePlacementPreviewRoot =
        new TransformNode(
          "omsi-new-spline-preview-root",
          scene
        );

      splinePlacementPreviewRoot.position.set(
        pendingSplinePlacement
          .targetTileX *
          300 +
          pendingSplinePlacement.x,
        pendingSplinePlacement.z,
        pendingSplinePlacement
          .targetTileY *
          300 +
          pendingSplinePlacement.y
      );

      const localPreview:
        OmsiPlacedSpline = {
          ...splinePlacementPreview,
          tileX: 0,
          tileY: 0,
          x: 0,
          y: 0,
          z: 0
        };

      const points =
        getSplineAxisLine(
          localPreview
        );

      if (points.length >= 2) {
        const axis =
          MeshBuilder.CreateLines(
            "omsi-new-spline-preview",
            { points },
            scene
          );

        axis.color =
          new Color3(
            1,
            0.55,
            0.15
          );

        axis.isPickable = false;
        axis.parent =
          splinePlacementPreviewRoot;
      }

      if (splinePlacementProfile) {
        createSelectedSplineProfile(
          scene,
          localPreview,
          splinePlacementProfile,
          textureAssetsByKey,
          splinePlacementPreviewRoot,
          "new-spline-preview-profile"
        );

        for (const mesh of
          splinePlacementPreviewRoot
            .getChildMeshes()) {
          mesh.visibility = 0.78;
          mesh.isPickable = false;
        }
      }
    }

    if (
      roadCurveControl &&
      onRoadCurveOffsetChange &&
      !usesWorldCoordinates
    ) {
      const startX =
        roadCurveControl.start
          .targetTileX *
          300 +
        roadCurveControl.start.x;
      const startZ =
        roadCurveControl.start
          .targetTileY *
          300 +
        roadCurveControl.start.y;
      const endX =
        roadCurveControl.end
          .targetTileX *
          300 +
        roadCurveControl.end.x;
      const endZ =
        roadCurveControl.end
          .targetTileY *
          300 +
        roadCurveControl.end.y;
      const deltaX =
        endX - startX;
      const deltaZ =
        endZ - startZ;
      const chord =
        Math.hypot(
          deltaX,
          deltaZ
        );

      if (chord > 0.001) {
        const middleX =
          (startX + endX) / 2;
        const middleZ =
          (startZ + endZ) / 2;
        const normalX =
          -deltaZ / chord;
        const normalZ =
          deltaX / chord;
        const handleX =
          middleX +
          normalX *
            roadCurveControl.offset;
        const handleZ =
          middleZ +
          normalZ *
            roadCurveControl.offset;
        const handleY =
          getTerrainHeightAtWorldPoint(
            tiles,
            handleX,
            handleZ
          ) + 1.2;

        const guide =
          MeshBuilder.CreateLines(
            "road-curve-control-guide",
            {
              points: [
                new Vector3(
                  middleX,
                  getTerrainHeightAtWorldPoint(
                    tiles,
                    middleX,
                    middleZ
                  ) + 0.35,
                  middleZ
                ),
                new Vector3(
                  handleX,
                  handleY,
                  handleZ
                )
              ]
            },
            scene
          );

        guide.color =
          new Color3(
            0.2,
            0.82,
            1
          );
        guide.isPickable = false;

        const handle =
          MeshBuilder.CreateSphere(
            "road-curve-control-handle",
            {
              diameter: 2.4,
              segments: 12
            },
            scene
          );

        handle.position.set(
          handleX,
          handleY,
          handleZ
        );

        const handleMaterial =
          new StandardMaterial(
            "road-curve-control-handle-material",
            scene
          );

        handleMaterial.diffuseColor =
          new Color3(
            0.15,
            0.65,
            0.95
          );
        handleMaterial.emissiveColor =
          new Color3(
            0.08,
            0.34,
            0.52
          );
        handleMaterial.specularColor =
          Color3.Black();

        handle.material =
          handleMaterial;
        handle.isPickable = true;
        handle.metadata = {
          mapStudioKind:
            "roadControl"
        };
      }
    }

    if (
      roadCurveControl &&
      onRoadControlPointChange &&
      !usesWorldCoordinates
    ) {
      const endpointDefinitions:
        Array<{
          control: "start" | "end";
          point:
            typeof roadCurveControl.start;
          color: Color3;
          emissive: Color3;
        }> = [
          {
            control: "start",
            point:
              roadCurveControl.start,
            color:
              new Color3(
                0.2,
                0.9,
                0.45
              ),
            emissive:
              new Color3(
                0.08,
                0.38,
                0.18
              )
          },
          {
            control: "end",
            point:
              roadCurveControl.end,
            color:
              new Color3(
                0.95,
                0.38,
                0.3
              ),
            emissive:
              new Color3(
                0.4,
                0.12,
                0.08
              )
          }
        ];

      for (const definition of
        endpointDefinitions) {
        const worldX =
          definition.point
            .targetTileX *
            300 +
          definition.point.x;
        const worldZ =
          definition.point
            .targetTileY *
            300 +
          definition.point.y;
        const worldY =
          getTerrainHeightAtWorldPoint(
            tiles,
            worldX,
            worldZ
          ) + 0.95;

        const handle =
          MeshBuilder.CreateSphere(
            `road-${definition.control}-control-handle`,
            {
              diameter: 2.15,
              segments: 12
            },
            scene
          );

        handle.position.set(
          worldX,
          worldY,
          worldZ
        );

        const material =
          new StandardMaterial(
            `road-${definition.control}-control-material`,
            scene
          );

        material.diffuseColor =
          definition.color;
        material.emissiveColor =
          definition.emissive;
        material.specularColor =
          Color3.Black();

        handle.material = material;
        handle.isPickable = true;
        handle.metadata = {
          mapStudioKind:
            "roadEndpointControl",
          control:
            definition.control
        };
      }
    }

    if (
      showSplines &&
      !usesWorldCoordinates &&
      selectedSpline &&
      selectedSplineProfile &&
      editorTool === "select"
    ) {
      createSelectedSplineProfile(
        scene,
        selectedSpline,
        selectedSplineProfile,
        textureAssetsByKey
      );
    }

    if (
      showObjects &&
      editorTool === "select" &&
      !usesWorldCoordinates &&
      selectedObject &&
      selectedGeometry &&
      !hasRenderableObjectVisual(
        selectedObject,
        objectGeometryByPath[
          selectedObject
            .sceneryObjectPath
        ],
        textureAssetsByKey
      )
    ) {
      createSelectedGeometry(
        scene,
        selectedObject,
        selectedGeometry,
        textureAssetsByKey,
        nightPreviewEnabled,
        objectLodInstances,
        tiles
      );
    }

    showSelection(
      showObjects
        ? selectedObject
        : undefined
    );

    showSplineSelection(
      showSplines
        ? selectedSpline
        : undefined
    );

    let editRoot:
      TransformNode | undefined;

    let editingSpline = false;

    if (
      !placementAssetPath &&
      !usesWorldCoordinates &&
      selectedObject &&
      editorTool !== "select"
    ) {
      if (
        selectedGeometry &&
        hasRenderableObjectVisual(
          selectedObject,
          selectedGeometry,
          textureAssetsByKey
        )
      ) {
        editRoot =
          createSelectedGeometry(
            scene,
            selectedObject,
            selectedGeometry,
            textureAssetsByKey,
            nightPreviewEnabled,
            objectLodInstances,
            tiles
          );
      } else {
        editRoot =
          new TransformNode(
            "preview-edit-anchor",
            scene
          );

        configureObjectRoot(
          editRoot,
          selectedObject,
          selectedGeometry ??
            objectGeometryByPath[
              selectedObject
                .sceneryObjectPath
            ],
          tiles
        );
      }
    } else if (
      !placementAssetPath &&
      !usesWorldCoordinates &&
      showSplines &&
      selectedSpline &&
      editorTool !== "select"
    ) {
      editRoot =
        createSplineEditRoot(
          scene,
          selectedSpline,
          selectedSplineProfile,
          textureAssetsByKey
        );

      editingSpline = true;
    }

    let gizmoManager:
      GizmoManager | undefined;

    if (editRoot) {
      gizmoManager =
        new GizmoManager(scene);

      gizmoManager
        .usePointerToAttachGizmos =
        false;

      gizmoManager
        .positionGizmoEnabled =
        editorTool === "move";

      gizmoManager
        .rotationGizmoEnabled =
        editorTool === "rotate";

      if (
        gizmoManager.gizmos
          .positionGizmo
      ) {
        gizmoManager.gizmos
          .positionGizmo
          .snapDistance =
          snapEnabled
            ? moveSnap
            : 0;
      }

      if (
        gizmoManager.gizmos
          .rotationGizmo
      ) {
        const rotationGizmo =
          gizmoManager.gizmos
            .rotationGizmo;

        rotationGizmo
          .snapDistance =
          snapEnabled
            ? rotationSnap *
              degreesToRadians
            : 0;

        if (editingSpline) {
          rotationGizmo
            .xGizmo
            .isEnabled = false;
          rotationGizmo
            .zGizmo
            .isEnabled = false;
        }
      }

      gizmoManager.attachToNode(
        editRoot
      );

      const commitPreview = () => {
        if (!editRoot) {
          return;
        }

        const euler =
          editRoot
            .rotationQuaternion
            ?.toEulerAngles() ??
          editRoot.rotation;

        if (selectedObject) {
          onPreviewObjectTransform({
            ...selectedObject,
            x:
              editRoot.position.x -
              selectedObject.tileX *
                300,
            y:
              editRoot.position.z -
              selectedObject.tileY *
                300,
            z:
              editRoot.position.y -
              getObjectTerrainOffset(
                selectedObject,
                selectedGeometry ??
                  objectGeometryByPath[
                    selectedObject
                      .sceneryObjectPath
                  ],
                tiles
              ),
            rotation:
              euler.y /
              degreesToRadians,
            bank:
              euler.z /
              degreesToRadians,
            pitch:
              euler.x /
              degreesToRadians
          });

          return;
        }

        if (selectedSpline) {
          onPreviewSplineTransform({
            ...selectedSpline,
            x:
              editRoot.position.x -
              selectedSpline.tileX *
                300,
            y:
              editRoot.position.z -
              selectedSpline.tileY *
                300,
            z: editRoot.position.y,
            rotation:
              euler.y /
              degreesToRadians
          });
        }
      };

      gizmoManager.gizmos
        .positionGizmo
        ?.onDragEndObservable
        .add(commitPreview);

      gizmoManager.gizmos
        .rotationGizmo
        ?.onDragEndObservable
        .add(commitPreview);
    }

    let pointerStart:
      | { x: number; y: number }
      | undefined;

    let pointerDownHandledSelection =
      false;

    const getMapItemClickKey = (
      kind: "object" | "spline",
      item:
        | OmsiPlacedObject
        | OmsiPlacedSpline
    ) =>
      kind === "object"
        ? [
            "object",
            (item as OmsiPlacedObject).tileX,
            (item as OmsiPlacedObject).tileY,
            (item as OmsiPlacedObject).objectId,
            (item as OmsiPlacedObject)
              .sourceSectionOrdinal
          ].join(":")
        : [
            "spline",
            (item as OmsiPlacedSpline).tileX,
            (item as OmsiPlacedSpline).tileY,
            (item as OmsiPlacedSpline).splineId,
            (item as OmsiPlacedSpline)
              .sourceSectionOrdinal
          ].join(":");

    const isRepeatedMapItemClick = (
      kind: "object" | "spline",
      item:
        | OmsiPlacedObject
        | OmsiPlacedSpline
    ) => {
      const now = performance.now();
      const key =
        getMapItemClickKey(
          kind,
          item
        );

      const repeated =
        lastMapItemClickRef.current
          ?.kind === kind &&
        lastMapItemClickRef.current
          ?.key === key &&
        now -
          lastMapItemClickRef.current.at <=
          500;

      lastMapItemClickRef.current = {
        kind,
        key,
        at: now
      };

      return repeated;
    };

    const focusPlacedObject = (
      placedObject: OmsiPlacedObject
    ) => {
      camera.setTarget(
        getObjectWorldPosition(
          placedObject,
          objectGeometryByPath[
            placedObject
              .sceneryObjectPath
          ],
          tiles
        )
      );

      camera.radius =
        clampCameraRadius(
          usesWorldCoordinates
            ? 4
            : 55
        );
    };

    const focusPlacedSpline = (
      placedSpline: OmsiPlacedSpline
    ) => {
      camera.setTarget(
        getSplineFrame(
          placedSpline,
          placedSpline.length / 2
        ).center
      );

      camera.radius =
        clampCameraRadius(
          usesWorldCoordinates
            ? 4
            : Math.max(
                40,
                Math.min(
                  180,
                  placedSpline.length *
                    1.5
                )
              )
        );
    };

    let navigationPointer:
      | {
          pointerId: number;
          button: number;
          lastX: number;
          lastY: number;
        }
      | undefined;

    let resolveSelectionHover:
      | ((
          event: PointerEvent
        ) => boolean)
      | undefined;

    let lastSelectionHoverAt = 0;
    let selectionHoverActive = false;
    let hoveredSelectionMesh:
      | Mesh
      | undefined;

    const clearSelectionHover = () => {
      if (
        hoveredSelectionMesh &&
        !hoveredSelectionMesh.isDisposed()
      ) {
        hoveredSelectionMesh.renderOutline =
          false;
      }

      hoveredSelectionMesh = undefined;
    };

    const setSelectionHoverMesh = (
      mesh: Mesh | undefined
    ) => {
      if (hoveredSelectionMesh === mesh) {
        return;
      }

      clearSelectionHover();

      if (!mesh || mesh.isDisposed()) {
        return;
      }

      hoveredSelectionMesh = mesh;
      mesh.renderOutline = true;
      mesh.outlineColor =
        new Color3(0.2, 0.62, 1);
      mesh.outlineWidth = 0.03;
    };

    const clampCameraRadius = (
      nextRadius: number
    ) =>
      Math.min(
        camera.upperRadiusLimit ??
          nextRadius,
        Math.max(
          camera.lowerRadiusLimit ??
            nextRadius,
          nextRadius
        )
      );

    const getHorizontalCameraAxes = () => ({
      right: new Vector3(
        -Math.sin(camera.alpha),
        0,
        Math.cos(camera.alpha)
      ),
      forward: new Vector3(
        -Math.cos(camera.alpha),
        0,
        -Math.sin(camera.alpha)
      )
    });

    const syncActiveTileToTarget = () => {
      if (
        usesWorldCoordinates ||
        !onActiveTileChange ||
        tiles.length === 0
      ) {
        return;
      }

      const tileX =
        Math.floor(
          camera.target.x / 300
        );
      const tileY =
        Math.floor(
          camera.target.z / 300
        );

      if (
        tiles.some(
          (tile) =>
            tile.x === tileX &&
            tile.y === tileY
        )
      ) {
        onActiveTileChange({
          x: tileX,
          y: tileY
        });
      }
    };

    const panCamera = (
      horizontalPixels: number,
      verticalPixels: number,
      multiplier = 1
    ) => {
      const scale =
        Math.max(
          usesWorldCoordinates
            ? 0.002
            : 0.12,
          camera.radius *
            (usesWorldCoordinates
              ? 0.0008
              : 0.0015)
        ) *
        multiplier;

      const { right, forward } =
        getHorizontalCameraAxes();

      const nextTarget =
        camera.target
          .add(
            right.scale(
              -horizontalPixels * scale
            )
          )
          .add(
            forward.scale(
              verticalPixels * scale
            )
          );

      camera.setTarget(nextTarget);
    };

    const handleNavigationPointerDown = (
      event: PointerEvent
    ) => {
      if (
        event.button !== 1 &&
        event.button !== 2
      ) {
        return;
      }

      event.preventDefault();
      canvas.focus({
        preventScroll: true
      });

      navigationPointer = {
        pointerId: event.pointerId,
        button: event.button,
        lastX: event.clientX,
        lastY: event.clientY
      };

      canvas.setPointerCapture(
        event.pointerId
      );
    };

    const handlePointerMove = (
      event: PointerEvent
    ) => {
      if (
        roadEndpointDragPointerRef
          .current ===
          event.pointerId
      ) {
        const now =
          performance.now();

        if (
          now -
            roadEndpointLastEmitRef
              .current >=
          40
        ) {
          const point =
            getPlacementPointFromPointer(
              event
            );
          const control =
            roadEndpointDragControlRef
              .current;

          if (
            point &&
            control
          ) {
            roadEndpointLastEmitRef
              .current =
              now;
            onRoadControlPointChange?.(
              control,
              {
                targetTileX:
                  point.tileX,
                targetTileY:
                  point.tileY,
                x: point.x,
                y: point.y
              }
            );
          }
        }

        event.preventDefault();
        return;
      }

      if (
        roadCurveDragPointerRef.current ===
          event.pointerId
      ) {
        const now =
          performance.now();

        if (
          now -
            roadCurveLastEmitRef.current >=
            40
        ) {
          const offset =
            getRoadCurveOffsetFromPointer(
              event
            );

          if (
            offset !== undefined
          ) {
            roadCurveLastEmitRef.current =
              now;
            onRoadCurveOffsetChange?.(
              offset
            );
          }
        }

        event.preventDefault();
        return;
      }

      if (
        roadDragPointerRef.current ===
          event.pointerId
      ) {
        const now =
          performance.now();

        if (
          now -
            roadDragLastEmitRef.current >=
          40
        ) {
          roadDragLastEmitRef.current =
            now;
          emitSplineRoadPoint(event);
        }

        event.preventDefault();
        return;
      }

      if (!navigationPointer) {
        const now =
          performance.now();

        if (
          resolveSelectionHover &&
          now -
            lastSelectionHoverAt >=
            70
        ) {
          lastSelectionHoverAt = now;
          selectionHoverActive =
            resolveSelectionHover(
              event
            );
        }

        if (selectionHoverActive) {
          canvas.style.cursor =
            "pointer";
        } else if (
          updateObjectHoverPreview(
            event
          ) ||
          updateSplineHoverPreview(
            event
          )
        ) {
          canvas.style.cursor =
            "copy";
        } else {
          canvas.style.cursor = "";
        }
      }

      if (
        !navigationPointer ||
        navigationPointer.pointerId !==
          event.pointerId
      ) {
        return;
      }

      const deltaX =
        event.clientX -
        navigationPointer.lastX;
      const deltaY =
        event.clientY -
        navigationPointer.lastY;

      navigationPointer.lastX =
        event.clientX;
      navigationPointer.lastY =
        event.clientY;

      if (
        navigationPointer.button === 2 &&
        !event.shiftKey
      ) {
        camera.alpha -=
          deltaX * 0.005;
        camera.beta =
          Math.min(
            Math.PI / 2 - 0.015,
            Math.max(
              0.01,
              camera.beta -
                deltaY * 0.005
            )
          );
      } else {
        panCamera(
          deltaX,
          deltaY,
          event.shiftKey ? 2.5 : 1
        );
      }
    };

    const finishNavigationPointer = (
      event: PointerEvent
    ) => {
      if (
        !navigationPointer ||
        navigationPointer.pointerId !==
          event.pointerId
      ) {
        return;
      }

      if (
        canvas.hasPointerCapture(
          event.pointerId
        )
      ) {
        canvas.releasePointerCapture(
          event.pointerId
        );
      }

      navigationPointer = undefined;
      syncActiveTileToTarget();
    };

    const handleWheel = (
      event: WheelEvent
    ) => {
      event.preventDefault();

      const factor =
        Math.exp(
          event.deltaY * 0.0012
        );

      camera.radius =
        clampCameraRadius(
          camera.radius * factor
        );
    };

    const handleViewportKeyDown = (
      event: KeyboardEvent
    ) => {
      const tileJump =
        !usesWorldCoordinates &&
        event.ctrlKey;

      const step =
        tileJump
          ? 300
          : Math.max(
              usesWorldCoordinates
                ? 0.25
                : 5,
              camera.radius * 0.025
            ) *
            (event.shiftKey ? 3 : 1);

      const { right, forward } =
        getHorizontalCameraAxes();

      let offset:
        | Vector3
        | undefined;

      const key =
        event.key.toLowerCase();

      if (
        event.key === "ArrowUp" ||
        key === "w"
      ) {
        offset =
          forward.scale(step);
      } else if (
        event.key === "ArrowDown" ||
        key === "s"
      ) {
        offset =
          forward.scale(-step);
      } else if (
        event.key === "ArrowLeft" ||
        key === "a"
      ) {
        offset =
          right.scale(-step);
      } else if (
        event.key === "ArrowRight" ||
        key === "d"
      ) {
        offset =
          right.scale(step);
      } else if (
        event.key === "+" ||
        event.key === "="
      ) {
        camera.radius =
          clampCameraRadius(
            camera.radius * 0.85
          );
        event.preventDefault();
        return;
      } else if (
        event.key === "-" ||
        event.key === "_"
      ) {
        camera.radius =
          clampCameraRadius(
            camera.radius * 1.15
          );
        event.preventDefault();
        return;
      }

      if (offset) {
        event.preventDefault();
        event.stopPropagation();
        camera.setTarget(
          camera.target.add(offset)
        );
        syncActiveTileToTarget();
      }
    };

    const handleContextMenu = (
      event: MouseEvent
    ) => {
      event.preventDefault();
    };

    const buildPickedDiagnostic = (
      mesh: Mesh | null,
      kind: "object" | "spline",
      item:
        | OmsiPlacedObject
        | OmsiPlacedSpline
    ): ViewportDiagnostic => {
      const metadata =
        mesh?.metadata ?? {};

      const uvs =
        mesh?.getVerticesData("uv") ??
        [];

      let minU =
        Number.POSITIVE_INFINITY;
      let maxU =
        Number.NEGATIVE_INFINITY;
      let minV =
        Number.POSITIVE_INFINITY;
      let maxV =
        Number.NEGATIVE_INFINITY;

      for (
        let index = 0;
        index + 1 < uvs.length;
        index += 2
      ) {
        minU = Math.min(
          minU,
          uvs[index]
        );
        maxU = Math.max(
          maxU,
          uvs[index]
        );
        minV = Math.min(
          minV,
          uvs[index + 1]
        );
        maxV = Math.max(
          maxV,
          uvs[index + 1]
        );
      }

      if (mesh) {
        mesh.computeWorldMatrix(true);
      }

      const position =
        mesh?.getAbsolutePosition();

      const material =
        mesh?.material;

      const textureName =
        metadata
          .mapStudioTextureName ??
        (
          material instanceof
            StandardMaterial
            ? material.diffuseTexture
                ?.name
            : null
        ) ??
        "sem textura";

      const resolvedTexture =
        metadata
          .mapStudioResolvedTexture ??
        "caminho não exposto";

      const itemId =
        kind === "object"
          ? (item as OmsiPlacedObject)
              .objectId
          : (item as OmsiPlacedSpline)
              .splineId;

      const fallbackOrigin =
        kind === "object"
          ? (item as OmsiPlacedObject)
              .sceneryObjectPath
          : (item as OmsiPlacedSpline)
              .splinePath;

      return {
        item:
          `${kind === "object" ? "Objeto" : "Spline"} #${itemId}`,
        mesh:
          mesh?.name ??
          "fallback geométrico",
        material:
          material?.name ??
          "sem material",
        texture:
          `${textureName} -> ${resolvedTexture}`,
        uv:
          Number.isFinite(minU) &&
          Number.isFinite(maxU) &&
          Number.isFinite(minV) &&
          Number.isFinite(maxV)
            ? `U ${minU.toFixed(4)}..${maxU.toFixed(4)} · V ${minV.toFixed(4)}..${maxV.toFixed(4)}`
            : "sem UV",
        position:
          position
            ? `X ${position.x.toFixed(3)} · Y ${position.y.toFixed(3)} · Z ${position.z.toFixed(3)}`
            : "posição via item",
        renderLift:
          typeof metadata
            .mapStudioRenderLift ===
            "number"
            ? metadata
                .mapStudioRenderLift
                .toFixed(3)
            : "0.000",
        origin:
          [
            metadata.mapStudioOrigin ??
              fallbackOrigin,
            metadata.mapStudioRenderType
              ? `rendertype=${metadata.mapStudioRenderType}`
              : null
          ]
            .filter(Boolean)
            .join(" · ")
      };
    };

    const objectSelectionEnabled =
      showObjects &&
      (
        selectionMode === "all" ||
        selectionMode === "object"
      );

    const splineSelectionEnabled =
      showSplines &&
      (
        selectionMode === "all" ||
        selectionMode === "spline"
      );

    const getPickedMapItem = (
      event: PointerEvent
    ) => {
      if (
        !objectSelectionEnabled &&
        !splineSelectionEnabled
      ) {
        return undefined;
      }

      const rect =
        canvas.getBoundingClientRect();

      if (
        rect.width <= 0 ||
        rect.height <= 0
      ) {
        return undefined;
      }

      const pointerX =
        (event.clientX - rect.left) *
        (engine.getRenderWidth() /
          rect.width);

      const pointerY =
        (event.clientY - rect.top) *
        (engine.getRenderHeight() /
          rect.height);

      const picks =
        scene.multiPick(
          pointerX,
          pointerY,
          (mesh) => {
            if (!mesh.isPickable) {
              return false;
            }

            let node: Node | null =
              mesh;

            while (node) {
              const kind =
                node.metadata
                  ?.mapStudioKind;

              if (
                kind === "object" &&
                objectSelectionEnabled
              ) {
                return true;
              }

              if (
                kind === "spline" &&
                splineSelectionEnabled
              ) {
                return true;
              }

              node = node.parent;
            }

            return false;
          },
          camera
        ) ?? [];

      picks.sort(
        (left, right) =>
          (
            Number.isFinite(
              left.distance
            )
              ? left.distance
              : Number.POSITIVE_INFINITY
          ) -
          (
            Number.isFinite(
              right.distance
            )
              ? right.distance
              : Number.POSITIVE_INFINITY
          )
      );

      for (const pick of picks) {
        let node: Node | null =
          pick.pickedMesh;

        while (node) {
          const metadata =
            node.metadata;

          if (
            objectSelectionEnabled &&
            metadata?.mapStudioKind ===
              "object" &&
            metadata.placedObject
          ) {
            const item =
              metadata.placedObject as
                OmsiPlacedObject;

            return {
              kind: "object" as const,
              item,
              pickedMesh:
                pick.pickedMesh instanceof
                  Mesh
                  ? pick.pickedMesh
                  : undefined,
              diagnostic:
                buildPickedDiagnostic(
                  pick.pickedMesh instanceof
                    Mesh
                    ? pick.pickedMesh
                    : null,
                  "object",
                  item
                )
            };
          }

          if (
            splineSelectionEnabled &&
            metadata?.mapStudioKind ===
              "spline" &&
            metadata.placedSpline
          ) {
            const item =
              metadata.placedSpline as
                OmsiPlacedSpline;

            return {
              kind: "spline" as const,
              item,
              pickedMesh:
                pick.pickedMesh instanceof
                  Mesh
                  ? pick.pickedMesh
                  : undefined,
              diagnostic:
                buildPickedDiagnostic(
                  pick.pickedMesh instanceof
                    Mesh
                    ? pick.pickedMesh
                    : null,
                  "spline",
                  item
                )
            };
          }

          node = node.parent;
        }
      }

      // Some OMSI assets do not expose a usable rendered mesh:
      // encrypted/protected O3D, helper-only SCOs, missing geometry,
      // or very thin spline profiles. Keep them selectable by using
      // the real OMSI placement/axis as a geometric fallback.
      const ray =
        scene.createPickingRay(
          pointerX,
          pointerY,
          Matrix.Identity(),
          camera,
          false
        );

      const direction =
        ray.direction
          .normalizeToNew();

      const threshold =
        Math.max(
          2.5,
          Math.min(
            24,
            camera.radius *
              0.006
          )
        );

      let fallback:
        | {
            kind: "object";
            item: OmsiPlacedObject;
            distance: number;
            depth: number;
          }
        | {
            kind: "spline";
            item: OmsiPlacedSpline;
            distance: number;
            depth: number;
          }
        | undefined;

      const considerPoint = (
        point: Vector3,
        candidate:
          | {
              kind: "object";
              item: OmsiPlacedObject;
            }
          | {
              kind: "spline";
              item: OmsiPlacedSpline;
            }
      ) => {
        const offset =
          point.subtract(
            ray.origin
          );

        const depth =
          Vector3.Dot(
            offset,
            direction
          );

        if (depth < 0) {
          return;
        }

        const closest =
          ray.origin.add(
            direction.scale(
              depth
            )
          );

        const distance =
          Vector3.Distance(
            point,
            closest
          );

        if (
          distance > threshold
        ) {
          return;
        }

        if (
          !fallback ||
          distance <
            fallback.distance -
              0.001 ||
          (
            Math.abs(
              distance -
                fallback.distance
            ) <= 0.001 &&
            depth <
              fallback.depth
          )
        ) {
          fallback = {
            ...candidate,
            distance,
            depth
          } as typeof fallback;
        }
      };

      if (objectSelectionEnabled) {
        for (const item of objects) {
          considerPoint(
            getObjectWorldPosition(
              item,
              objectGeometryByPath[
                item.sceneryObjectPath
              ],
              tiles
            ),
            {
              kind: "object",
              item
            }
          );
        }
      }

      if (splineSelectionEnabled) {
        for (const item of splines) {
          const points =
            getSplineAxisLine(
              item
            );

          if (
            points.length === 0
          ) {
            considerPoint(
              getSplineFrame(
                item,
                0
              ).center,
              {
                kind: "spline",
                item
              }
            );
            continue;
          }

          for (const point of points) {
            considerPoint(
              point,
              {
                kind: "spline",
                item
              }
            );
          }
        }
      }

      if (!fallback) {
        return undefined;
      }

      if (
        fallback.kind ===
        "object"
      ) {
        return {
          kind: "object" as const,
          item: fallback.item,
          pickedMesh: undefined,
          diagnostic:
            buildPickedDiagnostic(
              null,
              "object",
              fallback.item
            )
        };
      }

      return {
        kind: "spline" as const,
        item: fallback.item,
        pickedMesh: undefined,
        diagnostic:
          buildPickedDiagnostic(
            null,
            "spline",
            fallback.item
          )
      };
    };

    resolveSelectionHover = (
      event: PointerEvent
    ) => {
      const picked =
        getPickedMapItem(event);

      if (!picked) {
        clearSelectionHover();
        return false;
      }

      const alreadySelected =
        picked.kind === "object"
          ? isSameObject(
              picked.item,
              selectedObject
            )
          : isSameSpline(
              picked.item,
              selectedSpline
            );

      if (alreadySelected) {
        clearSelectionHover();
      } else {
        setSelectionHoverMesh(
          picked.pickedMesh
        );
      }

      return true;
    };

    const selectPickedMapItem = (
      event: PointerEvent
    ) => {
      const picked =
        getPickedMapItem(event);

      if (!picked) {
        return false;
      }

      if (picked.kind === "object") {
        const shouldFocus =
          isRepeatedMapItemClick(
            "object",
            picked.item
          );

        setViewportDiagnostic(
          picked.diagnostic
        );

        onSelectSpline(undefined);
        onSelectObject(
          picked.item
        );

        if (shouldFocus) {
          focusPlacedObject(
            picked.item
          );
        }

        return true;
      }

      const shouldFocus =
        isRepeatedMapItemClick(
          "spline",
          picked.item
        );

      setViewportDiagnostic(
        picked.diagnostic
      );

      onSelectObject(undefined);
      onSelectSpline(
        picked.item
      );

      if (shouldFocus) {
        focusPlacedSpline(
          picked.item
        );
      }

      return true;
    };

    const getPlacementPointFromPointer = (
      event: {
        clientX: number;
        clientY: number;
      }
    ) => {
      if (usesWorldCoordinates) {
        return undefined;
      }

      const rect =
        canvas.getBoundingClientRect();

      const pointerX =
        (event.clientX - rect.left) *
        (
          engine.getRenderWidth() /
          rect.width
        );

      const pointerY =
        (event.clientY - rect.top) *
        (
          engine.getRenderHeight() /
          rect.height
        );

      const ray =
        scene.createPickingRay(
          pointerX,
          pointerY,
          Matrix.Identity(),
          camera,
          false
        );

      const direction =
        ray.direction
          .normalizeToNew();

      if (
        Math.abs(direction.y) <=
        0.000001
      ) {
        return undefined;
      }

      const distanceToGround =
        -ray.origin.y /
        direction.y;

      if (distanceToGround <= 0) {
        return undefined;
      }

      const groundPoint =
        ray.origin.add(
          direction.scale(
            distanceToGround
          )
        );

      const tileX =
        Math.floor(
          groundPoint.x / 300
        );

      const tileY =
        Math.floor(
          groundPoint.z / 300
        );

      const tileExists =
        tiles.some(
          (tile) =>
            tile.x === tileX &&
            tile.y === tileY &&
            (
              !tile.detailsLoaded ||
              tile.fileExists
            )
        );

      if (!tileExists) {
        return undefined;
      }

      const rawX =
        groundPoint.x -
        tileX * 300;
      const rawY =
        groundPoint.z -
        tileY * 300;

      return {
        tileX,
        tileY,
        x:
          snapEnabled &&
          moveSnap > 0
            ? Math.round(
                rawX / moveSnap
              ) * moveSnap
            : rawX,
        y:
          snapEnabled &&
          moveSnap > 0
            ? Math.round(
                rawY / moveSnap
              ) * moveSnap
            : rawY
      };
    };

    const updateObjectHoverPreview = (
      event: PointerEvent
    ) => {
      if (
        !placementAssetPath ||
        !placementGeometry ||
        !pendingPlacement ||
        !placementPreviewRoot ||
        roadDragPointerRef.current !==
          undefined
      ) {
        return false;
      }

      const point =
        getPlacementPointFromPointer(
          event
        );

      if (!point) {
        return false;
      }

      configureObjectRoot(
        placementPreviewRoot,
        {
          ...placementPreview!,
          tileX: point.tileX,
          tileY: point.tileY,
          x: point.x,
          y: point.y
        },
        placementGeometry,
        tiles
      );

      return true;
    };

    const updateSplineHoverPreview = (
      event: PointerEvent
    ) => {
      if (
        roadDragMode ||
        !splinePlacementTemplate ||
        !pendingSplinePlacement ||
        !splinePlacementPreviewRoot
      ) {
        return false;
      }

      const point =
        getPlacementPointFromPointer(
          event
        );

      if (!point) {
        return false;
      }

      splinePlacementPreviewRoot
        .position.set(
          point.tileX * 300 +
            point.x,
          pendingSplinePlacement.z,
          point.tileY * 300 +
            point.y
        );

      return true;
    };

    const emitSplineRoadPoint = (
      event: PointerEvent
    ) => {
      if (
        !splinePlacementTemplate ||
        !onSplinePlacementPoint ||
        usesWorldCoordinates
      ) {
        return false;
      }

      const point =
        getPlacementPointFromPointer(
          event
        );

      if (!point) {
        return false;
      }

      onSplinePlacementPoint({
        targetTileX: point.tileX,
        targetTileY: point.tileY,
        x: point.x,
        y: point.y
      });

      return true;
    };

    const getRoadCurveOffsetFromPointer = (
      event: PointerEvent
    ) => {
      if (!roadCurveControl) {
        return undefined;
      }

      const point =
        getPlacementPointFromPointer(
          event
        );

      if (!point) {
        return undefined;
      }

      const startX =
        roadCurveControl.start
          .targetTileX *
          300 +
        roadCurveControl.start.x;
      const startZ =
        roadCurveControl.start
          .targetTileY *
          300 +
        roadCurveControl.start.y;
      const endX =
        roadCurveControl.end
          .targetTileX *
          300 +
        roadCurveControl.end.x;
      const endZ =
        roadCurveControl.end
          .targetTileY *
          300 +
        roadCurveControl.end.y;
      const deltaX =
        endX - startX;
      const deltaZ =
        endZ - startZ;
      const chord =
        Math.hypot(
          deltaX,
          deltaZ
        );

      if (chord < 0.001) {
        return undefined;
      }

      const middleX =
        (startX + endX) / 2;
      const middleZ =
        (startZ + endZ) / 2;
      const normalX =
        -deltaZ / chord;
      const normalZ =
        deltaX / chord;
      const pointX =
        point.tileX * 300 +
        point.x;
      const pointZ =
        point.tileY * 300 +
        point.y;

      const rawOffset =
        (
          pointX - middleX
        ) *
          normalX +
        (
          pointZ - middleZ
        ) *
          normalZ;

      const maximum =
        chord * 0.49;

      return Math.max(
        -maximum,
        Math.min(
          maximum,
          rawOffset
        )
      );
    };

    const pickRoadEndpointControl = (
      event: PointerEvent
    ):
      | "start"
      | "end"
      | undefined => {
      if (
        !roadCurveControl ||
        !onRoadControlPointChange
      ) {
        return undefined;
      }

      const rect =
        canvas.getBoundingClientRect();

      const pick =
        scene.pick(
          (
            event.clientX -
            rect.left
          ) *
            (
              engine.getRenderWidth() /
              rect.width
            ),
          (
            event.clientY -
            rect.top
          ) *
            (
              engine.getRenderHeight() /
              rect.height
            ),
          (mesh) =>
            mesh.metadata
              ?.mapStudioKind ===
            "roadEndpointControl",
          false,
          camera
        );

      if (!pick?.hit) {
        return undefined;
      }

      const control =
        pick.pickedMesh
          ?.metadata
          ?.control;

      return control === "start" ||
        control === "end"
        ? control
        : undefined;
    };

    const pickRoadCurveControl = (
      event: PointerEvent
    ) => {
      if (
        !roadCurveControl ||
        !onRoadCurveOffsetChange
      ) {
        return false;
      }

      const rect =
        canvas.getBoundingClientRect();

      const pick =
        scene.pick(
          (
            event.clientX -
            rect.left
          ) *
            (
              engine.getRenderWidth() /
              rect.width
            ),
          (
            event.clientY -
            rect.top
          ) *
            (
              engine.getRenderHeight() /
              rect.height
            ),
          (mesh) =>
            mesh.metadata
              ?.mapStudioKind ===
            "roadControl",
          false,
          camera
        );

      return Boolean(pick?.hit);
    };

    const handlePointerDown = (
      event: PointerEvent
    ) => {
      canvas.focus({
        preventScroll: true
      });

      if (event.button !== 0) {
        return;
      }

      pointerStart = {
        x: event.clientX,
        y: event.clientY
      };

      const endpointControl =
        pickRoadEndpointControl(
          event
        );

      if (endpointControl) {
        roadEndpointDragPointerRef
          .current =
          event.pointerId;
        roadEndpointDragControlRef
          .current =
          endpointControl;
        roadEndpointLastEmitRef
          .current =
          performance.now();
        pointerDownHandledSelection =
          false;
        canvas.setPointerCapture(
          event.pointerId
        );
        event.preventDefault();
        return;
      }

      if (
        pickRoadCurveControl(
          event
        )
      ) {
        roadCurveDragPointerRef.current =
          event.pointerId;
        roadCurveLastEmitRef.current =
          performance.now();
        pointerDownHandledSelection =
          false;
        canvas.setPointerCapture(
          event.pointerId
        );
        event.preventDefault();
        return;
      }

      if (
        roadDragMode &&
        splinePlacementTemplate &&
        onSplinePlacementPoint &&
        emitSplineRoadPoint(event)
      ) {
        roadDragPointerRef.current =
          event.pointerId;
        roadDragLastEmitRef.current =
          performance.now();
        pointerDownHandledSelection =
          false;
        canvas.setPointerCapture(
          event.pointerId
        );
        event.preventDefault();
        return;
      }

      pointerDownHandledSelection =
        selectPickedMapItem(
          event
        );
    };

    const handlePointerUp = (event: PointerEvent) => {
      if (
        roadEndpointDragPointerRef
          .current ===
          event.pointerId
      ) {
        const point =
          getPlacementPointFromPointer(
            event
          );
        const control =
          roadEndpointDragControlRef
            .current;

        if (
          point &&
          control
        ) {
          onRoadControlPointChange?.(
            control,
            {
              targetTileX:
                point.tileX,
              targetTileY:
                point.tileY,
              x: point.x,
              y: point.y
            }
          );
        }

        if (
          canvas.hasPointerCapture(
            event.pointerId
          )
        ) {
          canvas.releasePointerCapture(
            event.pointerId
          );
        }

        roadEndpointDragPointerRef
          .current =
          undefined;
        roadEndpointDragControlRef
          .current =
          undefined;
        pointerStart = undefined;
        pointerDownHandledSelection =
          false;
        return;
      }

      if (
        roadCurveDragPointerRef.current ===
          event.pointerId
      ) {
        const offset =
          getRoadCurveOffsetFromPointer(
            event
          );

        if (
          offset !== undefined
        ) {
          onRoadCurveOffsetChange?.(
            offset
          );
        }

        if (
          canvas.hasPointerCapture(
            event.pointerId
          )
        ) {
          canvas.releasePointerCapture(
            event.pointerId
          );
        }

        roadCurveDragPointerRef.current =
          undefined;
        pointerStart = undefined;
        pointerDownHandledSelection =
          false;
        return;
      }

      if (
        roadDragPointerRef.current ===
        event.pointerId
      ) {
        emitSplineRoadPoint(event);

        if (
          canvas.hasPointerCapture(
            event.pointerId
          )
        ) {
          canvas.releasePointerCapture(
            event.pointerId
          );
        }

        roadDragPointerRef.current =
          undefined;
        pointerStart = undefined;
        pointerDownHandledSelection =
          false;
        return;
      }

      if (event.button !== 0 || !pointerStart) {
        pointerStart = undefined;
        return;
      }

      const dragDistance = Math.hypot(
        event.clientX - pointerStart.x,
        event.clientY - pointerStart.y
      );
      pointerStart = undefined;

      if (dragDistance > 5) {
        pointerDownHandledSelection =
          false;
        return;
      }

      // A real mesh hit was already selected on pointerdown. Do not run
      // the pointerup proximity fallback for the same click, otherwise
      // a valid selection can be immediately cleared and the second
      // quick click never reaches the focus logic.
      if (
        pointerDownHandledSelection
      ) {
        pointerDownHandledSelection =
          false;
        return;
      }

      pointerDownHandledSelection =
        false;

      const rect = canvas.getBoundingClientRect();
      const pointerX =
        (event.clientX - rect.left) * (engine.getRenderWidth() / rect.width);
      const pointerY =
        (event.clientY - rect.top) * (engine.getRenderHeight() / rect.height);

      const ray = scene.createPickingRay(
        pointerX,
        pointerY,
        Matrix.Identity(),
        camera,
        false
      );
      const direction = ray.direction.normalizeToNew();

      if (
        (
          placementAssetPath &&
          onPlacementPoint
        ) ||
        (
          splinePlacementTemplate &&
          onSplinePlacementPoint
        )
      ) {
        if (
          Math.abs(direction.y) >
          0.000001
        ) {
          const distanceToGround =
            -ray.origin.y /
            direction.y;

          if (distanceToGround > 0) {
            const groundPoint =
              ray.origin.add(
                direction.scale(
                  distanceToGround
                )
              );

            const tileX =
              Math.floor(
                groundPoint.x /
                300
              );

            const tileY =
              Math.floor(
                groundPoint.z /
                300
              );

            const tileExists =
              tiles.some(
                (tile) =>
                  tile.x === tileX &&
                  tile.y === tileY &&
                  (!tile.detailsLoaded ||
                    tile.fileExists)
              );

            if (tileExists) {
              const rawX =
                groundPoint.x -
                tileX * 300;

              const rawY =
                groundPoint.z -
                tileY * 300;

              const snappedX =
                snapEnabled &&
                moveSnap > 0
                  ? Math.round(
                      rawX /
                        moveSnap
                    ) * moveSnap
                  : rawX;

              const snappedY =
                snapEnabled &&
                moveSnap > 0
                  ? Math.round(
                      rawY /
                        moveSnap
                    ) * moveSnap
                  : rawY;

              if (
                placementAssetPath &&
                onPlacementPoint
              ) {
                onPlacementPoint({
                  tileX,
                  tileY,
                  x: snappedX,
                  y: snappedY,
                  z: 0,
                  rotation: 0,
                  pitch: 0,
                  bank: 0
                });
              }

              if (
                splinePlacementTemplate &&
                onSplinePlacementPoint
              ) {
                onSplinePlacementPoint({
                  targetTileX: tileX,
                  targetTileY: tileY,
                  x: snappedX,
                  y: snappedY
                });
              }

              if (
                onActiveTileChange
              ) {
                onActiveTileChange({
                  x: tileX,
                  y: tileY
                });
              }
            }
          }
        }

        return;
      }

      if (
        selectionMode === "terrain"
      ) {
        const terrainPick =
          scene.pick(
            pointerX,
            pointerY,
            (mesh) =>
              mesh.metadata
                ?.mapStudioKind ===
              "terrain",
            false,
            camera
          );

        const point =
          terrainPick?.pickedPoint;

        if (
          terrainPick?.hit &&
          point
        ) {
          const tileX =
            Math.floor(
              point.x / 300
            );
          const tileY =
            Math.floor(
              point.z / 300
            );

          const localX =
            point.x -
            tileX * 300;
          const localY =
            point.z -
            tileY * 300;

          setViewportDiagnostic({
            item:
              `Terreno tile ${tileX},${tileY}`,
            mesh:
              terrainPick.pickedMesh
                ?.name ??
              "omsi-editor-terrain",
            material:
              "groundtex real",
            texture:
              terrainMainTextureAsset
                ?.resolvedPath ??
              terrainMainTextureAsset
                ?.errorCode ??
              "textura base não resolvida",
            uv:
              `local ${localX.toFixed(3)}, ${localY.toFixed(3)}`,
            position:
              `X ${point.x.toFixed(3)} · Y ${point.y.toFixed(3)} · Z ${point.z.toFixed(3)}`,
            renderLift: "0.000",
            origin:
              "global.cfg / .map.terrain"
          });

          onSelectObject(undefined);
          onSelectSpline(undefined);

          onTerrainPoint?.({
            tileX,
            tileY,
            x: localX,
            y: localY,
            height: point.y
          });

          onActiveTileChange?.({
            x: tileX,
            y: tileY
          });

          return;
        }
      }

      // Selection is handled on pointerdown so React can rebuild the
      // selected-item scene before pointerup. Keep the geometric
      // proximity fallback below for objects without pickable meshes.
      const threshold = Math.max(2.5, Math.min(20, camera.radius * 0.004));

      let selected: OmsiPlacedObject | undefined;
      let bestDistance = threshold;
      let bestDepth = Number.POSITIVE_INFINITY;

      for (
        const placedObject of
          objectSelectionEnabled
            ? objects
            : []
      ) {
        const position =
          getObjectWorldPosition(
            placedObject,
            objectGeometryByPath[
              placedObject
                .sceneryObjectPath
            ],
            tiles
          );
        const offset = position.subtract(ray.origin);
        const depth = Vector3.Dot(offset, direction);

        if (depth < 0) {
          continue;
        }

        const closestPoint = ray.origin.add(direction.scale(depth));
        const distance = Vector3.Distance(position, closestPoint);

        if (
          distance < bestDistance ||
          (Math.abs(distance - bestDistance) < 0.001 && depth < bestDepth)
        ) {
          selected = placedObject;
          bestDistance = distance;
          bestDepth = depth;
        }
      }

      if (selected) {
        const position =
          getObjectWorldPosition(
            selected,
            objectGeometryByPath[
              selected.sceneryObjectPath
            ],
            tiles
          );

        setViewportDiagnostic({
          item:
            `Objeto #${selected.objectId}`,
          mesh:
            "fallback geométrico",
          material:
            "não determinado",
          texture:
            "não determinada",
          uv:
            "não determinado",
          position:
            `X ${position.x.toFixed(3)} · Y ${position.y.toFixed(3)} · Z ${position.z.toFixed(3)}`,
          renderLift: "0.000",
          origin:
            selected.sceneryObjectPath
        });

        onSelectSpline(undefined);
        onSelectObject(selected);
        return;
      }

      const splinePick =
        splineSelectionEnabled
          ? scene.pick(
          pointerX,
          pointerY,
          (mesh) =>
            mesh.metadata
              ?.mapStudioKind ===
            "spline",
          false,
          camera
        )
          : undefined;

      if (
        splinePick?.hit &&
        splinePick.pickedMesh
          ?.metadata &&
        typeof splinePick
          .pickedMesh
          .metadata
          .splineIndex === "number"
      ) {
        const splineIndex =
          splinePick.pickedMesh
            .metadata
            .splineIndex as number;

        const placedSpline =
          splines[splineIndex];

        if (placedSpline) {
          onSelectObject(undefined);
          onSelectSpline(
            placedSpline
          );
          return;
        }
      }

      setViewportDiagnostic(
        undefined
      );
      onSelectObject(undefined);
      onSelectSpline(undefined);

      if (
        !usesWorldCoordinates &&
        onActiveTileChange &&
        Math.abs(direction.y) >
          0.000001
      ) {
        const distanceToGround =
          -ray.origin.y /
          direction.y;

        if (distanceToGround > 0) {
          const groundPoint =
            ray.origin.add(
              direction.scale(
                distanceToGround
              )
            );

          const tileX =
            Math.floor(
              groundPoint.x / 300
            );

          const tileY =
            Math.floor(
              groundPoint.z / 300
            );

          if (
            tiles.some(
              (tile) =>
                tile.x === tileX &&
                tile.y === tileY
            )
          ) {
            if (
              selectionMode ===
                "terrain"
            ) {
              setViewportDiagnostic({
                item:
                  `Terreno tile ${tileX},${tileY}`,
                mesh:
                  "omsi-editor-terrain",
                material:
                  "groundtex real",
                texture:
                  terrainMainTextureAsset
                    ?.resolvedPath ??
                  terrainMainTextureAsset
                    ?.errorCode ??
                  "textura base não resolvida",
                uv:
                  "UV do tile 0..1",
                position:
                  `X ${groundPoint.x.toFixed(3)} · Y 0.000 · Z ${groundPoint.z.toFixed(3)}`,
                renderLift:
                  "0.000",
                origin:
                  "global.cfg / .map.terrain"
              });
            }

            onActiveTileChange({
              x: tileX,
              y: tileY
            });
          }
        }
      }
    };

    const handleAssetDragOver = (
      event: DragEvent
    ) => {
      if (
        !onLibraryAssetDrop ||
        !Array.from(
          event.dataTransfer?.types ?? []
        ).some(
          (type) =>
            type ===
              "application/x-omsi-map-studio-scenery" ||
            type ===
              "application/x-omsi-map-studio-spline"
        )
      ) {
        return;
      }

      event.preventDefault();

      if (event.dataTransfer) {
        event.dataTransfer.dropEffect =
          "copy";
      }
    };

    const handleAssetDrop = (
      event: DragEvent
    ) => {
      if (!onLibraryAssetDrop) {
        return;
      }

      const sceneryPath =
        event.dataTransfer?.getData(
          "application/x-omsi-map-studio-scenery"
        );
      const splinePath =
        event.dataTransfer?.getData(
          "application/x-omsi-map-studio-spline"
        );

      if (!sceneryPath && !splinePath) {
        return;
      }

      const point =
        getPlacementPointFromPointer(
          event
        );

      if (!point) {
        return;
      }

      event.preventDefault();

      onLibraryAssetDrop({
        kind: sceneryPath
          ? "object"
          : "spline",
        assetPath:
          sceneryPath ||
          splinePath ||
          "",
        point
      });

      onActiveTileChange?.({
        x: point.tileX,
        y: point.tileY
      });
    };

    const handlePointerLeave = () => {
      clearSelectionHover();
      selectionHoverActive = false;
      canvas.style.cursor = "";
    };

    const handlePointerCancel = (
      event: PointerEvent
    ) => {
      if (
        roadEndpointDragPointerRef
          .current ===
          event.pointerId
      ) {
        roadEndpointDragPointerRef
          .current =
          undefined;
        roadEndpointDragControlRef
          .current =
          undefined;
      }

      if (
        roadCurveDragPointerRef.current ===
        event.pointerId
      ) {
        roadCurveDragPointerRef.current =
          undefined;
      }

      if (
        roadDragPointerRef.current ===
        event.pointerId
      ) {
        roadDragPointerRef.current =
          undefined;
      }

      pointerStart = undefined;
      finishNavigationPointer(event);
    };

    canvas.addEventListener(
      "pointerdown",
      handleNavigationPointerDown
    );
    canvas.addEventListener(
      "pointerdown",
      handlePointerDown
    );
    canvas.addEventListener(
      "pointermove",
      handlePointerMove
    );
    canvas.addEventListener(
      "pointerup",
      finishNavigationPointer
    );
    canvas.addEventListener(
      "pointerup",
      handlePointerUp
    );
    canvas.addEventListener(
      "pointercancel",
      handlePointerCancel
    );
    canvas.addEventListener(
      "pointerleave",
      handlePointerLeave
    );
    canvas.addEventListener(
      "wheel",
      handleWheel,
      { passive: false }
    );
    canvas.addEventListener(
      "keydown",
      handleViewportKeyDown
    );
    canvas.addEventListener(
      "contextmenu",
      handleContextMenu
    );
    canvas.addEventListener(
      "dragover",
      handleAssetDragOver
    );
    canvas.addEventListener(
      "drop",
      handleAssetDrop
    );

    const refreshObjectLods =
      () => {
        for (const instance of
          objectLodInstances) {
          updateObjectLod(
            instance,
            camera
          );
        }
      };

    refreshObjectLods();

    let lodFrame = 0;

    const lodObserver =
      scene.onBeforeRenderObservable.add(
        () => {
          lodFrame =
            (lodFrame + 1) % 6;

          if (lodFrame === 0) {
            refreshObjectLods();
          }
        }
      );

    let thumbnailFrame = 0;
    let thumbnailSent = false;

    engine.runRenderLoop(() => {
      scene.render();

      if (
        captureThumbnail &&
        !thumbnailSent &&
        thumbnailCallbackRef.current
      ) {
        thumbnailFrame += 1;

        if (thumbnailFrame >= 24) {
          try {
            const dataUrl =
              canvas.toDataURL(
                "image/jpeg",
                0.72
              );

            if (
              dataUrl.startsWith(
                "data:image/"
              ) &&
              dataUrl.length > 256
            ) {
              thumbnailSent = true;
              thumbnailCallbackRef.current(
                dataUrl
              );
            }
          } catch {
            thumbnailSent = true;
          }
        }
      }
    });

    let resizeFrame:
      | number
      | undefined;

    const resize = () => {
      if (resizeFrame !== undefined) {
        cancelAnimationFrame(
          resizeFrame
        );
      }

      resizeFrame =
        requestAnimationFrame(() => {
          resizeFrame = undefined;
          engine.resize();
        });
    };

    // WebView2/WPF fullscreen and movable city-builder panels can
    // change the viewport without dispatching a browser window.resize.
    // Observe the real container so Babylon render dimensions and
    // click coordinates stay synchronized with the visible canvas.
    const resizeObserver =
      typeof ResizeObserver !==
      "undefined"
        ? new ResizeObserver(resize)
        : undefined;

    resizeObserver?.observe(
      canvas.parentElement ?? canvas
    );
    window.addEventListener(
      "resize",
      resize
    );
    resize();

    return () => {
      cameraStateRef.current = {
        alpha: camera.alpha,
        beta: camera.beta,
        radius: camera.radius,
        target: {
          x: camera.target.x,
          y: camera.target.y,
          z: camera.target.z
        }
      };

      canvas.removeEventListener(
        "pointerdown",
        handleNavigationPointerDown
      );
      canvas.removeEventListener(
        "pointerdown",
        handlePointerDown
      );
      canvas.removeEventListener(
        "pointermove",
        handlePointerMove
      );
      canvas.removeEventListener(
        "pointerup",
        finishNavigationPointer
      );
      canvas.removeEventListener(
        "pointerup",
        handlePointerUp
      );
      canvas.removeEventListener(
        "pointercancel",
        handlePointerCancel
      );
      canvas.removeEventListener(
        "pointerleave",
        handlePointerLeave
      );
      clearSelectionHover();
      canvas.removeEventListener(
        "wheel",
        handleWheel
      );
      canvas.removeEventListener(
        "keydown",
        handleViewportKeyDown
      );
      canvas.removeEventListener(
        "contextmenu",
        handleContextMenu
      );
      canvas.removeEventListener(
        "dragover",
        handleAssetDragOver
      );
      canvas.removeEventListener(
        "drop",
        handleAssetDrop
      );
      window.removeEventListener(
        "resize",
        resize
      );
      resizeObserver?.disconnect();

      if (resizeFrame !== undefined) {
        cancelAnimationFrame(
          resizeFrame
        );
      }

      if (lodObserver) {
        scene.onBeforeRenderObservable.remove(
          lodObserver
        );
      }

      canvas.style.cursor = "";
      gizmoManager?.dispose();
      scene.dispose();
      engine.dispose();
    };
  }, [
    tiles,
    cameraStateKey,
    editorTool,
    selectionMode,
    snapEnabled,
    moveSnap,
    rotationSnap,
    showGrid,
    showTerrain,
    terrainMainTextureAsset,
    terrainMainTextureRepeating,
    terrainOverlays,
    showObjects,
    showSplines,
    showSplineProfiles,
    showAllSplineProfiles,
    nightPreviewEnabled,
    skyTextureAsset,
    cameraAction,
    placementAssetPath,
    placementGeometry,
    pendingPlacement,
    pendingPlacementBatch,
    onPlacementPoint,
    onLibraryAssetDrop,
    captureThumbnail,
    splinePlacementTemplate,
    splinePlacementProfile,
    pendingSplinePlacement,
    onSplinePlacementPoint,
    roadDragMode,
    roadCurveControl,
    onRoadCurveOffsetChange,
    onRoadControlPointChange,
    objects,
    splines,
    activeTile,
    onActiveTileChange,
    onTerrainPoint,
    referenceOverlay,
    usesWorldCoordinates,
    selectedObject,
    selectedGeometry,
    objectGeometryByPath,
    textureAssetsByKey,
    splineProfilesByPath,
    selectedSpline,
    selectedSplineProfile,
    onSelectObject,
    onSelectSpline,
    onPreviewObjectTransform,
    onPreviewSplineTransform
  ]);

  return (
    <>
      <canvas
        ref={canvasRef}
        className="viewport-canvas"
        tabIndex={0}
        aria-label="Viewport 3D do editor"
        title="Clique seleciona qualquer objeto/spline visível ou por posição OMSI · segundo clique rápido centraliza · botão direito orbita · botão do meio desloca · WASD/setas movem · Ctrl+setas salta 1 bloco/tile · roda aproxima/afasta"
      />
      {referenceOverlay && (
        <div className="reference-attribution">
          Referência: {referenceOverlay.attribution}
        </div>
      )}
      {viewportDiagnostic ? (
        <div
          className="viewport-diagnostic"
          aria-live="polite"
        >
          <strong>
            Diagnóstico real da seleção
          </strong>
          <span>
            {viewportDiagnostic.item}
          </span>
          <span>
            Mesh/material:{" "}
            {viewportDiagnostic.mesh} /{" "}
            {viewportDiagnostic.material}
          </span>
          <span>
            Textura:{" "}
            {viewportDiagnostic.texture}
          </span>
          <span>
            UV: {viewportDiagnostic.uv}
          </span>
          <span>
            Posição final:{" "}
            {viewportDiagnostic.position}
          </span>
          <span>
            Render lift:{" "}
            {viewportDiagnostic.renderLift}
          </span>
          <span>
            Origem:{" "}
            {viewportDiagnostic.origin}
          </span>
        </div>
      ) : null}
    </>
  );
}
