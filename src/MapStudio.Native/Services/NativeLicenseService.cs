using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MapStudio.Core.Commercial;
using Microsoft.Win32;
using NSec.Cryptography;

namespace MapStudio.Native.Services;

internal sealed record NativeLicenseSnapshot(
    MapStudioCommercialState State,
    string? Serial,
    string DeviceId,
    string DeviceName,
    string? SubscriptionStatus,
    DateTimeOffset? SubscriptionExpiresAt,
    DateTimeOffset? OfflineUntil,
    DateTimeOffset? LastOnlineCheck,
    string? PlanId,
    int? DeviceCount,
    int? MaxDevices,
    bool IsOffline,
    bool SignatureVerificationConfigured,
    string Message);

internal sealed record NativeLicenseActivationResult(
    bool Success,
    NativeLicenseSnapshot Snapshot,
    string Message);

internal sealed class NativeLicenseService
{
    private const string ProductDirectory =
        "OMSI Map Studio Native Preview";

    private const string LicenseDirectoryName =
        "Licensing";

    private const string LicenseFileName =
        "license.json";

    private const string DeviceSeedFileName =
        "device.seed";

    private const string Issuer =
        "omsi-map-studio";

    private static readonly HttpClient Http =
        new()
        {
            Timeout =
                TimeSpan.FromSeconds(
                    12)
        };

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive =
                true,
            WriteIndented =
                true
        };

    private static readonly Regex SerialPattern =
        new(
            @"^OMS-MS-A5-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase);

    private readonly string _deviceId;
    private readonly string _deviceName;

    public NativeLicenseService()
    {
        _deviceId =
            ResolveDeviceId();

        _deviceName =
            string.IsNullOrWhiteSpace(
                Environment.MachineName)
                ? "Windows PC"
                : Environment.MachineName;
    }

    public string DeviceId =>
        _deviceId;

    public string DeviceName =>
        _deviceName;

    public bool SignatureVerificationConfigured =>
        TryResolvePublicKey(
            out _,
            out _);

    public NativeLicenseSnapshot LoadCachedSnapshot()
    {
        var cache =
            ReadCache();

        if (
            !TryResolvePublicKey(
                out _,
                out var keyError))
        {
            return new NativeLicenseSnapshot(
                MapStudioCommercialState
                    .DevelopmentPreview(),
                cache?.Serial,
                _deviceId,
                _deviceName,
                null,
                null,
                null,
                cache?.LastOnlineCheck,
                cache?.PlanId,
                cache?.DeviceCount,
                cache?.MaxDevices,
                IsOffline:
                    true,
                SignatureVerificationConfigured:
                    false,
                Message:
                    keyError ??
                    "Chave pública Ed25519 ainda não configurada nesta build de desenvolvimento.");
        }

        if (
            cache is null ||
            string.IsNullOrWhiteSpace(
                cache.EntitlementToken))
        {
            return CreateUnlicensedSnapshot(
                cache?.Serial,
                "Nenhuma licença ativada neste computador.");
        }

        if (
            !TryCreateSnapshotFromToken(
                cache.EntitlementToken,
                cache.Serial,
                cache.LastOnlineCheck,
                cache.PlanId,
                cache.DeviceCount,
                cache.MaxDevices,
                isOffline:
                    true,
                out var snapshot,
                out var error))
        {
            return CreateUnlicensedSnapshot(
                cache.Serial,
                error ??
                "O token local de licença não pôde ser validado.");
        }

        return snapshot!;
    }

    public async Task<NativeLicenseSnapshot>
        RefreshCachedAsync(
            CancellationToken cancellationToken =
                default)
    {
        var cache =
            ReadCache();

        if (
            cache is null ||
            string.IsNullOrWhiteSpace(
                cache.Serial))
        {
            return LoadCachedSnapshot();
        }

        var result =
            await ActivateAsync(
                    cache.Serial,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        if (result.Success)
        {
            return result.Snapshot;
        }

        var cached =
            LoadCachedSnapshot();

        if (
            cached.State.Status is
                MapStudioLicenseStatus.GracePeriod or
                MapStudioLicenseStatus.PastDue)
        {
            return cached with
            {
                Message =
                    $"{result.Message} Usando token offline assinado até {cached.OfflineUntil?.ToLocalTime():g}.",
                IsOffline =
                    true
            };
        }

        return cached with
        {
            Message =
                result.Message
        };
    }

    public async Task<NativeLicenseActivationResult>
        ActivateAsync(
            string? serial,
            CancellationToken cancellationToken =
                default)
    {
        serial =
            serial
                ?.Trim()
                .ToUpperInvariant();

        if (
            string.IsNullOrWhiteSpace(
                serial) ||
            !SerialPattern.IsMatch(
                serial))
        {
            var snapshot =
                LoadCachedSnapshot();

            return new NativeLicenseActivationResult(
                false,
                snapshot,
                "Informe um serial no formato OMS-MS-A5-XXXX-XXXX-XXXX.");
        }

        if (
            !TryResolvePublicKey(
                out _,
                out var keyError))
        {
            var snapshot =
                LoadCachedSnapshot();

            return new NativeLicenseActivationResult(
                false,
                snapshot,
                keyError ??
                "A chave pública Ed25519 não está configurada.");
        }

        var apiBaseUri =
            NativeCommerceEndpoint
                .ResolveApiBaseUri();

        if (apiBaseUri is null)
        {
            var snapshot =
                LoadCachedSnapshot();

            return new NativeLicenseActivationResult(
                false,
                snapshot,
                "Servidor comercial ainda não configurado nesta build.");
        }

        var endpoint =
            new Uri(
                apiBaseUri,
                "/api/license/activate");

        var requestBody =
            JsonSerializer.Serialize(
                new ActivationRequest(
                    serial,
                    _deviceId,
                    _deviceName),
                JsonOptions);

        try
        {
            using var content =
                new StringContent(
                    requestBody,
                    Encoding.UTF8,
                    "application/json");

            using var response =
                await Http
                    .PostAsync(
                        endpoint,
                        content,
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            var responseBody =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            if (!response.IsSuccessStatusCode)
            {
                var message =
                    ReadServerError(
                        responseBody) ??
                    $"Falha ao verificar licença ({(int)response.StatusCode}).";

                if (
                    response.StatusCode is
                        HttpStatusCode.Forbidden or
                        HttpStatusCode.NotFound)
                {
                    SaveCache(
                        new NativeLicenseCache(
                            serial,
                            null,
                            DateTimeOffset.UtcNow,
                            null,
                            null,
                            null));

                    return new NativeLicenseActivationResult(
                        false,
                        CreateUnlicensedSnapshot(
                            serial,
                            message),
                        message);
                }

                return new NativeLicenseActivationResult(
                    false,
                    LoadCachedSnapshot(),
                    message);
            }

            var envelope =
                JsonSerializer.Deserialize<
                    ActivationEnvelope>(
                    responseBody,
                    JsonOptions);

            if (
                envelope is null ||
                string.IsNullOrWhiteSpace(
                    envelope.EntitlementToken))
            {
                return new NativeLicenseActivationResult(
                    false,
                    LoadCachedSnapshot(),
                    "O servidor não devolveu um token de entitlement.");
            }

            var now =
                DateTimeOffset.UtcNow;

            if (
                !TryCreateSnapshotFromToken(
                    envelope.EntitlementToken,
                    serial,
                    now,
                    envelope.Payload?.PlanId,
                    envelope.Payload?.DeviceCount,
                    envelope.Payload?.MaxDevices,
                    isOffline:
                        false,
                    out var snapshot,
                    out var verificationError))
            {
                return new NativeLicenseActivationResult(
                    false,
                    LoadCachedSnapshot(),
                    verificationError ??
                    "A assinatura Ed25519 da licença é inválida.");
            }

            SaveCache(
                new NativeLicenseCache(
                    serial,
                    envelope.EntitlementToken,
                    now,
                    snapshot!.PlanId,
                    snapshot.DeviceCount,
                    snapshot.MaxDevices));

            return new NativeLicenseActivationResult(
                true,
                snapshot,
                "Licença verificada e token offline atualizado.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.Write(
                $"License activation failed: {exception}");

            return new NativeLicenseActivationResult(
                false,
                LoadCachedSnapshot(),
                "Não foi possível contatar o servidor de licença. O token offline continuará sendo usado enquanto estiver válido.");
        }
    }

    private bool TryCreateSnapshotFromToken(
        string token,
        string? serial,
        DateTimeOffset? lastOnlineCheck,
        string? cachedPlanId,
        int? cachedDeviceCount,
        int? cachedMaxDevices,
        bool isOffline,
        out NativeLicenseSnapshot? snapshot,
        out string? error)
    {
        snapshot =
            null;

        error =
            null;

        var parts =
            token.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            error =
                "Token de licença com formato inválido.";

            return false;
        }

        if (
            !TryResolvePublicKey(
                out var publicKey,
                out error))
        {
            return false;
        }

        try
        {
            var signature =
                Base64UrlDecode(
                    parts[1]);

            var signedBody =
                Encoding.UTF8
                    .GetBytes(
                        parts[0]);

            var algorithm =
                SignatureAlgorithm
                    .Ed25519;

            if (
                !algorithm.Verify(
                    publicKey!,
                    signedBody,
                    signature))
            {
                error =
                    "A assinatura Ed25519 do token não confere.";

                return false;
            }

            var payloadBytes =
                Base64UrlDecode(
                    parts[0]);

            var payload =
                JsonSerializer.Deserialize<
                    NativeLicensePayload>(
                    payloadBytes,
                    JsonOptions);

            if (payload is null)
            {
                error =
                    "Payload de licença vazio.";

                return false;
            }

            if (
                !string.Equals(
                    payload.Issuer,
                    Issuer,
                    StringComparison.Ordinal))
            {
                error =
                    "Emissor do token de licença não reconhecido.";

                return false;
            }

            if (
                !string.Equals(
                    payload.DeviceId,
                    _deviceId,
                    StringComparison.Ordinal))
            {
                error =
                    "Este token pertence a outro computador.";

                return false;
            }

            var now =
                DateTimeOffset.UtcNow;

            if (
                payload.IssuedAt >
                now.AddMinutes(
                    10))
            {
                error =
                    "Token de licença emitido com data futura inválida.";

                return false;
            }

            if (
                payload.OfflineUntil >
                payload.SubscriptionExpiresAt)
            {
                error =
                    "Janela offline do token excede a validade da assinatura.";

                return false;
            }

            var entitlementSet =
                new HashSet<string>(
                    payload.Entitlements ??
                    [],
                    StringComparer
                        .OrdinalIgnoreCase);

            MapStudioLicenseStatus status;

            if (
                now >
                payload.SubscriptionExpiresAt)
            {
                status =
                    MapStudioLicenseStatus
                        .Expired;

                entitlementSet.Clear();
            }
            else if (
                now >
                payload.OfflineUntil)
            {
                status =
                    MapStudioLicenseStatus
                        .Unavailable;

                entitlementSet.Clear();
            }
            else
            {
                status =
                    payload.SubscriptionStatus
                        .ToLowerInvariant() switch
                    {
                        "past_due" =>
                            MapStudioLicenseStatus
                                .PastDue,
                        "active" or
                        "trialing" =>
                            isOffline
                                ? MapStudioLicenseStatus
                                    .GracePeriod
                                : MapStudioLicenseStatus
                                    .Active,
                        _ =>
                            MapStudioLicenseStatus
                                .Unavailable
                    };

                if (
                    status ==
                    MapStudioLicenseStatus
                        .Unavailable)
                {
                    entitlementSet.Clear();
                }
            }

            var planId =
                payload.PlanId ??
                cachedPlanId;

            var deviceCount =
                payload.DeviceCount ??
                cachedDeviceCount;

            var maxDevices =
                payload.MaxDevices ??
                cachedMaxDevices;

            var state =
                new MapStudioCommercialState(
                    EnforcementEnabled:
                        true,
                    Status:
                        status,
                    SubscriptionPlanId:
                        planId,
                    Entitlements:
                        entitlementSet,
                    ValidUntil:
                        payload.OfflineUntil,
                    AccountId:
                        payload.LicenseId);

            snapshot =
                new NativeLicenseSnapshot(
                    state,
                    serial,
                    _deviceId,
                    _deviceName,
                    payload.SubscriptionStatus,
                    payload.SubscriptionExpiresAt,
                    payload.OfflineUntil,
                    lastOnlineCheck,
                    planId,
                    deviceCount,
                    maxDevices,
                    isOffline,
                    SignatureVerificationConfigured:
                        true,
                    Message:
                        status switch
                        {
                            MapStudioLicenseStatus.Active =>
                                "Licença validada online.",
                            MapStudioLicenseStatus.GracePeriod =>
                                "Token assinado válido em modo offline.",
                            MapStudioLicenseStatus.PastDue =>
                                "Assinatura com pagamento pendente; respeitando a janela offline emitida pelo servidor.",
                            MapStudioLicenseStatus.Expired =>
                                "A assinatura expirou.",
                            _ =>
                                "A licença precisa ser verificada online."
                        });

            return true;
        }
        catch (Exception exception)
        {
            error =
                $"Não foi possível validar o token: {exception.Message}";

            return false;
        }
    }

    private NativeLicenseSnapshot CreateUnlicensedSnapshot(
        string? serial,
        string message) =>
        new(
            MapStudioCommercialState
                .ProductionUnlicensed(),
            serial,
            _deviceId,
            _deviceName,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            IsOffline:
                true,
            SignatureVerificationConfigured:
                true,
            Message:
                message);

    private static bool TryResolvePublicKey(
        out PublicKey? publicKey,
        out string? error)
    {
        publicKey =
            null;

        error =
            null;

        var configured =
            Environment
                .GetEnvironmentVariable(
                    "OMSI_MAP_STUDIO_LICENSE_PUBLIC_KEY");

        if (
            string.IsNullOrWhiteSpace(
                configured))
        {
            error =
                "Chave pública de licença não configurada. Nesta fase use OMSI_MAP_STUDIO_LICENSE_PUBLIC_KEY somente para testes; a chave pública de produção será embutida antes do lançamento.";

            return false;
        }

        configured =
            configured
                .Replace(
                    "\\n",
                    "\n",
                    StringComparison.Ordinal)
                .Trim();

        try
        {
            var algorithm =
                SignatureAlgorithm
                    .Ed25519;

            if (
                configured.Contains(
                    "BEGIN PUBLIC KEY",
                    StringComparison.Ordinal))
            {
                publicKey =
                    PublicKey.Import(
                        algorithm,
                        Encoding.UTF8.GetBytes(
                            configured),
                        KeyBlobFormat
                            .PkixPublicKeyText);

                return true;
            }

            var blob =
                DecodeBase64Flexible(
                    configured);

            publicKey =
                blob.Length ==
                    algorithm.PublicKeySize
                    ? PublicKey.Import(
                        algorithm,
                        blob,
                        KeyBlobFormat
                            .RawPublicKey)
                    : PublicKey.Import(
                        algorithm,
                        blob,
                        KeyBlobFormat
                            .PkixPublicKey);

            return true;
        }
        catch (Exception exception)
        {
            error =
                $"Chave pública Ed25519 inválida: {exception.Message}";

            return false;
        }
    }

    private static byte[] DecodeBase64Flexible(
        string value)
    {
        value =
            value
                .Trim()
                .Replace(
                    '-',
                    '+')
                .Replace(
                    '_',
                    '/');

        var padding =
            value.Length % 4;

        if (padding != 0)
        {
            value =
                value.PadRight(
                    value.Length +
                    (4 - padding),
                    '=');
        }

        return Convert
            .FromBase64String(
                value);
    }

    private static byte[] Base64UrlDecode(
        string value) =>
        DecodeBase64Flexible(
            value);

    private static string? ReadServerError(
        string responseBody)
    {
        try
        {
            using var document =
                JsonDocument.Parse(
                    responseBody);

            return
                document.RootElement
                    .TryGetProperty(
                        "error",
                        out var property)
                    ? property.GetString()
                    : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveDeviceId()
    {
        var seed =
            TryReadMachineGuid();

        if (
            string.IsNullOrWhiteSpace(
                seed))
        {
            seed =
                LoadOrCreateDeviceSeed();
        }

        var bytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    $"omsi-map-studio/device/v1|{seed}"));

        return Convert
            .ToHexString(
                bytes.AsSpan(
                    0,
                    16));
    }

    private static string? TryReadMachineGuid()
    {
        try
        {
            return Registry
                .GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
                    "MachineGuid",
                    null)
                ?.ToString()
                ?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string LoadOrCreateDeviceSeed()
    {
        var directory =
            LicenseDirectory;

        var path =
            Path.Combine(
                directory,
                DeviceSeedFileName);

        try
        {
            if (File.Exists(path))
            {
                var existing =
                    File.ReadAllText(
                            path,
                            Encoding.UTF8)
                        .Trim();

                if (
                    !string.IsNullOrWhiteSpace(
                        existing))
                {
                    return existing;
                }
            }

            var created =
                Convert.ToHexString(
                    RandomNumberGenerator
                        .GetBytes(
                            32));

            Directory.CreateDirectory(
                directory);

            File.WriteAllText(
                path,
                created,
                Encoding.UTF8);

            return created;
        }
        catch
        {
            return
                $"{Environment.MachineName}|{Environment.OSVersion.VersionString}";
        }
    }

    private static string LicenseDirectory
    {
        get
        {
            var directory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder
                            .LocalApplicationData),
                    ProductDirectory,
                    LicenseDirectoryName);

            Directory.CreateDirectory(
                directory);

            return directory;
        }
    }

    private static string LicenseFilePath =>
        Path.Combine(
            LicenseDirectory,
            LicenseFileName);

    private static NativeLicenseCache? ReadCache()
    {
        try
        {
            if (
                !File.Exists(
                    LicenseFilePath))
            {
                return null;
            }

            return JsonSerializer
                .Deserialize<
                    NativeLicenseCache>(
                    File.ReadAllText(
                        LicenseFilePath,
                        Encoding.UTF8),
                    JsonOptions);
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.Write(
                $"License cache read failed: {exception}");

            return null;
        }
    }

    private static void SaveCache(
        NativeLicenseCache cache)
    {
        try
        {
            var json =
                JsonSerializer.Serialize(
                    cache,
                    JsonOptions);

            var temp =
                LicenseFilePath +
                ".tmp";

            File.WriteAllText(
                temp,
                json,
                Encoding.UTF8);

            File.Move(
                temp,
                LicenseFilePath,
                overwrite:
                    true);
        }
        catch (Exception exception)
        {
            NativeStartupDiagnostics.Write(
                $"License cache write failed: {exception}");
        }
    }

    private sealed record ActivationRequest(
        string Serial,
        string DeviceId,
        string DeviceName);

    private sealed record ActivationEnvelope(
        string? EntitlementToken,
        NativeLicensePayload? Payload);

    private sealed record NativeLicensePayload(
        string Issuer,
        string LicenseId,
        string DeviceId,
        string[]? Entitlements,
        string SubscriptionStatus,
        DateTimeOffset SubscriptionExpiresAt,
        DateTimeOffset OfflineUntil,
        DateTimeOffset IssuedAt,
        string? PlanId = null,
        int? DeviceCount = null,
        int? MaxDevices = null);

    private sealed record NativeLicenseCache(
        string? Serial,
        string? EntitlementToken,
        DateTimeOffset? LastOnlineCheck,
        string? PlanId,
        int? DeviceCount,
        int? MaxDevices);
}
