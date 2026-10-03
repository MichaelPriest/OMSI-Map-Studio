using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Scenery;

public sealed class OmsiSceneryTrafficLightPatcher
{
    private static readonly HashSet<string>
        TrafficSectionKeywords =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "traffic_lights_group",
                "trafficlight_group",
                "traffic_light",
                "phase"
            };

    public byte[] Patch(
        OmsiConfigDocument document,
        IReadOnlyList<
            OmsiTrafficLightController>
            controllers)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            controllers);

        if (controllers.Count > 128)
        {
            throw new InvalidDataException(
                "trafficLightControllerCountTooLarge");
        }

        foreach (
            var controller in
                controllers)
        {
            if (
                controller.CycleDuration is
                    < 0 ||
                controller.Programs.Count >
                    256)
            {
                throw new InvalidDataException(
                    "trafficLightControllerInvalid");
            }

            foreach (
                var program in
                    controller.Programs)
            {
                if (
                    string.IsNullOrWhiteSpace(
                        program.Name) ||
                    program.Phases.Count >
                        4096 ||
                    program.Phases.Any(
                        phase =>
                            phase.Duration <
                                0 ||
                            !double.IsFinite(
                                phase.Duration)))
                {
                    throw new InvalidDataException(
                        "trafficLightProgramInvalid");
                }
            }
        }

        var lines =
            document.Lines
                .ToList();

        var sections =
            document.Sections;

        var remove =
            new bool[
                lines.Count];

        var insertAt =
            -1;

        for (
            var index = 0;
            index < sections.Count;
            index++)
        {
            var section =
                sections[index];

            if (
                !TrafficSectionKeywords
                    .Contains(
                        section.Keyword))
            {
                continue;
            }

            insertAt =
                insertAt < 0
                    ? section.KeywordLineIndex
                    : Math.Min(
                        insertAt,
                        section.KeywordLineIndex);

            var end =
                index + 1 <
                    sections.Count
                    ? sections[index + 1]
                        .KeywordLineIndex
                    : lines.Count;

            for (
                var lineIndex =
                    section.KeywordLineIndex;
                lineIndex < end &&
                lineIndex < remove.Length;
                lineIndex++)
            {
                remove[lineIndex] =
                    true;
            }
        }

        if (insertAt < 0)
        {
            insertAt =
                lines.Count;
        }

        var compacted =
            new List<string>(
                lines.Count);

        var adjustedInsertAt =
            0;

        for (
            var index = 0;
            index < lines.Count;
            index++)
        {
            if (
                index < insertAt &&
                !remove[index])
            {
                adjustedInsertAt++;
            }

            if (!remove[index])
            {
                compacted.Add(
                    lines[index]);
            }
        }

        var generated =
            BuildTrafficLines(
                controllers);

        if (
            generated.Count >
            0)
        {
            if (
                adjustedInsertAt >
                    0 &&
                compacted[
                    adjustedInsertAt -
                    1].Length !=
                    0)
            {
                generated.Insert(
                    0,
                    string.Empty);
            }

            if (
                adjustedInsertAt <
                    compacted.Count &&
                compacted[
                    adjustedInsertAt].Length !=
                    0)
            {
                generated.Add(
                    string.Empty);
            }

            compacted.InsertRange(
                adjustedInsertAt,
                generated);
        }

        var patched =
            new OmsiConfigDocument(
                compacted,
                Array.Empty<
                    OmsiConfigSection>(),
                document.NewLine,
                document
                    .HasTrailingNewLine,
                document
                    .TextEncoding,
                document
                    .HasByteOrderMark);

        return patched.ToBytes();
    }

    private static List<string>
        BuildTrafficLines(
            IReadOnlyList<
                OmsiTrafficLightController>
                controllers)
    {
        var result =
            new List<string>();

        for (
            var controllerIndex = 0;
            controllerIndex <
                controllers.Count;
            controllerIndex++)
        {
            var controller =
                controllers[
                    controllerIndex];

            var useGroup =
                controller
                    .CycleDuration
                    .HasValue ||
                controllers.Count >
                    1;

            if (useGroup)
            {
                var cycle =
                    controller
                        .CycleDuration ??
                    controller.Programs
                        .Select(
                            program =>
                                program
                                    .TotalDuration)
                        .DefaultIfEmpty(
                            0)
                        .Max();

                result.Add(
                    "[traffic_lights_group]");

                result.Add(
                    cycle.ToString(
                        "G17",
                        CultureInfo
                            .InvariantCulture));

                result.Add(
                    string.Empty);
            }

            foreach (
                var program in
                    controller.Programs)
            {
                result.Add(
                    "[traffic_light]");

                result.Add(
                    program.Name.Trim());

                result.Add(
                    string.Empty);

                foreach (
                    var phase in
                        program.Phases)
                {
                    result.Add(
                        "[phase]");

                    result.Add(
                        phase.SignalCode
                            .ToString(
                                CultureInfo
                                    .InvariantCulture));

                    result.Add(
                        phase.Duration
                            .ToString(
                                "G17",
                                CultureInfo
                                    .InvariantCulture));

                    result.Add(
                        string.Empty);
                }
            }

            if (
                controllerIndex <
                    controllers.Count -
                    1 &&
                result.Count >
                    0 &&
                result[^1].Length !=
                    0)
            {
                result.Add(
                    string.Empty);
            }
        }

        while (
            result.Count >
                0 &&
            result[^1].Length ==
                0)
        {
            result.RemoveAt(
                result.Count -
                1);
        }

        return result;
    }
}
