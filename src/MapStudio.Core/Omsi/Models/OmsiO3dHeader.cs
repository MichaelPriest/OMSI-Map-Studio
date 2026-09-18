namespace MapStudio.Core.Omsi.Models;

public sealed record OmsiO3dHeader(
    bool Exists,
    bool IsValid,
    int? Version,
    bool HasExtendedHeader,
    bool UsesLongTriangleIndices,
    bool UsesAlternativeEncryptionSeed,
    bool IsEncrypted)
{
    public static OmsiO3dHeader Missing { get; } = new(
        Exists: false,
        IsValid: false,
        Version: null,
        HasExtendedHeader: false,
        UsesLongTriangleIndices: false,
        UsesAlternativeEncryptionSeed: false,
        IsEncrypted: false);
}
