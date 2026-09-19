export type SceneryLibraryEntry = {
  sceneryObjectPath: string;
  fileName: string;
};

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
  sourceSectionOrdinal: number;
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
  sourceSectionOrdinal: number;
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
      type: "sceneryLibraryLoaded";
      entries: SceneryLibraryEntry[];
    }
  | {
      type: "objectInserted";
      directoryName: string;
      backupDirectory: string;
      placedObject: OmsiPlacedObject;
    }
  | {
      type: "objectTransformsSaved";
      directoryName: string;
      editsSaved: number;
      filesSaved: number;
      backupDirectory: string;
    }
  | {
      type: "objectDeleted";
      directoryName: string;
      objectId: number;
      deletedObjects: number;
      backupDirectory: string;
    }
  | {
      type: "splineInserted";
      directoryName: string;
      backupDirectory: string;
      placedSpline: OmsiPlacedSpline;
    }
  | {
      type: "splineTransformsSaved";
      directoryName: string;
      editsSaved: number;
      filesSaved: number;
      backupDirectory: string;
    }
  | {
      type: "mapFullLoadingStarted";
      directoryName: string;
      totalTiles: number;
    }
  | {
      type: "mapFullLoadingProgress";
      directoryName: string;
      completedTiles: number;
      totalTiles: number;
    }
  | {
      type: "mapFullLoaded";
      directoryName: string;
      tiles: OmsiTile[];
      objects: OmsiPlacedObject[];
      splines: OmsiPlacedSpline[];
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

export function loadSceneryLibrary() {
  getWebView()?.postMessage({
    type: "loadSceneryLibrary"
  });
}

export function insertObject(
  directoryName: string,
  sceneryObjectPath: string,
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
) {
  getWebView()?.postMessage({
    type: "insertObject",
    directoryName,
    sceneryObjectPath,
    ...placement
  });
}

export function deleteObject(
  directoryName: string,
  placedObject: OmsiPlacedObject
) {
  getWebView()?.postMessage({
    type: "deleteObject",
    directoryName,
    tileX: placedObject.tileX,
    tileY: placedObject.tileY,
    sourceSectionOrdinal:
      placedObject.sourceSectionOrdinal,
    sceneryObjectPath:
      placedObject.sceneryObjectPath,
    objectId: placedObject.objectId
  });
}

export function saveObjectTransforms(
  directoryName: string,
  edits: OmsiPlacedObject[]
) {
  getWebView()?.postMessage({
    type: "saveObjectTransforms",
    directoryName,
    edits: edits.map(
      (placedObject) => ({
        tileX: placedObject.tileX,
        tileY: placedObject.tileY,
        sourceSectionOrdinal:
          placedObject.sourceSectionOrdinal,
        sceneryObjectPath:
          placedObject.sceneryObjectPath,
        objectId:
          placedObject.objectId,
        x: placedObject.x,
        y: placedObject.y,
        z: placedObject.z,
        rotation:
          placedObject.rotation,
        pitch:
          placedObject.pitch,
        bank:
          placedObject.bank
      })
    )
  });
}

export function insertSpline(
  directoryName: string,
  sourceSpline: OmsiPlacedSpline,
  placement: {
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
  }
) {
  getWebView()?.postMessage({
    type: "insertSpline",
    directoryName,
    sourceTileX:
      sourceSpline.tileX,
    sourceTileY:
      sourceSpline.tileY,
    sourceSectionOrdinal:
      sourceSpline.sourceSectionOrdinal,
    splinePath:
      sourceSpline.splinePath,
    splineId:
      sourceSpline.splineId,
    previousSplineId:
      sourceSpline.previousSplineId,
    nextSplineId:
      sourceSpline.nextSplineId,
    isHeightSpline:
      sourceSpline.isHeightSpline,
    ...placement
  });
}

export function saveSplineTransforms(
  directoryName: string,
  edits: OmsiPlacedSpline[]
) {
  getWebView()?.postMessage({
    type: "saveSplineTransforms",
    directoryName,
    edits: edits.map(
      (placedSpline) => ({
        tileX: placedSpline.tileX,
        tileY: placedSpline.tileY,
        sourceSectionOrdinal:
          placedSpline.sourceSectionOrdinal,
        splinePath:
          placedSpline.splinePath,
        splineId:
          placedSpline.splineId,
        previousSplineId:
          placedSpline.previousSplineId,
        nextSplineId:
          placedSpline.nextSplineId,
        isHeightSpline:
          placedSpline.isHeightSpline,
        x: placedSpline.x,
        z: placedSpline.z,
        y: placedSpline.y,
        rotation:
          placedSpline.rotation,
        length:
          placedSpline.length,
        radius:
          placedSpline.radius,
        gradientStart:
          placedSpline.gradientStart,
        gradientEnd:
          placedSpline.gradientEnd
      })
    )
  });
}

export function loadMapFull(
  directoryName: string
) {
  getWebView()?.postMessage({
    type: "loadMapFull",
    directoryName
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
