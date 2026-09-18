import { useEffect, useRef } from "react";
import { ArcRotateCamera } from "@babylonjs/core/Cameras/arcRotateCamera";
import { Engine } from "@babylonjs/core/Engines/engine";
import { HemisphericLight } from "@babylonjs/core/Lights/hemisphericLight";
import { StandardMaterial } from "@babylonjs/core/Materials/standardMaterial";
import { Color3, Vector3 } from "@babylonjs/core/Maths/math";
import { MeshBuilder } from "@babylonjs/core/Meshes/meshBuilder";
import { Scene } from "@babylonjs/core/scene";
import type {
  OmsiPlacedObject,
  OmsiTile
} from "../bridge/desktopBridge";

type ViewportProps = {
  tiles: OmsiTile[];
  objects: OmsiPlacedObject[];
  usesWorldCoordinates: boolean;
};

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

function createObjectMarkerLines(objects: OmsiPlacedObject[]) {
  const markerRadius = 1.5;
  const markerHeight = 3;

  return objects.flatMap((placedObject) => {
    const x = placedObject.tileX * 300 + placedObject.x;
    const z = placedObject.tileY * 300 + placedObject.y;
    const y = placedObject.z;

    return [
      [
        new Vector3(x - markerRadius, y, z),
        new Vector3(x + markerRadius, y, z)
      ],
      [
        new Vector3(x, y, z - markerRadius),
        new Vector3(x, y, z + markerRadius)
      ],
      [
        new Vector3(x, y, z),
        new Vector3(x, y + markerHeight, z)
      ]
    ];
  });
}

export function Viewport({
  tiles,
  objects,
  usesWorldCoordinates
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

    if (tiles.length) {
      const existingLines = tiles
        .filter((tile) => tile.fileExists)
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
        .filter((tile) => !tile.fileExists)
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

    engine.runRenderLoop(() => scene.render());

    const resize = () => engine.resize();
    window.addEventListener("resize", resize);

    return () => {
      window.removeEventListener("resize", resize);
      scene.dispose();
      engine.dispose();
    };
  }, [tiles, objects, usesWorldCoordinates]);

  return <canvas ref={canvasRef} className="viewport-canvas" />;
}
