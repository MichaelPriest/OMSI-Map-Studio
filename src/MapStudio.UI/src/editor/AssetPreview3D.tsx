import {
  Matrix,
  Quaternion,
  Vector3
} from "@babylonjs/core/Maths/math.vector";
import type {
  OmsiPlacedSpline,
  OmsiSceneryObjectGeometry,
  OmsiSplineDefinition,
  OmsiTextureAsset
} from "../bridge/desktopBridge";
import { Viewport } from "./Viewport";

type AssetPreview3DProps =
  | {
      kind: "object";
      assetPath: string;
      geometry: OmsiSceneryObjectGeometry;
      textureAssetsByKey: Record<string, OmsiTextureAsset>;
      onThumbnailReady?: (
        dataUrl: string
      ) => void;
    }
  | {
      kind: "spline";
      template: OmsiPlacedSpline;
      profile: OmsiSplineDefinition;
      textureAssetsByKey: Record<string, OmsiTextureAsset>;
      length?: number;
      radius?: number;
      rotation?: number;
      onThumbnailReady?: (
        dataUrl: string
      ) => void;
    };

const noopObjectSelection = () => {};
const noopSplineSelection = () => {};
const noopObjectTransform = () => {};
const noopSplineTransform = () => {};
const noopPlacementPoint = () => {};
const noopSplinePlacementPoint = () => {};

const degreesToRadians =
  Math.PI / 180;

const formatDimension = (
  value: number
) =>
  value >= 10
    ? value.toFixed(1)
    : value.toFixed(2);

const getObjectDimensionLabel = (
  geometry: OmsiSceneryObjectGeometry
) => {
  let minimum =
    new Vector3(
      Number.POSITIVE_INFINITY,
      Number.POSITIVE_INFINITY,
      Number.POSITIVE_INFINITY
    );
  let maximum =
    new Vector3(
      Number.NEGATIVE_INFINITY,
      Number.NEGATIVE_INFINITY,
      Number.NEGATIVE_INFINITY
    );
  let hasPoint = false;

  for (const mesh of geometry.meshes) {
    if (
      !mesh.geometry.isLoaded ||
      mesh.geometry.positions.length <
        3
    ) {
      continue;
    }

    const transform =
      mesh.transform;
    const matrix =
      Matrix.Compose(
        new Vector3(
          transform.scaleX,
          transform.scaleY,
          transform.scaleZ
        ),
        Quaternion
          .RotationYawPitchRoll(
            transform.rotationY *
              degreesToRadians,
            transform.rotationX *
              degreesToRadians,
            transform.rotationZ *
              degreesToRadians
          ),
        new Vector3(
          transform.positionX,
          transform.positionY,
          transform.positionZ
        )
      );

    const positions =
      mesh.geometry.positions;

    for (
      let index = 0;
      index + 2 <
        positions.length;
      index += 3
    ) {
      const point =
        Vector3.TransformCoordinates(
          new Vector3(
            positions[index],
            positions[index + 1],
            positions[index + 2]
          ),
          matrix
        );

      minimum =
        Vector3.Minimize(
          minimum,
          point
        );
      maximum =
        Vector3.Maximize(
          maximum,
          point
        );
      hasPoint = true;
    }
  }

  if (hasPoint) {
    const size =
      maximum.subtract(
        minimum
      );

    return (
      formatDimension(
        Math.abs(size.x)
      ) +
      " × " +
      formatDimension(
        Math.abs(size.y)
      ) +
      " × " +
      formatDimension(
        Math.abs(size.z)
      ) +
      " m · L×A×P"
    );
  }

  if (geometry.tree) {
    const height =
      geometry.tree.maximumHeight;
    const width =
      height *
      geometry.tree.maximumAspect;

    return (
      formatDimension(width) +
      " × " +
      formatDimension(height) +
      " m · L×A [tree]"
    );
  }

  return "Escala indisponível";
};

const getSplineDimensionLabel = (
  profile: OmsiSplineDefinition
) => {
  const points =
    profile.surfaces.flatMap(
      (surface) => [
        surface.from,
        surface.to
      ]
    );

  if (points.length === 0) {
    return "Perfil sem pontos";
  }

  const xs =
    points.map(
      (point) => point.x
    );
  const zs =
    points.map(
      (point) => point.z
    );
  const width =
    Math.max(...xs) -
    Math.min(...xs);
  const height =
    Math.max(...zs) -
    Math.min(...zs);

  return (
    "Largura " +
    formatDimension(
      Math.abs(width)
    ) +
    " m · perfil ΔZ " +
    formatDimension(
      Math.abs(height)
    ) +
    " m"
  );
};

