namespace MapStudio.Core.Omsi.Traffic;

public static class OmsiTrafficRulePresets
{
    public static IReadOnlyList<
        OmsiTrafficRulePreset> All { get; } =
        [
            new(
                "speed-limit",
                "Speed Limit",
                "speedlimit",
                null,
                RequiresCustomValue:
                    true),
            new(
                "overtaking-prohibited",
                "Overtaking Prohibited",
                "overtaking_prohib",
                0),
            new(
                "no-unscheduled-traffic",
                "No Unscheduled Traffic",
                "trafficdensity",
                0),
            new(
                "density-extreme-low",
                "Unscheduled Traffic Density — Extreme Low",
                "trafficdensity",
                0.001),
            new(
                "density-very-low",
                "Unscheduled Traffic Density — Very Low",
                "trafficdensity",
                0.1),
            new(
                "density-low",
                "Unscheduled Traffic Density — Low",
                "trafficdensity",
                0.5),
            new(
                "density-normal",
                "Unscheduled Traffic Density — Normal",
                "trafficdensity",
                1),
            new(
                "density-high",
                "Unscheduled Traffic Density — High",
                "trafficdensity",
                3),
            new(
                "higher-priority",
                "Higher Priority",
                "priority",
                192),
            new(
                "lower-priority",
                "Lower Priority",
                "priority",
                64),
            new(
                "cars-prohibited",
                "Cars Prohibited",
                "no_cars",
                0),
            new(
                "trucks-allowed",
                "Trucks Allowed",
                "truck",
                0),
            new(
                "buses-cabs-allowed",
                "Buses & Cabs Allowed",
                "bus",
                0)
        ];

    public static OmsiTrafficRulePreset?
        TryMatch(
            string ruleName,
            double? value)
    {
        ArgumentNullException.ThrowIfNull(
            ruleName);

        var candidates =
            All.Where(
                preset =>
                    string.Equals(
                        preset.SerializedRuleName,
                        ruleName.Trim(),
                        StringComparison.OrdinalIgnoreCase));

        if (
            string.Equals(
                ruleName.Trim(),
                "speedlimit",
                StringComparison.OrdinalIgnoreCase))
        {
            return candidates
                .FirstOrDefault();
        }

        return candidates
            .FirstOrDefault(
                preset =>
                    preset.FixedValue.HasValue &&
                    value.HasValue &&
                    Math.Abs(
                        preset.FixedValue.Value -
                        value.Value) <
                    0.000001);
    }
}
