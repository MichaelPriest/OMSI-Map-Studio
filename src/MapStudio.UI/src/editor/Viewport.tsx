import { useEffect, useRef } from "react";
import { ArcRotateCamera } from "@babylonjs/core/Cameras/arcRotateCamera";
import { Engine } from "@babylonjs/core/Engines/engine";
import { GizmoManager } from "@babylonjs/core/Gizmos/gizmoManager";
import { HemisphericLight } from "@babylonjs/core/Lights/hemisphericLight";
import { Material } from "@babylonjs/core/Materials/material";
import { StandardMaterial } from "@babylonjs/core/Materials/standardMaterial";
import { Texture } from "@babylonjs/core/Materials/Textures/texture";
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
  getSplineTextureAssetKey
} from "../bridge/desktopBridge";

type ViewportProps = {
  tiles: OmsiTile[];
  editorTool: "select" | "move" | "rotate";
  snapEnabled: boolean;
  moveSnap: number;
  rotationSnap: number;
  showGrid: boolean;
  showObjects: boolean;
  showSplines: boolean;
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

function createTileSurface(
  scene: Scene,
  tiles: OmsiTile[],
  tileSize: number
) {
  if (tiles.length === 0) {
    return;
  }

  const positions: number[] = [];
  const indices: number[] = [];

  for (
    let tileIndex = 0;
    tileIndex < tiles.length;
    tileIndex += 1
  ) {
    const tile = tiles[tileIndex];

    if (
      tile.detailsLoaded &&
      !tile.fileExists
    ) {
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
      positions.length / 3;

    positions.push(
      x0, -0.04, z0,
      x1, -0.04, z0,
      x1, -0.04, z1,
      x0, -0.04, z1
    );

    indices.push(
      vertexOffset,
      vertexOffset + 2,
      vertexOffset + 1,
      vertexOffset,
      vertexOffset + 3,
      vertexOffset + 2
    );
  }

  if (positions.length === 0) {
    return;
  }

  const mesh = new Mesh(
    "omsi-editor-tile-surface",
    scene
  );

  const vertexData =
    new VertexData();

  vertexData.positions =
    positions;

  vertexData.indices =
    indices;

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
      "omsi-editor-tile-surface-material",
      scene
    );

  material.diffuseColor =
    new Color3(
      0.045,
      0.095,
      0.14
    );

  material.specularColor =
    Color3.Black();

  material.alpha = 0.92;

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
      y: activeTile.y,
      relativeMapPath: "",
      detailsLoaded: true,
      fileExists: true,
      objectCount: 0,
      splineCount: 0,
      splineAttachmentCount: 0
    },
    tileSize
  );
}

