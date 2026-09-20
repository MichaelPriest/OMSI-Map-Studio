import usageIcon from "./icons/usage.svg?raw";
import recentIcon from "./icons/recent.svg?raw";
import previewIcon from "./icons/preview.svg?raw";
import collectionIcon from "./icons/collection.svg?raw";
import favoriteIcon from "./icons/favorite.svg?raw";
import dragIcon from "./icons/drag.svg?raw";
import profileIcon from "./icons/profile.svg?raw";
import gridIcon from "./icons/grid.svg?raw";
import snapIcon from "./icons/snap.svg?raw";
import expandIcon from "./icons/expand.svg?raw";
import collapseIcon from "./icons/collapse.svg?raw";
import settingsIcon from "./icons/settings.svg?raw";
import toolsIcon from "./icons/tools.svg?raw";
import openIcon from "./icons/open.svg?raw";
import homeIcon from "./icons/home.svg?raw";
import icon31 from "./icons/success.svg?raw";
import icon30 from "./icons/warning.svg?raw";
import icon29 from "./icons/discard.svg?raw";
import icon28 from "./icons/dependency.svg?raw";
import icon27 from "./icons/construction-set.svg?raw";
import icon26 from "./icons/scale.svg?raw";
import icon0 from "./icons/select.svg?raw";
import icon1 from "./icons/move.svg?raw";
import icon2 from "./icons/rotate.svg?raw";
import icon3 from "./icons/focus.svg?raw";
import icon4 from "./icons/fit-view.svg?raw";
import icon5 from "./icons/explorer.svg?raw";
import icon6 from "./icons/inspector.svg?raw";
import icon7 from "./icons/undo.svg?raw";
import icon8 from "./icons/redo.svg?raw";
import icon9 from "./icons/save.svg?raw";
import icon10 from "./icons/fullscreen.svg?raw";
import icon11 from "./icons/exit-fullscreen.svg?raw";
import icon12 from "./icons/road.svg?raw";
import icon13 from "./icons/intersection.svg?raw";
import icon14 from "./icons/bridge.svg?raw";
import icon15 from "./icons/building.svg?raw";
import icon16 from "./icons/tree.svg?raw";
import icon17 from "./icons/terrain.svg?raw";
import icon18 from "./icons/transit.svg?raw";
import icon19 from "./icons/street-furniture.svg?raw";
import icon20 from "./icons/utility.svg?raw";
import icon21 from "./icons/sco-object.svg?raw";
import icon22 from "./icons/sli-spline.svg?raw";
import icon23 from "./icons/map-health.svg?raw";
import icon24 from "./icons/asset-index.svg?raw";
import icon25 from "./icons/streaming.svg?raw";
import type { CSSProperties } from "react";
import type { MapStudioIconName } from "./types";

const iconMarkupByName: Record<MapStudioIconName, string> = {
  "usage": usageIcon,
  "recent": recentIcon,
  "preview": previewIcon,
  "collection": collectionIcon,
  "favorite": favoriteIcon,
  "drag": dragIcon,
  "profile": profileIcon,
  "grid": gridIcon,
  "snap": snapIcon,
  "expand": expandIcon,
  "collapse": collapseIcon,
  "settings": settingsIcon,
  "tools": toolsIcon,
  "open": openIcon,
  "home": homeIcon,
  "success": icon31,
  "warning": icon30,
  "discard": icon29,
  "dependency": icon28,
  "construction-set": icon27,
  "scale": icon26,
  "select": icon0,
  "move": icon1,
  "rotate": icon2,
  "focus": icon3,
  "fit-view": icon4,
  "explorer": icon5,
  "inspector": icon6,
  "undo": icon7,
  "redo": icon8,
  "save": icon9,
  "fullscreen": icon10,
  "exit-fullscreen": icon11,
  "road": icon12,
  "intersection": icon13,
  "bridge": icon14,
  "building": icon15,
  "tree": icon16,
  "terrain": icon17,
  "transit": icon18,
  "street-furniture": icon19,
  "utility": icon20,
  "sco-object": icon21,
  "sli-spline": icon22,
  "map-health": icon23,
  "asset-index": icon24,
  "streaming": icon25,
};

export interface MapStudioIconProps {
  name: MapStudioIconName;
  size?: number;
  className?: string;
  label?: string;
}

export function MapStudioIcon({
  name,
  size = 20,
  className,
  label
}: MapStudioIconProps) {
  const style: CSSProperties = {
    width: size,
    height: size
  };

  return (
    <span
      className={["mapstudio-icon", className]
        .filter(Boolean)
        .join(" ")}
      style={style}
      role={label ? "img" : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
      data-mapstudio-icon={name}
      dangerouslySetInnerHTML={{
        __html: iconMarkupByName[name]
      }}
    />
  );
}