export function AssetPreview3D(
  props: AssetPreview3DProps
) {
  const objectMode =
    props.kind === "object";

  const splineMode =
    props.kind === "spline";

  const objectGeometryByPath =
    objectMode
      ? {
          [props.assetPath]:
            props.geometry
        }
      : {};

  const splineProfilesByPath =
    splineMode
      ? {
          [props.template.splinePath]:
            props.profile
        }
      : {};

  const dimensionLabel =
    objectMode
      ? getObjectDimensionLabel(
          props.geometry
        )
      : getSplineDimensionLabel(
          props.profile
        );

  const previewSplineTemplate =
    splineMode
      ? {
          ...props.template,
          tileX: 0,
          tileY: 0,
          x: 0,
          y: 0,
          z: 0,
          rotation:
            props.rotation ?? 0,
          length:
            Math.max(
              8,
              props.length ??
                props.template.length ??
                20
            ),
          radius:
            props.radius ??
            props.template.radius ??
            0
        }
      : undefined;

  return (
    <div
      className="asset-preview-3d"
      aria-label="Prévia 3D real do item"
    >
      <Viewport
        tiles={[]}
        cameraStateKey={
          objectMode
            ? `preview-object:${props.assetPath}`
            : `preview-spline:${props.template.splinePath}`
        }
        objects={[]}
        splines={[]}
        editorTool="select"
        selectionMode="all"
        snapEnabled={false}
        moveSnap={0.5}
        rotationSnap={5}
        showGrid={true}
        showTerrain={false}
        terrainOverlays={[]}
        showObjects={objectMode}
        showSplines={splineMode}
        showSplineProfiles={true}
        showAllSplineProfiles={true}
        nightPreviewEnabled={false}
        cameraAction={{
          type: "focus",
          token: 1
        }}
        placementAssetPath={
          objectMode
            ? props.assetPath
            : undefined
        }
        placementGeometry={
          objectMode
            ? props.geometry
            : undefined
        }
        pendingPlacement={
          objectMode
            ? {
                tileX: 0,
                tileY: 0,
                x: 0,
                y: 0,
                z: 0,
                rotation: 0,
                pitch: 0,
                bank: 0
              }
            : undefined
        }
        onPlacementPoint={
          noopPlacementPoint
        }
        onThumbnailReady={
          props.onThumbnailReady
        }
        splinePlacementTemplate={
          previewSplineTemplate
        }
        splinePlacementProfile={
          splineMode
            ? props.profile
            : undefined
        }
        pendingSplinePlacement={
          previewSplineTemplate
            ? {
                targetTileX: 0,
                targetTileY: 0,
                x: 0,
                y: 0,
                z: 0,
                rotation:
                  previewSplineTemplate
                    .rotation,
                length:
                  previewSplineTemplate
                    .length,
                radius:
                  previewSplineTemplate
                    .radius,
                gradientStart: 0,
                gradientEnd: 0
              }
            : undefined
        }
        onSplinePlacementPoint={
          noopSplinePlacementPoint
        }
        activeTile={{
          x: 0,
          y: 0
        }}
        usesWorldCoordinates={false}
        selectedObject={undefined}
        selectedGeometry={undefined}
        objectGeometryByPath={
          objectGeometryByPath
        }
        textureAssetsByKey={
          props.textureAssetsByKey
        }
        splineProfilesByPath={
          splineProfilesByPath
        }
        selectedSpline={undefined}
        selectedSplineProfile={undefined}
        onSelectObject={
          noopObjectSelection
        }
        onSelectSpline={
          noopSplineSelection
        }
        onPreviewObjectTransform={
          noopObjectTransform
        }
        onPreviewSplineTransform={
          noopSplineTransform
        }
      />
      <div className="asset-preview-scale">
        <span>
          {dimensionLabel}
        </span>
      </div>
      <div className="asset-preview-3d-badge">
        3D real
      </div>
    </div>
  );
}
