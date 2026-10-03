using System.Globalization;

namespace MapStudio.Core.Generation.Terrain;

public sealed class MapStudioElevationGridReader
{
    private const int MaxDimension =
        2048;

    private const int MaxSamples =
        4_194_304;

    public async Task<MapStudioElevationGrid>
        ReadAsync(
            string path,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        var fullPath =
            Path.GetFullPath(
                path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Elevation grid file not found.",
                fullPath);
        }

        var lines =
            await File.ReadAllLinesAsync(
                    fullPath,
                    cancellationToken)
                .ConfigureAwait(false);

        return Path.GetExtension(
                fullPath)
            .Equals(
                ".asc",
                StringComparison.OrdinalIgnoreCase)
            ? ReadEsriAscii(lines)
            : ReadDelimited(lines);
    }

    public MapStudioElevationGrid
        ReadDelimited(
            IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(
            lines);

        var rows =
            new List<double[]>();

        foreach (var raw in lines)
        {
            var line =
                raw.Trim();

            if (
                line.Length == 0 ||
                line.StartsWith(
                    '#'))
            {
                continue;
            }

            var values =
                line.Split(
                    [
                        ',',
                        ';',
                        '\t',
                        ' '
                    ],
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions
                        .TrimEntries);

            if (values.Length < 2)
            {
                throw new InvalidDataException(
                    "elevationGridRequiresAtLeastTwoColumns");
            }

            var row =
                new double[
                    values.Length];

            for (
                var index = 0;
                index <
                    values.Length;
                index++)
            {
                if (
                    !double.TryParse(
                        values[index],
                        NumberStyles.Float,
                        CultureInfo
                            .InvariantCulture,
                        out var value) ||
                    !double.IsFinite(value))
                {
                    throw new InvalidDataException(
                        "invalidElevationGridValue");
                }

                row[index] =
                    value;
            }

            if (
                rows.Count >
                    0 &&
                rows[0].Length !=
                    row.Length)
            {
                throw new InvalidDataException(
                    "elevationGridColumnCountMismatch");
            }

            rows.Add(row);
        }

        return Build(
            rows,
            "delimited");
    }

    public MapStudioElevationGrid
        ReadEsriAscii(
            IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(
            lines);

        int? columns =
            null;

        int? rows =
            null;

        double? noData =
            null;

        var dataStart =
            -1;

        for (
            var index = 0;
            index <
                lines.Count;
            index++)
        {
            var line =
                lines[index]
                    .Trim();

            if (
                line.Length ==
                0)
            {
                continue;
            }

            var parts =
                line.Split(
                    [
                        ' ',
                        '\t'
                    ],
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions
                        .TrimEntries);

            if (
                parts.Length >=
                    2 &&
                TryReadHeader(
                    parts[0],
                    parts[1],
                    ref columns,
                    ref rows,
                    ref noData))
            {
                continue;
            }

            dataStart =
                index;

            break;
        }

        if (
            columns is null ||
            rows is null ||
            columns is < 2 ||
            rows is < 2 ||
            dataStart < 0)
        {
            throw new InvalidDataException(
                "invalidEsriAsciiElevationHeader");
        }

        ValidateDimensions(
            rows.Value,
            columns.Value);

        var parsed =
            new List<double[]>(
                rows.Value);

        for (
            var lineIndex =
                dataStart;
            lineIndex <
                lines.Count &&
            parsed.Count <
                rows.Value;
            lineIndex++)
        {
            var line =
                lines[lineIndex]
                    .Trim();

            if (
                line.Length ==
                0)
            {
                continue;
            }

            var parts =
                line.Split(
                    [
                        ' ',
                        '\t'
                    ],
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions
                        .TrimEntries);

            if (
                parts.Length !=
                columns.Value)
            {
                throw new InvalidDataException(
                    "elevationGridColumnCountMismatch");
            }

            var row =
                new double[
                    columns.Value];

            for (
                var column = 0;
                column <
                    parts.Length;
                column++)
            {
                if (
                    !double.TryParse(
                        parts[column],
                        NumberStyles.Float,
                        CultureInfo
                            .InvariantCulture,
                        out var value) ||
                    !double.IsFinite(value))
                {
                    throw new InvalidDataException(
                        "invalidElevationGridValue");
                }

                if (
                    noData is { } missing &&
                    Math.Abs(
                        value -
                        missing) <=
                    Math.Max(
                        1e-9,
                        Math.Abs(missing) *
                            1e-12))
                {
                    throw new InvalidDataException(
                        "elevationGridContainsNoData");
                }

                row[column] =
                    value;
            }

            parsed.Add(row);
        }

        if (
            parsed.Count !=
                rows.Value)
        {
            throw new InvalidDataException(
                "elevationGridRowCountMismatch");
        }

        return Build(
            parsed,
            "esri-ascii");
    }

    private static bool TryReadHeader(
        string key,
        string valueText,
        ref int? columns,
        ref int? rows,
        ref double? noData)
    {
        switch (
            key.ToLowerInvariant())
        {
            case "ncols":
                if (
                    !int.TryParse(
                        valueText,
                        NumberStyles.Integer,
                        CultureInfo
                            .InvariantCulture,
                        out var parsedColumns))
                {
                    throw new InvalidDataException(
                        "invalidEsriAsciiElevationHeader");
                }

                columns =
                    parsedColumns;

                return true;

            case "nrows":
                if (
                    !int.TryParse(
                        valueText,
                        NumberStyles.Integer,
                        CultureInfo
                            .InvariantCulture,
                        out var parsedRows))
                {
                    throw new InvalidDataException(
                        "invalidEsriAsciiElevationHeader");
                }

                rows =
                    parsedRows;

                return true;

            case "nodata_value":
                if (
                    !double.TryParse(
                        valueText,
                        NumberStyles.Float,
                        CultureInfo
                            .InvariantCulture,
                        out var parsedNoData) ||
                    !double.IsFinite(
                        parsedNoData))
                {
                    throw new InvalidDataException(
                        "invalidEsriAsciiElevationHeader");
                }

                noData =
                    parsedNoData;

                return true;

            case "xllcorner":
            case "yllcorner":
            case "xllcenter":
            case "yllcenter":
            case "cellsize":
                if (
                    !double.TryParse(
                        valueText,
                        NumberStyles.Float,
                        CultureInfo
                            .InvariantCulture,
                        out var ignored) ||
                    !double.IsFinite(
                        ignored))
                {
                    throw new InvalidDataException(
                        "invalidEsriAsciiElevationHeader");
                }

                return true;

            default:
                return false;
        }
    }

    private static MapStudioElevationGrid
        Build(
            IReadOnlyList<double[]> rows,
            string sourceFormat)
    {
        if (
            rows.Count <
                2)
        {
            throw new InvalidDataException(
                "elevationGridRequiresAtLeastTwoRows");
        }

        var columns =
            rows[0].Length;

        ValidateDimensions(
            rows.Count,
            columns);

        if (
            rows.Any(
                row =>
                    row.Length !=
                        columns))
        {
            throw new InvalidDataException(
                "elevationGridColumnCountMismatch");
        }

        var values =
            rows
                .SelectMany(
                    row =>
                        row)
                .ToArray();

        if (
            values.Length !=
                rows.Count *
                columns ||
            values.Any(
                value =>
                    !double.IsFinite(
                        value)))
        {
            throw new InvalidDataException(
                "invalidElevationGrid");
        }

        return new MapStudioElevationGrid(
            rows.Count,
            columns,
            values,
            values.Min(),
            values.Max(),
            sourceFormat);
    }

    private static void ValidateDimensions(
        int rows,
        int columns)
    {
        if (
            rows is < 2 or > MaxDimension ||
            columns is < 2 or > MaxDimension ||
            (long)rows *
                columns >
            MaxSamples)
        {
            throw new InvalidDataException(
                "invalidElevationGridDimensions");
        }
    }
}
