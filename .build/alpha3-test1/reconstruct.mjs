import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const buildDir = path.dirname(fileURLToPath(import.meta.url));
const manifest = JSON.parse(await fs.readFile(path.join(buildDir, "manifest.json"), "utf8"));
const outDir = path.join(buildDir, "worktree");
const expected = new Map(manifest.files.map((file) => [file.path, file]));

function safeRelativePath(value) {
  if (typeof value !== "string" || value.length === 0) {
    throw new Error("Invalid empty path in chunk.");
  }

  const unix = value.replaceAll("\\", "/");
  const normalized = path.posix.normalize(unix);

  if (
    normalized === ".." ||
    normalized.startsWith("../") ||
    normalized.startsWith("/") ||
    normalized.includes("/../")
  ) {
    throw new Error(`Unsafe path in chunk: ${value}`);
  }

  return normalized;
}

function gitBlobSha(data) {
  const header = Buffer.from(`blob ${data.length}\\0`, "utf8");
  return crypto.createHash("sha1").update(header).update(data).digest("hex");
}

await fs.rm(outDir, { recursive: true, force: true });
await fs.mkdir(outDir, { recursive: true });

const seen = new Set();

for (let chunkNumber = 1; chunkNumber <= manifest.chunkCount; chunkNumber += 1) {
  const chunkPath = path.join(buildDir, `chunk${chunkNumber}.json`);
  const entries = JSON.parse(await fs.readFile(chunkPath, "utf8"));

  if (!Array.isArray(entries)) {
    throw new Error(`Chunk ${chunkNumber} is not an array.`);
  }

  for (const entry of entries) {
    const relativePath = safeRelativePath(entry.path);
    const expectedFile = expected.get(relativePath);

    if (!expectedFile) {
      throw new Error(`Unexpected file in chunk ${chunkNumber}: ${relativePath}`);
    }

    if (expectedFile.chunk !== chunkNumber) {
      throw new Error(
        `File ${relativePath} belongs to chunk ${expectedFile.chunk}, not chunk ${chunkNumber}.`
      );
    }

    if (seen.has(relativePath)) {
      throw new Error(`Duplicate file in chunks: ${relativePath}`);
    }

    let data;
    if (entry.encoding === "utf-8") {
      data = Buffer.from(entry.data, "utf8");
    } else if (entry.encoding === "base64") {
      data = Buffer.from(entry.data, "base64");
    } else {
      throw new Error(`Unsupported encoding for ${relativePath}: ${entry.encoding}`);
    }

    if (data.length !== expectedFile.size) {
      throw new Error(
        `Size mismatch for ${relativePath}: expected ${expectedFile.size}, got ${data.length}`
      );
    }

    const actualSha = gitBlobSha(data);
    if (actualSha !== expectedFile.gitBlobSha) {
      throw new Error(
        `Git blob SHA mismatch for ${relativePath}: expected ${expectedFile.gitBlobSha}, got ${actualSha}`
      );
    }

    const destination = path.join(outDir, ...relativePath.split("/"));
    await fs.mkdir(path.dirname(destination), { recursive: true });
    await fs.writeFile(destination, data);
    seen.add(relativePath);
  }
}

if (seen.size !== manifest.fileCount) {
  throw new Error(`Expected ${manifest.fileCount} files, reconstructed ${seen.size}.`);
}

for (const expectedPath of expected.keys()) {
  if (!seen.has(expectedPath)) {
    throw new Error(`Missing reconstructed file: ${expectedPath}`);
  }
}

await fs.writeFile(
  path.join(buildDir, "reconstruction-result.json"),
  JSON.stringify(
    {
      sourceHead: manifest.sourceHead,
      release: manifest.release,
      reconstructedFiles: seen.size,
      verified: true
    },
    null,
    2
  ) + "\n",
  "utf8"
);

console.log(
  `Reconstructed and Git-SHA verified ${seen.size} files from ${manifest.chunkCount} chunks for ${manifest.sourceHead}.`
);
