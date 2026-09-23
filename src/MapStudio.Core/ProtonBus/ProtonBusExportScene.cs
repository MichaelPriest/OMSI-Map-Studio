using System.Numerics;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusExportVertex(
    Vector3 Position,
    Vector2 TextureCoordinate);

public sealed record ProtonBusExportTriangle(
    int A,
    int B,
    int C,
    string? MaterialName = null);

public sealed record ProtonBusExportMaterial(
    string Name,
    string? TextureFileName = null,
    bool Transparent = false,
    bool Emissive = false,
    bool Additive = false);

public sealed record ProtonBusExportMesh(
    string Name,
    IReadOnlyList<ProtonBusExportVertex>
        Vertices,
    IReadOnlyList<ProtonBusExportTriangle>
        Triangles,
    IReadOnlyList<ProtonBusExportMaterial>
        Materials);

public sealed record ProtonBusExportScene(
    IReadOnlyList<ProtonBusExportMesh>
        Meshes);

public sealed record ProtonBus3dsMeshChunk(
    string Name,
    IReadOnlyList<ProtonBusExportVertex>
        Vertices,
    IReadOnlyList<ProtonBusExportTriangle>
        Triangles,
    IReadOnlyList<ProtonBusExportMaterial>
        Materials);

public static class ProtonBus3dsMeshChunkPlanner
{
    public const int DefaultMaxVertices =
        65535;

    public const int DefaultMaxFaces =
        65535;

    public static IReadOnlyList<
        ProtonBus3dsMeshChunk>
        Plan(
            ProtonBusExportMesh mesh,
            int maxVertices =
                DefaultMaxVertices,
            int maxFaces =
                DefaultMaxFaces)
    {
        ArgumentNullException.ThrowIfNull(
            mesh);

        if (
            maxVertices <
            3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxVertices),
                "A 3DS mesh chunk must allow at least three vertices.");
        }

        if (
            maxFaces <
            1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFaces),
                "A 3DS mesh chunk must allow at least one face.");
        }

        ValidateMesh(
            mesh);

        if (
            mesh.Triangles.Count ==
            0)
        {
            return
            [
                new(
                    mesh.Name,
                    mesh.Vertices,
                    [],
                    mesh.Materials)
            ];
        }

        var chunks =
            new List<
                ProtonBus3dsMeshChunk>();

        var vertexMap =
            new Dictionary<int, int>();

        var vertices =
            new List<
                ProtonBusExportVertex>();

        var triangles =
            new List<
                ProtonBusExportTriangle>();

        void Flush()
        {
            if (
                triangles.Count ==
                0)
            {
                return;
            }

            var index =
                chunks.Count +
                1;

            chunks.Add(
                new(
                    $"{mesh.Name}_{index:0000}",
                    vertices.ToArray(),
                    triangles.ToArray(),
                    mesh.Materials));

            vertexMap.Clear();
            vertices.Clear();
            triangles.Clear();
        }

        foreach (
            var triangle
            in mesh.Triangles)
        {
            var sourceIndices =
                new[]
                {
                    triangle.A,
                    triangle.B,
                    triangle.C
                };

            var additionalVertices =
                sourceIndices
                    .Distinct()
                    .Count(
                        sourceIndex =>
                            !vertexMap.ContainsKey(
                                sourceIndex));

            if (
                triangles.Count >=
                    maxFaces ||
                vertices.Count +
                    additionalVertices >
                    maxVertices)
            {
                Flush();
            }

            var local =
                new int[3];

            for (
                var index = 0;
                index <
                sourceIndices.Length;
                index++)
            {
                var sourceIndex =
                    sourceIndices[
                        index];

                if (
                    !vertexMap.TryGetValue(
                        sourceIndex,
                        out var localIndex))
                {
                    localIndex =
                        vertices.Count;

                    vertexMap[
                        sourceIndex] =
                        localIndex;

                    vertices.Add(
                        mesh.Vertices[
                            sourceIndex]);
                }

                local[
                    index] =
                    localIndex;
            }

            triangles.Add(
                new(
                    local[0],
                    local[1],
                    local[2],
                    triangle
                        .MaterialName));
        }

        Flush();

        if (
            chunks.Count ==
            1)
        {
            var only =
                chunks[0];

            chunks[0] =
                only with
                {
                    Name =
                        mesh.Name
                };
        }

        return chunks;
    }

    private static void ValidateMesh(
        ProtonBusExportMesh mesh)
    {
        if (
            string.IsNullOrWhiteSpace(
                mesh.Name))
        {
            throw new ArgumentException(
                "Export mesh name cannot be empty.",
                nameof(mesh));
        }

        foreach (
            var triangle
            in mesh.Triangles)
        {
            ValidateIndex(
                triangle.A,
                mesh.Vertices.Count,
                mesh.Name);

            ValidateIndex(
                triangle.B,
                mesh.Vertices.Count,
                mesh.Name);

            ValidateIndex(
                triangle.C,
                mesh.Vertices.Count,
                mesh.Name);
        }
    }

    private static void ValidateIndex(
        int index,
        int vertexCount,
        string meshName)
    {
        if (
            index >=
                0 &&
            index <
                vertexCount)
        {
            return;
        }

        throw new ArgumentException(
            $"Mesh '{meshName}' contains triangle index {index} outside the vertex range 0..{Math.Max(0, vertexCount - 1)}.");
    }
}
