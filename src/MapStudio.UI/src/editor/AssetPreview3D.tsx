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
      <div className="asset-preview-3d-badge">
        3D real
      </div>
    </div>
  );
}
