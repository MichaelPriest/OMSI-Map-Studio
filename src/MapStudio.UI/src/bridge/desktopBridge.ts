export type OmsiTile = {
  x: number;
  y: number;
  relativeMapPath: string;
};

export type OmsiMap = {
  directoryName: string;
  displayName: string;
  directoryPath: string;
  globalConfigPath: string;
  tiles: OmsiTile[];
};

export type HostMessage =
  | {
      type: "omsiInstallationLoaded";
      rootPath: string;
      maps: OmsiMap[];
    }
  | {
      type: "hostError";
      code: "invalidMessage" | "invalidOmsiRoot" | "accessDenied" | "ioError" | string;
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
