namespace MapStudio.Core.Omsi.Traffic;

public sealed record OmsiTrafficRulePreset(
    string Key,
    string DisplayName,
    string SerializedRuleName,
    double? FixedValue,
    bool RequiresCustomValue = false);