function createTileOutline(tile: OmsiTile, tileSize: number) {
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

function getObjectWorldPosition(placedObject: OmsiPlacedObject) {
  return new Vector3(
    placedObject.tileX * 300 + placedObject.x,
    placedObject.z,
    placedObject.tileY * 300 + placedObject.y
  );
}

function createObjectMarkerLines(objects: OmsiPlacedObject[]) {
  const markerRadius = 1.5;
  const markerHeight = 3;

  return objects.flatMap((placedObject) => {
    const position = getObjectWorldPosition(placedObject);

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
        new Vector3(0, 0.08, 0)
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
  parent?: TransformNode
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
      `selected-spline-profile-${surfaceIndex}`,
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
        `selected-spline-profile-material-${surfaceIndex}`,
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
        material.diffuseTexture =
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

function createSelectedMarkerLines(placedObject: OmsiPlacedObject) {
  const position = getObjectWorldPosition(placedObject);
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
    !asset.base64Data ||
    !asset.extension
  ) {
    return undefined;
  }

  const mimeType =
    asset.mimeType ??
    "application/octet-stream";

  const texture =
    new Texture(
      `data:${mimeType};base64,${asset.base64Data}`,
      scene,
      {
        forcedExtension:
          asset.extension
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

  material.alpha =
    clamp01(materialData.diffuseA);

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
    );

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
  placedObject: OmsiPlacedObject
) {
  root.position.copyFrom(
    getObjectWorldPosition(
      placedObject
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
    ObjectLodInstance[]
) {
  const root = new TransformNode(
    "selected-object-geometry-root",
    scene
  );

  configureObjectRoot(
    root,
    placedObject
  );

  const meshes =
    createGeometryMeshes(
      scene,
      "selected-object",
      placedObject.sceneryObjectPath,
      geometry,
      textureAssetsByKey,
      nightPreviewEnabled
    );

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
    ObjectLodInstance[]
) {
  const placementsByPath =
    new Map<
      string,
      OmsiPlacedObject[]
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
      !hasRenderableGeometry(
        geometry
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
      placements[0]
    );

    const sourceMeshes =
      createGeometryMeshes(
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
        ]
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
  editorTool,
  snapEnabled,
  moveSnap,
  rotationSnap,
  showGrid,
  showObjects,
  showSplines,
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
  selectedSpline,
  selectedSplineProfile,
  onSelectObject,
  onSelectSpline,
  onPreviewObjectTransform,
  onPreviewSplineTransform
}: ViewportProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

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

    const target = tiles.length
      ? new Vector3(
          ((minX + maxX + 1) * tileSize) / 2,
          0,
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

    camera.lowerRadiusLimit = usesWorldCoordinates ? 0.5 : 5;
    camera.upperRadiusLimit = Math.max(usesWorldCoordinates ? 50 : 900, radius * 4);
    camera.attachControl(canvas, true);

    if (
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
            selectedObject
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
        { lines: createSelectedMarkerLines(placedObject) },
        scene
      );
      selectionMarker.color = new Color3(1, 0.96, 0.68);
      selectionMarker.isPickable = false;
    };

    if (tiles.length) {
      if (showGrid) {
        createTileSurface(
          scene,
          tiles,
          tileSize
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
          objectLodInstances
        );

        const markerObjects =
          objects.filter(
            (placedObject) =>
              !hasRenderableGeometry(
                objectGeometryByPath[
                  placedObject
                    .sceneryObjectPath
                ]
              )
          );

        if (markerObjects.length) {
          const objectMarkers =
            MeshBuilder.CreateLineSystem(
              "omsi-object-markers",
              {
                lines:
                  createObjectMarkerLines(
                    markerObjects
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
          objectLodInstances
        );
      }

      const placementMarker =
        MeshBuilder.CreateLineSystem(
          "omsi-new-object-preview",
          {
            lines:
              createSelectedMarkerLines(
                placementPreview
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
      !hasRenderableGeometry(
        objectGeometryByPath[
          selectedObject
            .sceneryObjectPath
        ]
      )
    ) {
      createSelectedGeometry(
        scene,
        selectedObject,
        selectedGeometry,
        textureAssetsByKey,
        nightPreviewEnabled,
        objectLodInstances
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
        hasRenderableGeometry(
          selectedGeometry
        )
      ) {
        editRoot =
          createSelectedGeometry(
            scene,
            selectedObject,
            selectedGeometry,
            textureAssetsByKey,
            nightPreviewEnabled,
            objectLodInstances
          );
      } else {
        editRoot =
          new TransformNode(
            "preview-edit-anchor",
            scene
          );

        configureObjectRoot(
          editRoot,
          selectedObject
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
            z: editRoot.position.y,
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

    let pointerStart: { x: number; y: number } | undefined;

    const handlePointerDown = (event: PointerEvent) => {
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
        const position = getObjectWorldPosition(placedObject);
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

    const handlePointerCancel = () => {
      pointerStart = undefined;
    };

    canvas.addEventListener("pointerdown", handlePointerDown);
    canvas.addEventListener("pointerup", handlePointerUp);
    canvas.addEventListener("pointercancel", handlePointerCancel);

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
      canvas.removeEventListener("pointerdown", handlePointerDown);
      canvas.removeEventListener("pointerup", handlePointerUp);
      canvas.removeEventListener("pointercancel", handlePointerCancel);
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
    editorTool,
    snapEnabled,
    moveSnap,
    rotationSnap,
    showGrid,
    showObjects,
    showSplines,
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
    selectedSpline,
    selectedSplineProfile,
    onSelectObject,
    onSelectSpline,
    onPreviewObjectTransform,
    onPreviewSplineTransform
  ]);

  return <canvas ref={canvasRef} className="viewport-canvas" />;
}
