using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage.Streams;
using Windows.UI.Composition;

namespace CaptureEncoder
{
    public sealed class Encoder : IDisposable
    {
        public Encoder(IDirect3DDevice device, GraphicsCaptureItem item)
        {
            _device = device;
            _d3dDevice = Direct3D11Helpers.CreateSharpDXDevice(device);
            _captureItem = item;
            _isRecording = false;
            _previewLock = new object();

            CreateMediaObjects();
        }

        public IAsyncAction EncodeAsync(IRandomAccessStream stream, uint width, uint height, uint bitrateInBps, uint frameRate, bool includeCursor)
        {
            return EncodeInternalAsync(stream, width, height, bitrateInBps, frameRate, includeCursor).AsAsyncAction();
        }

        private async Task EncodeInternalAsync(IRandomAccessStream stream, uint width, uint height, uint bitrateInBps, uint frameRate, bool includeCursor)
        {
            if (!_isRecording)
            {
                _isRecording = true;

                _frameGenerator = new CaptureFrameWait(
                    _device,
                    _captureItem,
                    _captureItem.Size,
                    includeCursor);

                using (_frameGenerator)
                {
                    var encodingProfile = new MediaEncodingProfile();
                    encodingProfile.Container.Subtype = "MPEG4";
                    encodingProfile.Video.Subtype = "H264";
                    encodingProfile.Video.Width = width;
                    encodingProfile.Video.Height = height;
                    encodingProfile.Video.Bitrate = bitrateInBps;
                    encodingProfile.Video.FrameRate.Numerator = frameRate;
                    encodingProfile.Video.FrameRate.Denominator = 1;
                    encodingProfile.Video.PixelAspectRatio.Numerator = 1;
                    encodingProfile.Video.PixelAspectRatio.Denominator = 1;
                    var transcode = await _transcoder.PrepareMediaStreamSourceTranscodeAsync(_mediaStreamSource, stream, encodingProfile);

                    await transcode.TranscodeAsync();
                }
            }
        }

        public ICompositionSurface CreatePreviewSurface(Compositor compositor)
        {
            if (!_isPreviewing)
            {
                lock (_previewLock)
                {
                    if (!_isPreviewing)
                    {
                        _preview = new EncoderPreview(_d3dDevice);
                        _isPreviewing = true;
                    }
                }
            }

            return _preview.CreateCompositionSurface(compositor);
        }

        public void Dispose()
        {
            if (_closed)
            {
                return;
            }
            _closed = true;

            if (!_isRecording)
            {
                DisposeInternal();
            }

            _isRecording = false;            
        }

        private void DisposeInternal()
        {
            _frameGenerator.Dispose();
            _preview?.Dispose();
        }

        private void CreateMediaObjects()
        {
            // Create our encoding profile based on the size of the item
            int width = _captureItem.Size.Width;
            int height = _captureItem.Size.Height;

            // Describe our input: uncompressed BGRA8 buffers
            var videoProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)width, (uint)height);
            _videoDescriptor = new VideoStreamDescriptor(videoProperties);

            // Create our MediaStreamSource
            _mediaStreamSource = new MediaStreamSource(_videoDescriptor);
            _mediaStreamSource.BufferTime = TimeSpan.FromSeconds(0);
            _mediaStreamSource.Starting += OnMediaStreamSourceStarting;
            _mediaStreamSource.SampleRequested += OnMediaStreamSourceSampleRequested;

