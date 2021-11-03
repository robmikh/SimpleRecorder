using CaptureEncoder;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Storage;
using Windows.UI.Composition;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Navigation;

namespace SimpleRecorder
{
    class RecordingOptions
    {
        public GraphicsCaptureItem Target { get; }
        public SizeUInt32 Resolution { get; }
        public uint Bitrate { get; }
        public uint FrameRate { get; }
        public bool IncludeCursor { get; }

        public RecordingOptions(GraphicsCaptureItem target, SizeUInt32 resolution, uint bitrate, uint frameRate, bool includeCursor)
        {
            Target = target;
            Resolution = resolution;
            Bitrate = bitrate;
            FrameRate = frameRate;
            IncludeCursor = includeCursor;
        }
    }


    public sealed partial class RecordingPage : Page
    {
        enum RecordingState
        {
            Recording,
            Done,
            Interrupted,
            Failed
        }

        public RecordingPage()
        {
            this.InitializeComponent();

            _device = D3DDeviceManager.Device;

            var compositor = Window.Current.Compositor;
            var visual = compositor.CreateSpriteVisual();
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.Size = new Vector2(-30.0f, -30.0f);
            visual.RelativeOffsetAdjustment = new Vector3(0.5f, 0.5f, 0);
            visual.AnchorPoint = new Vector2(0.5f, 0.5f);
            _previewBrush = compositor.CreateSurfaceBrush();
            visual.Brush = _previewBrush;
            ElementCompositionPreview.SetElementChildVisual(PreviewGrid, visual);
        }

        public void EndCurrentRecording()
        {
            _encoder.Dispose();
        }

        private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            _encoder?.Dispose();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            var options = (RecordingOptions)e.Parameter;
            await StartRecordingAsync(options);
        }

        private async Task StartRecordingAsync(RecordingOptions options)
        {
            // Encoders generally like even numbers
            var width = EnsureEven(options.Resolution.Width);
            var height = EnsureEven(options.Resolution.Height);

            // Find a place to put our vidoe for now
            var file = await GetTempFileAsync();

            // Kick off the encoding
            try
            {
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                using (_encoder = new Encoder(_device, options.Target))
                {
                    var surface = _encoder.CreatePreviewSurface(Window.Current.Compositor);
                    _previewBrush.Surface = surface;

                    await _encoder.EncodeAsync(
                        stream,
                        width, height, options.Bitrate,
                        options.FrameRate,
                        options.IncludeCursor);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex);

                var message = GetMessageForHResult(ex.HResult);
                if (message == null)
                {
                    message = $"Uh-oh! Something went wrong!\n0x{ex.HResult:X8} - {ex.Message}";
                }
                var dialog = new MessageDialog(
                    message,
                    "Recording failed");

                await dialog.ShowAsync();

                // Go back to the main page
                Frame.GoBack();
                return;
            }

            // At this point the encoding has finished, let the user preview the file
            Frame.Navigate(typeof(SavePage), file);
        }

        private uint EnsureEven(uint number)
        {
            if (number % 2 == 0)
            {
                return number;
            }
            else
            {
                return number + 1;
            }
        }

        private async Task<StorageFile> GetTempFileAsync()
        {
            var folder = ApplicationData.Current.TemporaryFolder;
            var name = DateTime.Now.ToString("yyyyMMdd-HHmm-ss");
            var file = await folder.CreateFileAsync($"{name}.mp4");
            return file;
        }

        private string GetMessageForHResult(int hresult)
        {
            switch ((uint)hresult)
            {
                // MF_E_TRANSFORM_TYPE_NOT_SET
                case 0xC00D6D60:
                    return "The combination of options you've chosen are not supported by your hardware.";
                default:
                    return null;
            }
        }

        private IDirect3DDevice _device;
        private Encoder _encoder;

        private CompositionSurfaceBrush _previewBrush;
    }
}
