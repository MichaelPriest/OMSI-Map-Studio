export type SceneryLibraryEntry = {
  sceneryObjectPath: string;
  fileName: string;
};

export type SplineLibraryEntry = {
  splinePath: string;
  fileName: string;
};

export type OmsiTerrainGrid = {
  cellCount: number;
  heights: number[];
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
  terrainMarkerPresent: boolean;
  terrainFileExists: boolean;
  terrainFileSize: number;
  terrain?: OmsiTerrainGrid | null;
};

export type OmsiGroundTexture = {
  mainTexturePath: string;
  detailTexturePath: string;
  resolutionCode: number;
  mainTextureRepeating: number;
  detailTextureRepeating: number;
};

export type OmsiMap = {
  directoryName: string;
  displayName: string;
  directoryPath: string;
  globalConfigPath: string;
  usesWorldCoordinates: boolean;
  tiles: OmsiTile[];
  groundTextures: OmsiGroundTexture[];
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

export type OmsiTextureAsset = {
  exists: boolean;
  base64Data: string | null;
  extension: string | null;
  mimeType: string | null;
  errorCode: string | null;
};

export type OmsiSceneryMaterialOverride = {
  meshOrdinal: number;
  textureName: string;
  materialIndex: number;
  alphaMode: number | null;
  noZWrite: boolean;
  noZCheck: boolean;
  bumpMapTextureName: string | null;
  bumpMapStrength: number | null;
  nightMapTextureName: string | null;
  environmentMapTextureName: string | null;
  environmentMapStrength: number | null;
  transMapSource: string | null;
  lightMapTextureName: string | null;
  unsupportedCommands: string[];
};

export type OmsiSceneryObjectGeometry = {
  meshes: Array<{
    declaredPath: string;
    lodThreshold: number | null;
    materialOverrides:
      OmsiSceneryMaterialOverride[];
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
      type: "splineLibraryLoaded";
      entries: SplineLibraryEntry[];
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
      type: "splineDeleted";
      directoryName: string;
      splineId: number;
      deletedSplines: number;
      unlinkedSplines: number;
      filesSaved: number;
      backupDirectory: string;
    }
  | {
      type: "splineInserted";
      directoryName: string;
      backupDirectory: string;
      placedSpline: OmsiPlacedSpline;
    }
  | {
      type: "splineLinksUpdated";
      directoryName: string;
      splineId: number;
      previousSplineId: number;
      nextSplineId: number;
      linksUpdated: number;
      filesSaved: number;
      backupDirectory: string;
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
      type: "textureAssetLoaded";
      requestKey: string;
      asset: OmsiTextureAsset;
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
      type: "fullScreenChanged";
      enabled: boolean;
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

export function setFullScreen(
  enabled: boolean
) {
  getWebView()?.postMessage({
    type: "setFullScreen",
    enabled
  });
}

export function loadSceneryLibrary() {
  getWebView()?.postMessage({
    type: "loadSceneryLibrary"
  });
}

export function loadSplineLibrary() {
  getWebView()?.postMessage({
    type: "loadSplineLibrary"
  });
}

export function insertSplineFromLibrary(
  directoryName: string,
  splinePath: string,
  isHeightSpline: boolean,
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
    type: "insertSplineFromLibrary",
    directoryName,
    splinePath,
    isHeightSpline,
    ...placement
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

export function deleteSpline(
  directoryName: string,
  placedSpline: OmsiPlacedSpline
) {
  getWebView()?.postMessage({
    type: "deleteSpline",
    directoryName,
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
      placedSpline.isHeightSpline
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

export function updateSplineLinks(
  directoryName: string,
  placedSpline: OmsiPlacedSpline,
  desiredPreviousSplineId: number,
  desiredNextSplineId: number
) {
  getWebView()?.postMessage({
    type: "updateSplineLinks",
    directoryName,
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
    desiredPreviousSplineId,
    desiredNextSplineId
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

export function getGroundTextureAssetKey(
  directoryName: string,
  texturePath: string
) {
  return [
    "ground",
    directoryName,
    texturePath
  ].join("|");
}

export function getSceneryTextureAssetKey(
  sceneryObjectPath: string,
  declaredMeshPath: string,
  textureName: string
) {
  return [
    "scenery",
    sceneryObjectPath,
    declaredMeshPath,
    textureName
  ].join("|");
}

export function getSplineTextureAssetKey(
  splinePath: string,
  textureName: string
) {
  return [
    "spline",
    splinePath,
    textureName
  ].join("|");
}

export function loadGroundTextureAsset(
  directoryName: string,
  texturePath: string
) {
  const requestKey =
    getGroundTextureAssetKey(
      directoryName,
      texturePath
    );

  getWebView()?.postMessage({
    type: "loadGroundTextureAsset",
    requestKey,
    directoryName,
    texturePath
  });

  return requestKey;
}

export function loadSceneryTextureAsset(
  requestKey: string,
  sceneryObjectPath: string,
  declaredMeshPath: string,
  textureName: string
) {
  getWebView()?.postMessage({
    type: "loadSceneryTextureAsset",
    requestKey,
    sceneryObjectPath,
    declaredMeshPath,
    textureName
  });
}

export function loadSplineTextureAsset(
  requestKey: string,
  splinePath: string,
  textureName: string
) {
  getWebView()?.postMessage({
    type: "loadSplineTextureAsset",
    requestKey,
    splinePath,
    textureName
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