            // Create our transcoder
            _transcoder = new MediaTranscoder();
            _transcoder.HardwareAccelerationEnabled = true;
        }

        private void OnMediaStreamSourceSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            if (_isRecording && !_closed)
            {
                try
                {
                    using (var frame = _frameGenerator.WaitForNewFrame())
                    {
                        if (frame == null)
                        {
                            args.Request.Sample = null;
                            DisposeInternal();
                            return;
                        }

                        if (_isPreviewing)
                        {
                            lock (_previewLock)
                            {
                                _preview.PresentSurface(frame.Surface);
                            }
                        }

                        var timeStamp = frame.SystemRelativeTime;
                        var sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, timeStamp);
                        args.Request.Sample = sample;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                    Debug.WriteLine(e);
                    args.Request.Sample = null;
                    DisposeInternal();
                }
            }
            else
            {
                args.Request.Sample = null;
                DisposeInternal();
            }
        }

        private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            using (var frame = _frameGenerator.WaitForNewFrame())
            {
                args.Request.SetActualStartPosition(frame.SystemRelativeTime);
            }
        }

        private class EncoderPreview : IDisposable
        {
            public EncoderPreview(SharpDX.Direct3D11.Device device)
            {
                _d3dDevice = device;

                var dxgiDevice = _d3dDevice.QueryInterface<SharpDX.DXGI.Device>();
                var adapter = dxgiDevice.GetParent<SharpDX.DXGI.Adapter>();
                var factory = adapter.GetParent<SharpDX.DXGI.Factory2>();

                var description = new SharpDX.DXGI.SwapChainDescription1
                {
                    Width = 1,
                    Height = 1,
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
                var swapChain = new SharpDX.DXGI.SwapChain1(factory, dxgiDevice, ref description);

                using (var backBuffer = swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
                using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(_d3dDevice, backBuffer))
                {
                    _d3dDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 0));
                }

                _swapChain = swapChain;
            }

            public ICompositionSurface CreateCompositionSurface(Compositor compositor)
            {
                return compositor.CreateCompositionSurfaceForSwapChain(_swapChain);
            }

            public void PresentSurface(IDirect3DSurface surface)
            {
                using (var sourceTexture = Direct3D11Helpers.CreateSharpDXTexture2D(surface))
                {
                    if (!_isSwapChainSized)
                    {
                        var description = sourceTexture.Description;

                        _swapChain.ResizeBuffers(
                            2,
                            description.Width,
                            description.Height,
                            SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                            SharpDX.DXGI.SwapChainFlags.None);

                        _isSwapChainSized = true;
                    }

                    using (var backBuffer = _swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
                    using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(_d3dDevice, backBuffer))
                    {
                        _d3dDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));
                        _d3dDevice.ImmediateContext.CopyResource(sourceTexture, backBuffer);
                    }
                }

                _swapChain.Present(1, SharpDX.DXGI.PresentFlags.None);
            }

            public void Dispose()
            {
                _swapChain.Dispose();
                _d3dDevice.Dispose();
            }

            private SharpDX.Direct3D11.Device _d3dDevice;
            private SharpDX.DXGI.SwapChain1 _swapChain;

            private bool _isSwapChainSized = false;
        }

        private IDirect3DDevice _device;
        private SharpDX.Direct3D11.Device _d3dDevice;

        private GraphicsCaptureItem _captureItem;
        private CaptureFrameWait _frameGenerator;

        private VideoStreamDescriptor _videoDescriptor;
        private MediaStreamSource _mediaStreamSource;
        private MediaTranscoder _transcoder;
        private bool _isRecording;

        private bool _isPreviewing = false;
        private object _previewLock;
        private EncoderPreview _preview;

        private bool _closed = false;
    }

    public struct SizeUInt32
    {
        public uint Width;
        public uint Height;
    }

    // Presets are made to match MediaEncodingProfile for ease of use
    public static class EncoderPresets
    {
        public static SizeUInt32[] Resolutions => new SizeUInt32[]
        {
            new SizeUInt32() { Width = 1280, Height = 720 },
            new SizeUInt32() { Width = 1920, Height = 1080 },
            new SizeUInt32() { Width = 3840, Height = 2160 },
            new SizeUInt32() { Width = 7680, Height = 4320 }
        };

        public static uint[] Bitrates => new uint[]
        {
            9000000,
            18000000,
            36000000,
            72000000,
        };

        public static uint[] FrameRates => new uint[]
        {
            24,
            30,
            60
        };
    }

}
