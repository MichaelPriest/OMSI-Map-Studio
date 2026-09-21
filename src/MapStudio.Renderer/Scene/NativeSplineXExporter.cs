using System.Globalization;
using System.Numerics;
using System.Text;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSplineXExportResult(
    string Content,
    int SplineCount,
    int VertexCount,
    int TriangleCount,
    Vector3 Origin);

public sealed class NativeSplineXExporter
{
    public NativeSplineXExportResult Build(
        NativeSplineTriangleGeometry geometry,
        Vector3 origin,
        int splineCount)
    {
        ArgumentNullException.ThrowIfNull(
            geometry);

        if (
            geometry.Vertices.Length == 0 ||
            geometry.Vertices.Length % 3 != 0)
        {
            throw new InvalidDataException(
                "splineExportGeometryInvalid");
        }

        var vertices =
            geometry.Vertices;

        var triangleCount =
            vertices.Length /
            3;

        var builder =
            new StringBuilder(
                Math.Max(
                    4096,
                    vertices.Length *
                        80));

        builder.AppendLine(
            "xof 0303txt 0032");

        builder.AppendLine();

        builder.AppendLine(
            "// OMSI Map Studio native spline export");

        builder.AppendLine(
            FormattableString.Invariant(
                $"// Origin world: {origin.X:0.######}, {origin.Y:0.######}, {origin.Z:0.######}"));

        builder.AppendLine(
            "Mesh MapStudioSplineExport {");

        builder.Append("  ");
        builder.Append(
            vertices.Length.ToString(
                CultureInfo.InvariantCulture));
        builder.AppendLine(";");

        for (
            var index = 0;
            index < vertices.Length;
            index++)
        {
            var position =
                vertices[index]
                    .Position -
                origin;

            builder.Append("  ");
            builder.Append(
                Format(position.X));
            builder.Append(';');
            builder.Append(
                Format(position.Y));
            builder.Append(';');
            builder.Append(
                Format(position.Z));
            builder.Append(';');

            builder.AppendLine(
                index ==
                    vertices.Length -
                    1
                    ? ";"
                    : ",");
        }

        builder.Append("  ");
        builder.Append(
            triangleCount.ToString(
                CultureInfo.InvariantCulture));
        builder.AppendLine(";");

        for (
            var triangle = 0;
            triangle < triangleCount;
            triangle++)
        {
            var first =
                triangle *
                3;

            builder.Append("  3;");
            builder.Append(
                first.ToString(
                    CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(
                (first + 1)
                    .ToString(
                        CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(
                (first + 2)
                    .ToString(
                        CultureInfo.InvariantCulture));
            builder.Append(';');

            builder.AppendLine(
                triangle ==
                    triangleCount -
                    1
                    ? ";"
                    : ",");
        }

        builder.AppendLine(
            "  MeshTextureCoords {");

        builder.Append("    ");
        builder.Append(
            vertices.Length.ToString(
                CultureInfo.InvariantCulture));
        builder.AppendLine(";");

        for (
            var index = 0;
            index < vertices.Length;
            index++)
        {
            var uv =
                vertices[index]
                    .TexCoord;

            builder.Append("    ");
            builder.Append(
                Format(uv.X));
            builder.Append(';');
            builder.Append(
                Format(
                    1.0f -
                    uv.Y));
            builder.Append(';');

            builder.AppendLine(
                index ==
                    vertices.Length -
                    1
                    ? ";"
                    : ",");
        }

        builder.AppendLine(
            "  }");

        builder.AppendLine(
            "}");

        return new NativeSplineXExportResult(
            builder.ToString(),
            splineCount,
            vertices.Length,
            triangleCount,
            origin);
    }

    private static string Format(
        float value) =>
        value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);
}
