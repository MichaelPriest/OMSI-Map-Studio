using Vortice.Direct3D;
using Vortice.Direct3D11;
using static Vortice.Direct3D11.D3D11;

namespace MapStudio.Renderer.Graphics;

public sealed class D3D11DeviceHost : IDisposable
{
    private static readonly FeatureLevel[] FeatureLevels =
    [
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1,
        FeatureLevel.Level_10_0
    ];

    public D3D11DeviceHost()
    {
        var flags =
            DeviceCreationFlags.BgraSupport;

#if DEBUG
        if (SdkLayersAvailable())
        {
            flags |= DeviceCreationFlags.Debug;
        }
#endif

        var result =
            D3D11CreateDevice(
                null,
                DriverType.Hardware,
                flags,
                FeatureLevels,
                out ID3D11Device device,
                out FeatureLevel featureLevel,
                out ID3D11DeviceContext context);

        if (result.Failure)
        {
            result =
                D3D11CreateDevice(
                    IntPtr.Zero,
                    DriverType.Warp,
                    flags,
                    FeatureLevels,
                    out device,
                    out featureLevel,
                    out context);
        }

        result.CheckError();

        Device = device;
        Context = context;
        FeatureLevel = featureLevel;
    }

    public ID3D11Device Device { get; }

    public ID3D11DeviceContext Context { get; }

    public FeatureLevel FeatureLevel { get; }

    public void Dispose()
    {
        Context.ClearState();
        Context.Flush();
        Context.Dispose();
        Device.Dispose();
    }
}
