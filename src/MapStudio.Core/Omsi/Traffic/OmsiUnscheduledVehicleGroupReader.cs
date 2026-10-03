using System.Globalization;
using System.Text;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Traffic;

public sealed class OmsiUnscheduledVehicleGroupReader
{
    public async Task<IReadOnlyList<
        OmsiUnscheduledVehicleGroup>> ReadMapAsync(
        string mapDirectory,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        var path =
            Path.Combine(
                Path.GetFullPath(
                    mapDirectory),
                "unsched_vehgroups.txt");

        if (!File.Exists(path))
        {
            return Array.Empty<
                OmsiUnscheduledVehicleGroup>();
        }

        return await ReadAsync(
            path,
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<
        OmsiUnscheduledVehicleGroup>> ReadAsync(
        string filePath,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        var bytes =
            await File.ReadAllBytesAsync(
                filePath,
                cancellationToken)
                .ConfigureAwait(false);

        var document =
            OmsiConfigParser.ParseBytes(
                bytes);

        var result =
            new List<
                OmsiUnscheduledVehicleGroup>();

        foreach (
            var section in
                document.FindSections(
                    "group"))
        {
            var values =
                section.DataLines
                    .Take(2)
                    .ToArray();

            if (
                values.Length < 2 ||
                string.IsNullOrWhiteSpace(
                    values[0]) ||
                !int.TryParse(
                    values[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var density) ||
                density < 0)
            {
                continue;
            }

            result.Add(
                new OmsiUnscheduledVehicleGroup(
                    values[0].Trim(),
                    density));
        }

        return result;
    }
}
