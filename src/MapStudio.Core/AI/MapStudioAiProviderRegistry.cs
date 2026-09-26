namespace MapStudio.Core.AI;

public sealed class MapStudioAiProviderRegistry
{
    private readonly Dictionary<
        string,
        IMapStudioAiProvider>
        _providers =
            new(
                StringComparer
                    .OrdinalIgnoreCase);

    public IReadOnlyList<
        MapStudioAiProviderDescriptor>
        Providers =>
        _providers
            .Values
            .Select(
                provider =>
                    provider
                        .Descriptor)
            .OrderBy(
                descriptor =>
                    descriptor
                        .DisplayName,
                StringComparer
                    .OrdinalIgnoreCase)
            .ToArray();

    public void Register(
        IMapStudioAiProvider provider)
    {
        ArgumentNullException.ThrowIfNull(
            provider);

        var descriptor =
            provider.Descriptor;

        if (
            string.IsNullOrWhiteSpace(
                descriptor.Id) ||
            string.IsNullOrWhiteSpace(
                descriptor.DisplayName))
        {
            throw new ArgumentException(
                "AI provider descriptor must contain an id and display name.",
                nameof(provider));
        }

        _providers[
            descriptor.Id.Trim()] =
            provider;
    }

    public bool Remove(
        string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerId);

        return _providers.Remove(
            providerId.Trim());
    }

    public bool TryGet(
        string providerId,
        out IMapStudioAiProvider?
            provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerId);

        return _providers.TryGetValue(
            providerId.Trim(),
            out provider);
    }

    public IMapStudioAiProvider GetRequired(
        string providerId)
    {
        if (
            TryGet(
                providerId,
                out var provider) &&
            provider is not null)
        {
            return provider;
        }

        throw new KeyNotFoundException(
            $"AI provider '{providerId}' is not registered.");
    }
}
