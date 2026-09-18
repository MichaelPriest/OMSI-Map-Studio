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
      type: "omsiInstallationLoadingStarted";
      rootPath: string;
    }
  | {
      type: "omsiInstallationLoadingProgress";
      rootPath: string;
      completed: number;
      total: number;
      skipped: number;
      directoryName: string | null;
    }
  | {
      type: "omsiInstallationLoaded";
      rootPath: string;
      skippedMaps: number;
      maps: OmsiMap[];
    }
  | {
      type: "selectionCancelled";
    }
  | {
      type: "mapContentLoaded";
      directoryName: string;
      usesWorldCoordinates: boolean;
      tiles: OmsiTile[];
      objects: OmsiPlacedObject[];
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
      code:
        | "invalidMessage"
        | "invalidOmsiRoot"
        | "accessDenied"
        | "ioError"
        | "unknownMap"
        | "unknownSceneryObject"
        | "invalidSceneryObjectPath"
        | string;
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

export function loadMapContent(
  directoryName: string
) {
  getWebView()?.postMessage({
    type: "loadMapContent",
    directoryName
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
