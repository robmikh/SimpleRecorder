using CaptureEncoder;
using Windows.Graphics.DirectX.Direct3D11;

namespace SimpleRecorder
{
    static class D3DDeviceManager
    {
        private static IDirect3DDevice GlobalDevice;
        public static IDirect3DDevice Device
        {
            get
            {
                // This initialization isn't thread safe, so make sure this 
                // happens well before everyone starts needing it.
                if (GlobalDevice == null)
                {
                    GlobalDevice = Direct3D11Helpers.CreateDevice();
                }
                return GlobalDevice;
            }
            
        }

    }
}
