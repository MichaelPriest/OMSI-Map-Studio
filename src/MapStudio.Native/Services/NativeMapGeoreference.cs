using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Native.Services;

public sealed record NativeMapGeoreference(
    double Latitude,
    double Longitude,
    int AnchorTileX,
    int AnchorTileY,
    double AnchorX,
    double AnchorY,
    int Zoom,
    string MapType,
    string Provider)
{
    public MapStudioGeographicAnchor ToGeographicAnchor() =>
        new(Latitude, Longitude,
            OmsiTileGrid.GetOriginX(AnchorTileX) + AnchorX,
            OmsiTileGrid.GetOriginZ(AnchorTileY) + AnchorY);
}
