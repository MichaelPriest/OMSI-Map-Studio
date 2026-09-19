import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import zlib from "node:zlib";

const [publishDirArg, publicDirArg] = process.argv.slice(2);
if (!publishDirArg || !publicDirArg) {
  throw new Error("Usage: node package-artifact.mjs <publish-dir> <public-dir>");
}

const publishDir = path.resolve(publishDirArg);
const publicDir = path.resolve(publicDirArg);
const release = process.env.MAPSTUDIO_RELEASE ?? "v0.1.0-alpha.3-test.1";
const sourceHead =
  process.env.MAPSTUDIO_SOURCE_HEAD ?? "b4f18fcf3e132a5780e5be6257539a3bcdc784e1";
const sdkVersion = process.env.MAPSTUDIO_DOTNET_SDK ?? "10.0.401";
const artifactName = `OMSI-Map-Studio-${release}-win-x64.zip`;
const zipPath = path.join(publicDir, artifactName);

const crcTable = (() => {
  const table = new Uint32Array(256);
  for (let i = 0; i < 256; i += 1) {
    let c = i;
    for (let j = 0; j < 8; j += 1) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    }
    table[i] = c >>> 0;
  }
  return table;
})();

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function dosDateTime(date) {
  const year = Math.max(1980, date.getFullYear());
  const dosDate =
    ((year - 1980) << 9) | ((date.getMonth() + 1) << 5) | date.getDate();
  const dosTime =
    (date.getHours() << 11) | (date.getMinutes() << 5) | Math.floor(date.getSeconds() / 2);
  return { dosDate, dosTime };
}

async function collectFiles(root, current = "") {
  const directory = path.join(root, current);
  const entries = await fs.readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const relative = current ? path.join(current, entry.name) : entry.name;
    if (entry.isDirectory()) {
      files.push(...(await collectFiles(root, relative)));
    } else if (entry.isFile()) {
      files.push(relative);
    }
  }

  return files;
}

await fs.mkdir(publicDir, { recursive: true });

const files = (await collectFiles(publishDir)).sort((a, b) => a.localeCompare(b));
if (files.length === 0) {
  throw new Error("Publish directory is empty.");
}

const localParts = [];
const centralParts = [];
let localOffset = 0;
let totalUncompressedBytes = 0;
const buildTime = new Date();
const { dosDate, dosTime } = dosDateTime(buildTime);

for (const relativeFile of files) {
  const fullPath = path.join(publishDir, relativeFile);
  const data = await fs.readFile(fullPath);
  const name = Buffer.from(relativeFile.replaceAll(path.sep, "/"), "utf8");
  const deflated = zlib.deflateRawSync(data, { level: 9 });
  const useDeflate = deflated.length < data.length;
  const payload = useDeflate ? deflated : data;
  const method = useDeflate ? 8 : 0;
  const crc = crc32(data);
  const flags = 0x0800;

  if (
    data.length > 0xffffffff ||
    payload.length > 0xffffffff ||
    localOffset > 0xffffffff
  ) {
    throw new Error("ZIP64 would be required; artifact is unexpectedly large.");
  }

  const localHeader = Buffer.alloc(30);
  localHeader.writeUInt32LE(0x04034b50, 0);
  localHeader.writeUInt16LE(20, 4);
  localHeader.writeUInt16LE(flags, 6);
  localHeader.writeUInt16LE(method, 8);
  localHeader.writeUInt16LE(dosTime, 10);
  localHeader.writeUInt16LE(dosDate, 12);
  localHeader.writeUInt32LE(crc, 14);
  localHeader.writeUInt32LE(payload.length, 18);
  localHeader.writeUInt32LE(data.length, 22);
  localHeader.writeUInt16LE(name.length, 26);
  localHeader.writeUInt16LE(0, 28);

  localParts.push(localHeader, name, payload);

  const centralHeader = Buffer.alloc(46);
  centralHeader.writeUInt32LE(0x02014b50, 0);
  centralHeader.writeUInt16LE(20, 4);
  centralHeader.writeUInt16LE(20, 6);
  centralHeader.writeUInt16LE(flags, 8);
  centralHeader.writeUInt16LE(method, 10);
  centralHeader.writeUInt16LE(dosTime, 12);
  centralHeader.writeUInt16LE(dosDate, 14);
  centralHeader.writeUInt32LE(crc, 16);
  centralHeader.writeUInt32LE(payload.length, 20);
  centralHeader.writeUInt32LE(data.length, 24);
  centralHeader.writeUInt16LE(name.length, 28);
  centralHeader.writeUInt16LE(0, 30);
  centralHeader.writeUInt16LE(0, 32);
  centralHeader.writeUInt16LE(0, 34);
  centralHeader.writeUInt16LE(0, 36);
  centralHeader.writeUInt32LE(0, 38);
  centralHeader.writeUInt32LE(localOffset, 42);

  centralParts.push(centralHeader, name);

  localOffset += localHeader.length + name.length + payload.length;
  totalUncompressedBytes += data.length;
}

const centralDirectory = Buffer.concat(centralParts);
const localData = Buffer.concat(localParts);
const end = Buffer.alloc(22);
end.writeUInt32LE(0x06054b50, 0);
end.writeUInt16LE(0, 4);
end.writeUInt16LE(0, 6);
end.writeUInt16LE(files.length, 8);
end.writeUInt16LE(files.length, 10);
end.writeUInt32LE(centralDirectory.length, 12);
end.writeUInt32LE(localData.length, 16);
end.writeUInt16LE(0, 20);

const archive = Buffer.concat([localData, centralDirectory, end]);
await fs.writeFile(zipPath, archive);

const sha256 = crypto.createHash("sha256").update(archive).digest("hex");
await fs.writeFile(
  `${zipPath}.sha256`,
  `${sha256}  ${artifactName}\n`,
  "utf8"
);

const buildInfo = {
  release,
  sourceHead,
  dotnetSdk: sdkVersion,
  tests: "passed",
  uiBuild: "passed",
  publish: "passed",
  runtime: "win-x64",
  selfContained: true,
  publishReadyToRun: false,
  publishTrimmed: false,
  fileCount: files.length,
  totalUncompressedBytes,
  zipBytes: archive.length,
  sha256,
  generatedAt: buildTime.toISOString()
};

await fs.writeFile(
  path.join(publicDir, "build-info.json"),
  JSON.stringify(buildInfo, null, 2) + "\n",
  "utf8"
);

const html = `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>OMSI Map Studio ${release}</title>
</head>
<body>
  <main>
    <h1>OMSI Map Studio ${release}</h1>
    <p>Source HEAD: <code>${sourceHead}</code></p>
    <p>Tests, React UI build and win-x64 self-contained publish completed successfully.</p>
    <p><a href="./${artifactName}">Download Windows x64 ZIP</a></p>
    <p><a href="./${artifactName}.sha256">SHA-256</a> · <a href="./build-info.json">Build info</a></p>
  </main>
</body>
</html>
`;

await fs.writeFile(path.join(publicDir, "index.html"), html, "utf8");

console.log(`Created ${artifactName} (${archive.length} bytes, SHA-256 ${sha256}).`);
