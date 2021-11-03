using System;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;

namespace CaptureEncoder
{
    public sealed class CapturePreview : IDisposable
    {
        public CapturePreview(IDirect3DDevice device, GraphicsCaptureItem item)
        {
            _item = item;
            _device = device;
            _d3dDevice = Direct3D11Helpers.CreateSharpDXDevice(device);

            var dxgiDevice = _d3dDevice.QueryInterface<SharpDX.DXGI.Device>();
            var adapter = dxgiDevice.GetParent<SharpDX.DXGI.Adapter>();
            var factory = adapter.GetParent<SharpDX.DXGI.Factory2>();

            var description = new SharpDX.DXGI.SwapChainDescription1
            {
                Width = item.Size.Width,
                Height = item.Size.Height,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Usage = SharpDX.DXGI.Usage.RenderTargetOutput,
                SampleDescription = new SharpDX.DXGI.SampleDescription()
                {
                    Count = 1,
                    Quality = 0
                },
                BufferCount = 2,
                Scaling = SharpDX.DXGI.Scaling.Stretch,
                SwapEffect = SharpDX.DXGI.SwapEffect.FlipSequential,
                AlphaMode = SharpDX.DXGI.AlphaMode.Premultiplied
            };
            _swapChain = new SharpDX.DXGI.SwapChain1(factory, dxgiDevice, ref description);

            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    item.Size);
            _session = _framePool.CreateCaptureSession(item);
            _lastSize = item.Size;

            _framePool.FrameArrived += OnFrameArrived;
        }

        public GraphicsCaptureItem Target => _item;

        public bool IsCursorCaptureEnabled
        {
            get { return _includeCursor; }
            set
            {
                if (_includeCursor != value)
                {
                    _includeCursor = value;
                    _session.IsCursorCaptureEnabled = _includeCursor;
                }
            }
        }

        public void StartCapture()
        {
            _session.StartCapture();
        }

        public ICompositionSurface CreateSurface(Compositor compositor)
        {
            return compositor.CreateCompositionSurfaceForSwapChain(_swapChain);
        }

        public void Dispose()
        {
            _session?.Dispose();
            _framePool?.Dispose();
            _swapChain?.Dispose();

            _swapChain = null;
            _framePool = null;
            _session = null;
            _item = null;
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            var newSize = false;

            using (var frame = sender.TryGetNextFrame())
            {
                if (frame.ContentSize.Width != _lastSize.Width ||
                    frame.ContentSize.Height != _lastSize.Height)
                {
                    // The thing we have been capturing has changed size.
                    // We need to resize our swap chain first, then blit the pixels.
                    // After we do that, retire the frame and then recreate our frame pool.
                    newSize = true;
                    _lastSize = frame.ContentSize;
                    _swapChain.ResizeBuffers(
                        2,
                        _lastSize.Width,
                        _lastSize.Height,
                        SharpDX.DXGI.Format.B8G8R8A8_UNorm, 
                        SharpDX.DXGI.SwapChainFlags.None);
                }

                using (var sourceTexture = Direct3D11Helpers.CreateSharpDXTexture2D(frame.Surface))
                using (var backBuffer = _swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
                using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(_d3dDevice, backBuffer))
                {
                    _d3dDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));
                    _d3dDevice.ImmediateContext.CopyResource(sourceTexture, backBuffer);
                }

            } // retire the frame

            _swapChain.Present(1, SharpDX.DXGI.PresentFlags.None);

            if (newSize)
            {
                _framePool.Recreate(
                    _device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _lastSize);
            }
        }

        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;
        private SizeInt32 _lastSize;
        private bool _includeCursor = true;

        private IDirect3DDevice _device;
        private SharpDX.Direct3D11.Device _d3dDevice;
        private SharpDX.DXGI.SwapChain1 _swapChain;
    }
}
