import { useEffect, useRef } from "react";
import { ArcRotateCamera } from "@babylonjs/core/Cameras/arcRotateCamera";
import { Engine } from "@babylonjs/core/Engines/engine";
import { HemisphericLight } from "@babylonjs/core/Lights/hemisphericLight";
import { StandardMaterial } from "@babylonjs/core/Materials/standardMaterial";
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
  OmsiSceneryObjectGeometry,
  OmsiTile
} from "../bridge/desktopBridge";

type ViewportProps = {
  tiles: OmsiTile[];
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
  selectedSpline?: OmsiPlacedSpline;
  selectedSplineProfile?: OmsiSplineDefinition;
  onSelectObject: (placedObject: OmsiPlacedObject | undefined) => void;
  onSelectSpline: (placedSpline: OmsiPlacedSpline | undefined) => void;
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
    -placedSpline.rotation *
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
  definition: OmsiSplineDefinition
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

    mesh.material = material;
    mesh.isPickable = false;
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

function createPreviewMaterial(
  scene: Scene,
  meshIndex: number,
  materialIndex: number,
  materialData:
    | OmsiSceneryObjectGeometry["meshes"][number]["geometry"]["materials"][number]
    | undefined
) {
  const material = new StandardMaterial(
    `selected-object-material-${meshIndex}-${materialIndex}`,
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

  return material;
}

function createSelectedGeometry(
  scene: Scene,
  placedObject: OmsiPlacedObject,
  geometry: OmsiSceneryObjectGeometry
) {
  const root = new TransformNode(
    "selected-object-geometry-root",
    scene
  );

  root.position.copyFrom(
    getObjectWorldPosition(placedObject)
  );

  root.rotationQuaternion =
    Quaternion.RotationYawPitchRoll(
      -placedObject.rotation * degreesToRadians,
      -placedObject.bank * degreesToRadians,
      -placedObject.pitch * degreesToRadians
    );

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
        `selected-o3d-${meshIndex}-material-${materialIndex}`,
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
          meshIndex,
          materialIndex,
          materialIndex >= 0
            ? meshGeometry.materials[
                materialIndex
              ]
            : undefined
        );

      mesh.parent = root;
      mesh.isPickable = false;
    }
  }
}


export function Viewport({
  tiles,
  objects,
  splines,
  activeTile,
  onActiveTileChange,
  usesWorldCoordinates,
  selectedObject,
  selectedGeometry,
  selectedSpline,
  selectedSplineProfile,
  onSelectObject,
  onSelectSpline
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

    const light = new HemisphericLight("editor-light", new Vector3(0, 1, 0), scene);
    light.intensity = 0.9;

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
      createTileSurface(
        scene,
        tiles,
        tileSize
      );

      const existingLines = tiles
        .filter(
          (tile) =>
            !tile.detailsLoaded ||
            tile.fileExists
        )
        .map((tile) => createTileOutline(tile, tileSize));

      if (existingLines.length) {
        const existingGrid = MeshBuilder.CreateLineSystem(
          "omsi-tile-grid",
          { lines: existingLines },
          scene
        );
        existingGrid.color = new Color3(0.55, 0.68, 0.82);
        existingGrid.isPickable = false;
      }

      const missingLines = tiles
        .filter(
          (tile) =>
            tile.detailsLoaded &&
            !tile.fileExists
        )
        .map((tile) => createTileOutline(tile, tileSize));

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
        !usesWorldCoordinates &&
        splines.length
      ) {
        splines.forEach(
          (
            placedSpline,
            splineIndex
          ) => {
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

      if (!usesWorldCoordinates && objects.length) {
        const objectMarkers = MeshBuilder.CreateLineSystem(
          "omsi-object-markers",
          { lines: createObjectMarkerLines(objects) },
          scene
        );
        objectMarkers.color = new Color3(0.95, 0.78, 0.38);
        objectMarkers.isPickable = false;
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

    if (
      !usesWorldCoordinates &&
      selectedSpline &&
      selectedSplineProfile
    ) {
      createSelectedSplineProfile(
        scene,
        selectedSpline,
        selectedSplineProfile
      );
    }

    if (
      !usesWorldCoordinates &&
      selectedObject &&
      selectedGeometry
    ) {
      createSelectedGeometry(
        scene,
        selectedObject,
        selectedGeometry
      );
    }

    showSelection(selectedObject);

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
      const threshold = Math.max(2.5, Math.min(20, camera.radius * 0.004));

      let selected: OmsiPlacedObject | undefined;
      let bestDistance = threshold;
      let bestDepth = Number.POSITIVE_INFINITY;

      for (const placedObject of objects) {
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
        scene.pick(
          pointerX,
          pointerY,
          (mesh) =>
            mesh.metadata
              ?.mapStudioKind ===
            "spline",
          false,
          camera
        );

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

    engine.runRenderLoop(() => scene.render());

    const resize = () => engine.resize();
    window.addEventListener("resize", resize);

    return () => {
      canvas.removeEventListener("pointerdown", handlePointerDown);
      canvas.removeEventListener("pointerup", handlePointerUp);
      canvas.removeEventListener("pointercancel", handlePointerCancel);
      window.removeEventListener("resize", resize);
      scene.dispose();
      engine.dispose();
    };
  }, [
    tiles,
    objects,
    splines,
    activeTile,
    onActiveTileChange,
    usesWorldCoordinates,
    selectedObject,
    selectedGeometry,
    selectedSpline,
    selectedSplineProfile,
    onSelectObject,
    onSelectSpline
  ]);

  return <canvas ref={canvasRef} className="viewport-canvas" />;
}
