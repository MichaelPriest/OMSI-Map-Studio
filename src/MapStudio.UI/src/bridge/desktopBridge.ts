export type OmsiTile = {
  x: number;
  y: number;
  relativeMapPath: string;
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

export type HostMessage =
  | {
      type: "omsiInstallationLoaded";
      rootPath: string;
      maps: OmsiMap[];
    }
  | {
      type: "mapObjectsLoaded";
      directoryName: string;
      usesWorldCoordinates: boolean;
      objects: OmsiPlacedObject[];
    }
  | {
      type: "hostError";
      code:
        | "invalidMessage"
        | "invalidOmsiRoot"
        | "accessDenied"
        | "ioError"
        | "unknownMap"
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
  getWebView()?.postMessage({ type: "selectOmsiRoot" });
}

export function loadMapObjects(directoryName: string) {
  getWebView()?.postMessage({
    type: "loadMapObjects",
    directoryName
  });
}

export function subscribeToHost(
  handler: (message: HostMessage) => void
): () => void {
  const webView = getWebView();

  if (!webView) {
    return () => undefined;
  }

  const listener = (event: MessageEvent<HostMessage>) => handler(event.data);
  webView.addEventListener("message", listener);

  return () => webView.removeEventListener("message", listener);
}
