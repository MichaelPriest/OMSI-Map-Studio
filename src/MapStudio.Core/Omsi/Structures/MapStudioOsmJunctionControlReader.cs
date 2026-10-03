using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Omsi.Structures;

public enum MapStudioOsmJunctionControlKind
{
    TrafficSignal,
    Crosswalk
}

public sealed record MapStudioOsmJunctionControl(
    long NodeId,
    MapStudioOsmJunctionControlKind Kind,
    MapStudioRoadPoint WorldPoint);

public sealed class MapStudioOsmJunctionControlReader
{
    public IReadOnlyList<MapStudioOsmJunctionControl> Parse(
        string osmXml,
        MapStudioGeographicAnchor anchor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(osmXml);

        using var textReader =
            new StringReader(
                osmXml);

        using var reader =
            XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    DtdProcessing =
                        DtdProcessing.Prohibit,
                    XmlResolver =
                        null,
                    MaxCharactersInDocument =
                        128L *
                        1024 *
                        1024
                });

        var document =
            XDocument.Load(
                reader,
                LoadOptions.None);

        var root =
            document.Root;

        if (
            root is null ||
            !string.Equals(
                root.Name.LocalName,
                "osm",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "osmRootInvalid");
        }

        var result =
            new List<MapStudioOsmJunctionControl>();

        foreach (
            var node in
                root.Elements()
                    .Where(
                        element =>
                            element.Name.LocalName ==
                            "node"))
        {
            if (
                !long.TryParse(
                    node.Attribute(
                        "id")?.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var nodeId) ||
                !double.TryParse(
                    node.Attribute(
                        "lat")?.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var latitude) ||
                !double.TryParse(
                    node.Attribute(
                        "lon")?.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var longitude) ||
                !double.IsFinite(
                    latitude) ||
                !double.IsFinite(
                    longitude))
            {
                continue;
            }

            var tags =
                node.Elements()
                    .Where(
                        element =>
                            element.Name.LocalName ==
                            "tag")
                    .Select(
                        element =>
                            (
                                Key:
                                    element.Attribute(
                                        "k")?.Value,
                                Value:
                                    element.Attribute(
                                        "v")?.Value
                            ))
                    .Where(
                        item =>
                            !string.IsNullOrWhiteSpace(
                                item.Key))
                    .GroupBy(
                        item =>
                            item.Key!,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.Last().Value,
                        StringComparer.OrdinalIgnoreCase);

            var highway =
                tags.GetValueOrDefault(
                    "highway")
                    ?.Trim();

            var crossing =
                tags.GetValueOrDefault(
                    "crossing")
                    ?.Trim();

            var crossingSignals =
                tags.GetValueOrDefault(
                    "crossing:signals")
                    ?.Trim();

            var worldPoint =
                MapStudioGeographicProjection
                    .Project(
                        anchor,
                        new MapStudioGeoRoadPoint(
                            latitude,
                            longitude));

            if (
                string.Equals(
                    highway,
                    "traffic_signals",
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(
                    new MapStudioOsmJunctionControl(
                        nodeId,
                        MapStudioOsmJunctionControlKind
                            .TrafficSignal,
                        worldPoint));
            }

            var isCrosswalk =
                string.Equals(
                    highway,
                    "crossing",
                    StringComparison.OrdinalIgnoreCase) ||
                (
                    !string.IsNullOrWhiteSpace(
                        crossing) &&
                    !string.Equals(
                        crossing,
                        "no",
                        StringComparison.OrdinalIgnoreCase)
                );

            if (isCrosswalk)
            {
                result.Add(
                    new MapStudioOsmJunctionControl(
                        nodeId,
                        MapStudioOsmJunctionControlKind
                            .Crosswalk,
                        worldPoint));
            }

            if (
                isCrosswalk &&
                string.Equals(
                    crossingSignals,
                    "yes",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    highway,
                    "traffic_signals",
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(
                    new MapStudioOsmJunctionControl(
                        nodeId,
                        MapStudioOsmJunctionControlKind
                            .TrafficSignal,
                        worldPoint));
            }
        }

        return result;
    }
}
