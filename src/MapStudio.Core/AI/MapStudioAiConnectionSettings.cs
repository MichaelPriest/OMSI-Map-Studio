namespace MapStudio.Core.AI;

public sealed record MapStudioAiConnectionProfile(
    string Id,
    string DisplayName,
    string AdapterId,
    string? Endpoint,
    string? Model,
    bool IsLocal = false)
{
    public MapStudioAiConnectionProfile Normalize()
    {
        var id =
            Id
                .Trim();

        var displayName =
            DisplayName
                .Trim();

        var adapterId =
            AdapterId
                .Trim();

        var endpoint =
            string.IsNullOrWhiteSpace(
                Endpoint)
                ? null
                : Endpoint.Trim();

        var model =
            string.IsNullOrWhiteSpace(
                Model)
                ? null
                : Model.Trim();

        if (
            string.IsNullOrWhiteSpace(
                id) ||
            string.IsNullOrWhiteSpace(
                displayName) ||
            string.IsNullOrWhiteSpace(
                adapterId))
        {
            throw new InvalidDataException(
                "aiConnectionProfileRequiredFields");
        }

        if (
            endpoint is not null &&
            (
                !Uri.TryCreate(
                    endpoint,
                    UriKind.Absolute,
                    out var uri) ||
                uri.Scheme is not
                    (
                        "http" or
                        "https"
                    )
            ))
        {
            throw new InvalidDataException(
                "aiConnectionProfileEndpointInvalid");
        }

        return this with
        {
            Id = id,
            DisplayName =
                displayName,
            AdapterId =
                adapterId,
            Endpoint =
                endpoint,
            Model =
                model
        };
    }
}

public sealed record MapStudioAiConnectionSettings(
    string? ActiveProfileId,
    IReadOnlyList<MapStudioAiConnectionProfile> Profiles)
{
    public static MapStudioAiConnectionSettings Empty { get; } =
        new(
            null,
            Array.Empty<
                MapStudioAiConnectionProfile>());

    public MapStudioAiConnectionSettings Normalize()
    {
        var profiles =
            (Profiles ??
                Array.Empty<
                    MapStudioAiConnectionProfile>())
            .Select(
                profile =>
                    profile.Normalize())
            .GroupBy(
                profile =>
                    profile.Id,
                StringComparer
                    .OrdinalIgnoreCase)
            .Select(
                group =>
                    group.Last())
            .OrderBy(
                profile =>
                    profile.DisplayName,
                StringComparer
                    .OrdinalIgnoreCase)
            .ToArray();

        var active =
            string.IsNullOrWhiteSpace(
                ActiveProfileId)
                ? null
                : ActiveProfileId.Trim();

        if (
            active is not null &&
            !profiles.Any(
                profile =>
                    string.Equals(
                        profile.Id,
                        active,
                        StringComparison
                            .OrdinalIgnoreCase)))
        {
            active =
                null;
        }

        return new MapStudioAiConnectionSettings(
            active,
            profiles);
    }

    public MapStudioAiConnectionProfile?
        GetActiveProfile()
    {
        var normalized =
            Normalize();

        if (
            normalized.ActiveProfileId is
                null)
        {
            return null;
        }

        return normalized
            .Profiles
            .FirstOrDefault(
                profile =>
                    string.Equals(
                        profile.Id,
                        normalized.ActiveProfileId,
                        StringComparison
                            .OrdinalIgnoreCase));
    }
}
