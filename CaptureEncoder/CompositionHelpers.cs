using System;
using System.Runtime.InteropServices;
using Windows.UI.Composition;

namespace CaptureEncoder
{
    static class CompositionHelpers
    {
        [ComImport]
        [Guid("25297D5C-3AD4-4C9C-B5CF-E36A38512330")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        interface ICompositorInterop
        {
            ICompositionSurface CreateCompositionSurfaceForHandle(
                IntPtr swapChain);

            ICompositionSurface CreateCompositionSurfaceForSwapChain(
                IntPtr swapChain);

            CompositionGraphicsDevice CreateGraphicsDevice(
                IntPtr renderingDevice);
        }

        public static ICompositionSurface CreateCompositionSurfaceForSwapChain(this Compositor compositor, SharpDX.DXGI.SwapChain1 swapChain)
        {
            var interop = (ICompositorInterop)(object)compositor;
            return interop.CreateCompositionSurfaceForSwapChain(swapChain.NativePointer);
        }
    }
}
