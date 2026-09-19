import { useEffect, useRef } from "react";
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

type ViewportProps = {
  tiles: OmsiTile[];
  cameraStateKey: string;
  editorTool: "select" | "move" | "rotate";
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
  cameraAction?: {
    type:
      | "fit"
      | "focus"
      | "perspective"
      | "top";
    token: number;
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
                point.z + 0.03,
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

    if (surface.textureName) {
      const asset =
        textureAssetsByKey[
          getSplineTextureAssetKey(
            placedSpline.splinePath,
            surface.textureName
          )
        ];

      const texture =
        createTextureFromAsset(
          scene,
          asset
        );

      if (texture) {
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
      }
    }

    mesh.material = material;
    mesh.isPickable = false;

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
  const radius = 4;
  const height = 8;

  return [
    [
      new Vector3(position.x - radius, position.y, position.z - radius),
      new Vector3(position.x + radius, position.y, position.z - radius),
      new Vector3(position.x + radius, position.y, position.z + radius),
      new Vector3(position.x - radius, position.y, position.z + radius),
      new Vector3(position.x - radius, position.y, position.z - radius)
    ],
    [
      new Vector3(position.x, position.y, position.z),
      new Vector3(position.x, position.y + height, position.z)
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

  const textureName =
    `inline-${asset.extension}-${asset.base64Data.length}-${asset.base64Data.slice(0, 16)}`;

  const texture =
    forcedExtension
      ? new Texture(
          `data:${mimeType};base64,${asset.base64Data}`,
          scene,
          {
            forcedExtension
          }
        )
      : Texture.CreateFromBase64String(
          asset.base64Data,
          textureName,
          scene,
          false,
          false,
          Texture.TRILINEAR_SAMPLINGMODE,
          undefined,
          () => {
            console.error(
              "OMSI Map Studio: texture upload failed",
              {
                extension:
                  asset.extension,
                sourceExtension:
                  asset.sourceExtension,
                mimeType
              }
            );
          }
        );

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

  return overrides.find(
    (override) =>
      override.materialIndex ===
        materialIndex &&
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
          nightPreviewEnabled
        );

      const meshTransform =
        meshReference.transform;

      mesh.position.set(
        meshTransform.positionX,
        meshTransform.positionZ,
        meshTransform.positionY
      );

      mesh.scaling.set(
        meshTransform.scaleX,
        meshTransform.scaleZ,
        meshTransform.scaleY
      );

      mesh.rotationQuaternion =
        Quaternion.RotationYawPitchRoll(
          -meshTransform.rotationZ *
            degreesToRadians,
          -meshTransform.rotationX *
            degreesToRadians,
          -meshTransform.rotationY *
            degreesToRadians
        );

      mesh.metadata = {
        ...(mesh.metadata ?? {}),
        mapStudioLodThreshold:
          meshReference
            .lodThreshold
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

  root.rotationQuaternion =
    Quaternion.RotationYawPitchRoll(
      -placedObject.rotation *
        degreesToRadians,
      -placedObject.bank *
        degreesToRadians,
      -placedObject.pitch *
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
  }

  for (const mesh of meshes) {
    mesh.parent = root;
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
            ...(source.metadata ?? {})
          };

          clone.isPickable = false;
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


export function Viewport({
  tiles,
  cameraStateKey,
  editorTool,
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
  cameraAction,
  placementAssetPath,
  placementGeometry,
  pendingPlacement,
  onPlacementPoint,
  splinePlacementTemplate,
  splinePlacementProfile,
  pendingSplinePlacement,
  onSplinePlacementPoint,
  objects,
  splines,
  activeTile,
  onActiveTileChange,
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

    const engine = new Engine(canvas, true);
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
      }
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

    const light = new HemisphericLight("editor-light", new Vector3(0, 1, 0), scene);
    light.intensity = 0.9;

    const objectLodInstances:
      ObjectLodInstance[] = [];

    let selectionMarker: ReturnType<typeof MeshBuilder.CreateLineSystem> | undefined;

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
      selectionMarker.color = new Color3(1, 0.96, 0.68);
      selectionMarker.isPickable = false;
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
              splineIndex
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

          if (protectedMarkers.length) {
            const objectMarkers =
              MeshBuilder.CreateLineSystem(
                "omsi-protected-object-markers",
                {
                  lines:
                    createObjectMarkerLines(
                      protectedMarkers,
                      objectGeometryByPath,
                      tiles
                    )
                },
                scene
              );

            objectMarkers.color =
              new Color3(
                0.48,
                0.66,
                0.82
              );

            objectMarkers.isPickable =
              false;
          }

          if (missingMarkers.length) {
            const objectMarkers =
              MeshBuilder.CreateLineSystem(
                "omsi-missing-object-markers",
                {
                  lines:
                    createObjectMarkerLines(
                      missingMarkers,
                      objectGeometryByPath,
                      tiles
                    )
                },
                scene
              );

            objectMarkers.color =
              new Color3(
                0.95,
                0.78,
                0.38
              );

            objectMarkers.isPickable =
              false;
          }
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
        createSelectedGeometry(
          scene,
          placementPreview,
          placementGeometry,
          textureAssetsByKey,
          nightPreviewEnabled,
          objectLodInstances,
          tiles
        );
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

    let splinePlacementPreview:
      OmsiPlacedSpline | undefined;

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

      const points =
        getSplineAxisLine(
          splinePlacementPreview
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
      }

      if (splinePlacementProfile) {
        createSelectedSplineProfile(
          scene,
          splinePlacementPreview,
          splinePlacementProfile,
          textureAssetsByKey
        );
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
      showObjects &&
      !placementAssetPath
        ? selectedObject
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
              -euler.y /
              degreesToRadians,
            bank:
              -euler.x /
              degreesToRadians,
            pitch:
              -euler.z /
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

    let navigationPointer:
      | {
          pointerId: number;
          button: number;
          lastX: number;
          lastY: number;
        }
      | undefined;

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
      const step =
        Math.max(
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
      }
    };

    const handleContextMenu = (
      event: MouseEvent
    ) => {
      event.preventDefault();
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
    };

    const handlePointerUp = (event: PointerEvent) => {
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
        return;
      }

      if (usesWorldCoordinates) {
        onSelectObject(undefined);
        onSelectSpline(undefined);
        return;
      }

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

      const threshold = Math.max(2.5, Math.min(20, camera.radius * 0.004));

      let selected: OmsiPlacedObject | undefined;
      let bestDistance = threshold;
      let bestDepth = Number.POSITIVE_INFINITY;

      for (
        const placedObject of
          showObjects
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
        onSelectSpline(undefined);
        onSelectObject(selected);
        return;
      }

      const splinePick =
        showSplines
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
            onActiveTileChange({
              x: tileX,
              y: tileY
            });
          }
        }
      }
    };

    const handlePointerCancel = (
      event: PointerEvent
    ) => {
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

    engine.runRenderLoop(() => scene.render());

    const resize = () => engine.resize();
    window.addEventListener("resize", resize);

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
      window.removeEventListener("resize", resize);

      if (lodObserver) {
        scene.onBeforeRenderObservable.remove(
          lodObserver
        );
      }

      gizmoManager?.dispose();
      scene.dispose();
      engine.dispose();
    };
  }, [
    tiles,
    cameraStateKey,
    editorTool,
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
    cameraAction,
    placementAssetPath,
    placementGeometry,
    pendingPlacement,
    onPlacementPoint,
    splinePlacementTemplate,
    splinePlacementProfile,
    pendingSplinePlacement,
    onSplinePlacementPoint,
    objects,
    splines,
    activeTile,
    onActiveTileChange,
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
    <canvas
      ref={canvasRef}
      className="viewport-canvas"
      tabIndex={0}
      aria-label="Viewport 3D do editor"
      title="Navegação: botão direito orbita · botão do meio ou Shift+botão direito desloca · WASD/setas movem para frente/trás/laterais · Shift acelera · roda aproxima/afasta"
    />
  );
}
