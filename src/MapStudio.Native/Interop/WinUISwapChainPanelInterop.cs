using System.Runtime.InteropServices;

namespace MapStudio.Native.Interop;

internal static class WinUISwapChainPanelInterop
{
    [ComImport]
    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISwapChainPanelNative
    {
        [PreserveSig]
        int SetSwapChain(
            IntPtr swapChain);
    }
}
