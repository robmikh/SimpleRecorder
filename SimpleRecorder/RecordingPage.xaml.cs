using CaptureEncoder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SimpleRecorder
{
    class RecordingOptions
    {
        public GraphicsCaptureItem Target { get; }
        public SizeUInt32 Resolution { get; }
        public uint Bitrate { get; }
        public uint FrameRate { get; }

        public RecordingOptions(GraphicsCaptureItem target, SizeUInt32 resolution, uint bitrate, uint frameRate)
        {
            Target = target;
            Resolution = resolution;
            Bitrate = bitrate;
            FrameRate = frameRate;
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

            _device = Direct3D11Helpers.CreateDevice();

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

        private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            _encoder?.Dispose();
            // TODO: Go back to the main page?
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

            // Tell the user we've started recording
            SetUIMode(RecordingState.Recording);

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
                        options.FrameRate);
                }
                //MainTextBlock.Foreground = originalBrush;
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

                // Tell the user we have failed
                SetUIMode(RecordingState.Failed);
                // TODO: what to do on failure?
                return;
            }

            // At this point the encoding has finished
            SetUIMode(RecordingState.Done);

            // Ask the user where they'd like the video to live
            var newFile = await PickVideoAsync();
            if (newFile == null)
            {
                // User decided they didn't want it
                // Throw out the encoded video
                //button.IsChecked = false;
                //MainTextBlock.Text = "canceled";
                //MainProgressBar.IsIndeterminate = false;
                await file.DeleteAsync();
                return;
            }
            // Move our vidoe to its new home
            await file.MoveAndReplaceAsync(newFile);

            // Open the final product
            await Launcher.LaunchFileAsync(newFile);
        }

        private void SetUIMode(RecordingState state)
        {
            switch (state)
            {
                case RecordingState.Recording:
                    RecordingStatusTextBlock.Text = "● rec";
                    RecordingStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                    break;
                case RecordingState.Failed:
                    RecordingStatusTextBlock.Text = "failure";
                    RecordingStatusTextBlock.Foreground = new SolidColorBrush((Color)Resources["SystemColorWindowTextColor"]);
                    break;
                case RecordingState.Done:
                    RecordingStatusTextBlock.Text = "done";
                    RecordingStatusTextBlock.Foreground = new SolidColorBrush((Color)Resources["SystemColorWindowTextColor"]);
                    break;
                case RecordingState.Interrupted:
                    RecordingStatusTextBlock.Text = "interrupted";
                    RecordingStatusTextBlock.Foreground = new SolidColorBrush((Color)Resources["SystemColorWindowTextColor"]);
                    break;
            }
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

        private async Task<StorageFile> PickVideoAsync()
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.SuggestedFileName = "recordedVideo";
            picker.DefaultFileExtension = ".mp4";
            picker.FileTypeChoices.Add("MP4 Video", new List<string> { ".mp4" });

            var file = await picker.PickSaveFileAsync();
            return file;
        }

        private IDirect3DDevice _device;
        private Encoder _encoder;

        private CompositionSurfaceBrush _previewBrush;
    }
}
