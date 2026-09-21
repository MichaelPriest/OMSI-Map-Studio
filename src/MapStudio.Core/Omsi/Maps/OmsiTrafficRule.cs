namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTrafficRule(
    bool IsKillRule,
    int? PathIndex,
    string RuleName,
    string RawValue,
    double? NumericValue,
    int? VehicleGroupIndex,
    IReadOnlyList<string> RawValues)
{
    public string NormalizedRuleName =>
        RuleName
            .Trim()
            .ToLowerInvariant();

    public bool IsKnownRule =>
        NormalizedRuleName is
            "speedlimit" or
            "overtaking_prohib" or
            "trafficdensity" or
            "priority" or
            "no_cars" or
            "truck" or
            "bus";
}
