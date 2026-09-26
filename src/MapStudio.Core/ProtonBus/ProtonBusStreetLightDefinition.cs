using System.Globalization;
using System.Numerics;
using System.Text;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusStreetLightReal(
    Vector3 Color,
    double Range = 15.0,
    double Intensity = 1.0);

public sealed record ProtonBusStreetLightFake(
    string TextureFileName,
    bool AlwaysFaceCamera = true,
    Vector4? Color = null,
    Vector3? TextureScale = null,
    Vector3? RotationDegrees = null);

public sealed record ProtonBusStreetLightDefinition(
    string Prefix,
    bool AlwaysOn = false,
    ProtonBusStreetLightReal? Real = null,
    ProtonBusStreetLightFake? Fake = null)
{
    public string SuggestedFileName => Prefix + ".txt";

    public string RealObjectName =>
        $"_{Prefix}_real_";

    public string FakeObjectName =>
        $"_{Prefix}_fake_";
}

public static class ProtonBusStreetLightDefinitionWriter
{
    public static string Serialize(
        ProtonBusStreetLightDefinition definition)
    {
        Validate(definition);

        var builder = new StringBuilder();

        builder.AppendLine("[streetlight]");
        builder.Append("prefix=")
            .AppendLine(definition.Prefix);
        builder.Append("alwaysOn=")
            .AppendLine(ToBoolean(definition.AlwaysOn));

        if (definition.Real is { } real)
        {
            builder.AppendLine();
            builder.AppendLine("[real]");
            builder.Append("colorR=")
                .AppendLine(Format(real.Color.X));
            builder.Append("colorG=")
                .AppendLine(Format(real.Color.Y));
            builder.Append("colorB=")
                .AppendLine(Format(real.Color.Z));
            builder.AppendLine("type=point");
            builder.Append("range=")
                .AppendLine(Format(real.Range));
            builder.Append("intensity=")
                .AppendLine(Format(real.Intensity));
        }

        if (definition.Fake is { } fake)
        {
            var color =
                fake.Color ??
                new Vector4(
                    1,
                    1,
                    1,
                    0.5f);

            var scale =
                fake.TextureScale ??
                new Vector3(
                    10,
                    10,
                    10);

            var rotation =
                fake.RotationDegrees ??
                Vector3.Zero;

            builder.AppendLine();
            builder.AppendLine("[fake]");
            builder.AppendLine("shader=additive");
            builder.Append("texture=")
                .AppendLine(fake.TextureFileName);
            builder.Append("alwaysFaceCamera=")
                .AppendLine(
                    ToBoolean(
                        fake.AlwaysFaceCamera));
            builder.Append("colorR=")
                .AppendLine(Format(color.X));
            builder.Append("colorG=")
                .AppendLine(Format(color.Y));
            builder.Append("colorB=")
                .AppendLine(Format(color.Z));
            builder.Append("colorA=")
                .AppendLine(Format(color.W));
            builder.Append("texScaleX=")
                .AppendLine(Format(scale.X));
            builder.Append("texScaleY=")
                .AppendLine(Format(scale.Y));
            builder.Append("texScaleZ=")
                .AppendLine(Format(scale.Z));
            builder.Append("rotX=")
                .AppendLine(Format(rotation.X));
            builder.Append("rotY=")
                .AppendLine(Format(rotation.Y));
            builder.Append("rotZ=")
                .AppendLine(Format(rotation.Z));
        }

        return builder.ToString();
    }

    public static void Validate(
        ProtonBusStreetLightDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        ValidatePrefix(
            definition.Prefix);

        if (
            definition.Real is null &&
            definition.Fake is null)
        {
            throw new ArgumentException(
                "A Proton Bus street light must define a real light, a fake light, or both.",
                nameof(definition));
        }

        if (definition.Real is { } real)
        {
            ValidateColor(
                new Vector4(
                    real.Color,
                    1),
                "real");

            ValidatePositiveFinite(
                real.Range,
                "real.range");

            ValidateUnit(
                real.Intensity,
                "real.intensity");
        }

        if (definition.Fake is { } fake)
        {
            ValidateTextureName(
                fake.TextureFileName);

            ValidateColor(
                fake.Color ??
                new Vector4(
                    1,
                    1,
                    1,
                    0.5f),
                "fake");

            var scale =
                fake.TextureScale ??
                new Vector3(
                    10,
                    10,
                    10);

            if (
                !float.IsFinite(scale.X) ||
                !float.IsFinite(scale.Y) ||
                !float.IsFinite(scale.Z) ||
                scale.X <= 0 ||
                scale.Y <= 0 ||
                scale.Z <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    "Fake light texture scales must be finite and greater than zero.");
            }

            var rotation =
                fake.RotationDegrees ??
                Vector3.Zero;

            if (
                !float.IsFinite(rotation.X) ||
                !float.IsFinite(rotation.Y) ||
                !float.IsFinite(rotation.Z))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    "Fake light rotation must contain finite values.");
            }
        }
    }

    private static void ValidateTextureName(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        if (
            !string.Equals(
                Path.GetFileName(value),
                value,
                StringComparison.Ordinal) ||
            !value.EndsWith(
                ".png",
                StringComparison.OrdinalIgnoreCase) ||
            value.Any(
                character =>
                    character > 127 ||
                    !(
                        char.IsAsciiLetterOrDigit(character) ||
                        character is
                            '_' or
                            '-' or
                            '.' or
                            ' '
                    )))
        {
            throw new ArgumentException(
                "Street-light fake texture must be a portable PNG file name.",
                nameof(value));
        }
    }

    private static void ValidateColor(
        Vector4 color,
        string field)
    {
        ValidateUnit(
            color.X,
            field + ".colorR");
        ValidateUnit(
            color.Y,
            field + ".colorG");
        ValidateUnit(
            color.Z,
            field + ".colorB");
        ValidateUnit(
            color.W,
            field + ".colorA");
    }

    private static void ValidateUnit(
        double value,
        string field)
    {
        if (
            !double.IsFinite(value) ||
            value < 0 ||
            value > 1)
        {
            throw new ArgumentOutOfRangeException(
                field,
                "Value must be finite and between 0 and 1.");
        }
    }

    private static void ValidatePositiveFinite(
        double value,
        string field)
    {
        if (
            !double.IsFinite(value) ||
            value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                field,
                "Value must be finite and greater than zero.");
        }
    }

    private static void ValidatePrefix(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        if (
            value.Any(
                character =>
                    !(
                        char.IsAsciiLetterOrDigit(character) ||
                        character is
                            '_' or
                            '-'
                    )))
        {
            throw new ArgumentException(
                "Street-light prefix must use only ASCII letters, numbers, underscore or hyphen.",
                nameof(value));
        }
    }

    private static string ToBoolean(
        bool value) =>
        value
            ? "1"
            : "0";

    private static string Format(
        double value) =>
        value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);
}
