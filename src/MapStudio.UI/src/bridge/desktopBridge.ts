export type OmsiTile = {
  x: number;
  y: number;
  relativeMapPath: string;
  detailsLoaded: boolean;
  fileExists: boolean;
  objectCount: number;
  splineCount: number;
  splineAttachmentCount: number;
};

export type OmsiMap = {
  directoryName: string;
  displayName: string;
  directoryPath: string;
  globalConfigPath: string;
  usesWorldCoordinates: boolean;
  tiles: OmsiTile[];
};

export type OmsiPlacedObject = {
  tileX: number;
  tileY: number;
  headerValue: string;
  sceneryObjectPath: string;
  objectId: number;
  x: number;
  y: number;
  z: number;
  rotation: number;
  pitch: number;
  bank: number;
};

export type OmsiPlacedSpline = {
  tileX: number;
  tileY: number;
  headerValue: string;
  splinePath: string;
  splineId: number;
  previousSplineId: number;
  nextSplineId: number;
  x: number;
  z: number;
  y: number;
  rotation: number;
  length: number;
  radius: number;
  gradientStart: number;
  gradientEnd: number;
  isHeightSpline: boolean;
};

export type OmsiSplineProfilePoint = {
  x: number;
  z: number;
  textureX: number;
  textureScale: number;
};

export type OmsiSplineSurface = {
  textureIndex: number;
  textureName: string | null;
  from: OmsiSplineProfilePoint;
  to: OmsiSplineProfilePoint;
};

export type OmsiSplineDefinition = {
  exists: boolean;
  textures: string[];
  surfaces: OmsiSplineSurface[];
};

export type OmsiO3dHeader = {
  exists: boolean;
  isValid: boolean;
  version: number | null;
  hasExtendedHeader: boolean;
  usesLongTriangleIndices: boolean;
  usesAlternativeEncryptionSeed: boolean;
  isEncrypted: boolean;
};

export type OmsiO3dStructureSummary = {
  isParsed: boolean;
  vertexCount: number;
  triangleCount: number;
  materialCount: number;
  boneCount: number;
  hasTransform: boolean;
  errorCode: string | null;
};

export type OmsiSceneryMeshReference = {
  declaredPath: string;
  fileExists: boolean;
  o3d: OmsiO3dHeader | null;
  structure: OmsiO3dStructureSummary | null;
};

export type OmsiSceneryObjectMetadata = {
  exists: boolean;
  friendlyName: string | null;
  groups: string[];
  meshes: OmsiSceneryMeshReference[];
  collisionMeshes: OmsiSceneryMeshReference[];
};

export type OmsiO3dMaterial = {
  diffuseR: number;
  diffuseG: number;
  diffuseB: number;
  diffuseA: number;
  specularR: number;
  specularG: number;
  specularB: number;
  emissionR: number;
  emissionG: number;
  emissionB: number;
  specularPower: number;
  textureName: string | null;
};

export type OmsiO3dGeometry = {
  isLoaded: boolean;
  errorCode: string | null;
  positions: number[];
  normals: number[];
  uvs: number[];
  indices: number[];
  triangleMaterialIndices: number[];
  materials: OmsiO3dMaterial[];
};

export type OmsiSceneryObjectGeometry = {
  meshes: Array<{
    declaredPath: string;
    geometry: OmsiO3dGeometry;
  }>;
};

export type HostMessage =
  | {
      type: "omsiRootSelected";
      rootPath: string;
    }
  | {
      type: "mapOpened";
      initialTile: {
        x: number;
        y: number;
      } | null;
      map: OmsiMap;
    }
  | {
      type: "selectionCancelled";
      target: "omsi" | "map";
    }
  | {
      type: "mapRegionLoaded";
      directoryName: string;
      centerX: number;
      centerY: number;
      radius: number;
      tiles: OmsiTile[];
      objects: OmsiPlacedObject[];
      splines: OmsiPlacedSpline[];
    }
  | {
      type: "splineProfileLoaded";
      splinePath: string;
      definition: OmsiSplineDefinition;
    }
  | {
      type: "sceneryObjectMetadataLoaded";
      sceneryObjectPath: string;
      metadata: OmsiSceneryObjectMetadata;
    }
  | {
      type: "sceneryObjectGeometryLoaded";
      sceneryObjectPath: string;
      geometry: OmsiSceneryObjectGeometry;
    }
  | {
      type: "hostError";
      code: string;
      detail?: string;
    };

type DesktopWebView = {
  postMessage: (message: unknown) => void;
  addEventListener: (
    type: "message",
    listener: (event: MessageEvent<HostMessage>) => void
  ) => void;
  removeEventListener: (
    type: "message",
    listener: (event: MessageEvent<HostMessage>) => void
  ) => void;
};

function getWebView(): DesktopWebView | undefined {
  const hostWindow = window as Window & {
    chrome?: {
      webview?: DesktopWebView;
    };
  };

  return hostWindow.chrome?.webview;
}

export function isDesktopBridgeAvailable() {
  return Boolean(getWebView());
}

export function selectOmsiRoot() {
  getWebView()?.postMessage({
    type: "selectOmsiRoot"
  });
}

export function selectMap() {
  getWebView()?.postMessage({
    type: "selectMap"
  });
}

export function loadMapRegion(
  directoryName: string,
  centerX: number,
  centerY: number,
  radius: number
) {
  getWebView()?.postMessage({
    type: "loadMapRegion",
    directoryName,
    centerX,
    centerY,
    radius
  });
}

export function loadSplineProfile(
  splinePath: string
) {
  getWebView()?.postMessage({
    type: "loadSplineProfile",
    splinePath
  });
}

export function loadSceneryObjectMetadata(
  sceneryObjectPath: string
) {
  getWebView()?.postMessage({
    type: "loadSceneryObjectMetadata",
    sceneryObjectPath
  });
}

export function loadSceneryObjectGeometry(
  sceneryObjectPath: string
) {
  getWebView()?.postMessage({
    type: "loadSceneryObjectGeometry",
    sceneryObjectPath
  });
}

export function subscribeToHost(
  handler: (message: HostMessage) => void
): () => void {
  const webView = getWebView();

  if (!webView) {
    return () => undefined;
  }

  const listener = (
    event: MessageEvent<HostMessage>
  ) => handler(event.data);

  webView.addEventListener(
    "message",
    listener
  );

  return () =>
    webView.removeEventListener(
      "message",
      listener
    );
}
