using CaptureEncoder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI.Composition;

namespace SimpleRecorder
{
    class ResolutionItem
    {
        public string DisplayName { get; set; }
        public SizeUInt32 Resolution { get; set; }

        public bool IsZero() { return Resolution.Width == 0 || Resolution.Height == 0; }
    }

    class BitrateItem
    {
        public string DisplayName { get; set; }
        public uint Bitrate { get; set; }
    }

    class FrameRateItem
    {
        public string DisplayName { get; set; }
        public uint FrameRate { get; set; }
    }

    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            
            ApplicationView.GetForCurrentView().SetPreferredMinSize(
               new Size(350, 200));

            if (!GraphicsCaptureSession.IsSupported())
            {
                IsEnabled = false;

                var dialog = new MessageDialog(
                    "Screen capture is not supported on this device for this release of Windows!",
                    "Screen capture unsupported");

                var ignored = dialog.ShowAsync();
                return;
            }

            var compositor = Window.Current.Compositor;
            _previewBrush = compositor.CreateSurfaceBrush();
            _previewBrush.Stretch = CompositionStretch.Uniform;
            var shadow = compositor.CreateDropShadow();
            shadow.Mask = _previewBrush;
            _previewVisual = compositor.CreateSpriteVisual();
            _previewVisual.RelativeSizeAdjustment = Vector2.One;
            _previewVisual.Brush = _previewBrush;
            _previewVisual.Shadow = shadow;
            ElementCompositionPreview.SetElementChildVisual(CapturePreviewGrid, _previewVisual);

            _device = Direct3D11Helpers.CreateDevice();

            var settings = GetCachedSettings();

            _resolutions = new List<ResolutionItem>();
            foreach (var resolution in EncoderPresets.Resolutions)
            {
                _resolutions.Add(new ResolutionItem()
                {
                    DisplayName = $"{resolution.Width} x {resolution.Height}",
                    Resolution = resolution,
                });
            }
            _resolutions.Add(new ResolutionItem()
            {
                DisplayName = "Use source size",
                Resolution = new SizeUInt32() { Width = 0, Height = 0 },
            });
            ResolutionComboBox.ItemsSource = _resolutions;
            ResolutionComboBox.SelectedIndex = GetResolutionIndex(settings.Width, settings.Height);

            _bitrates = new List<BitrateItem>();
            foreach (var bitrate in EncoderPresets.Bitrates)
            {
                var mbps = (float)bitrate / 1000000;
                _bitrates.Add(new BitrateItem()
                {
                    DisplayName = $"{mbps:0.##} Mbps",
                    Bitrate = bitrate,
                });
            }
            BitrateComboBox.ItemsSource = _bitrates;
            BitrateComboBox.SelectedIndex = GetBitrateIndex(settings.Bitrate);

            _frameRates = new List<FrameRateItem>();
            foreach (var frameRate in EncoderPresets.FrameRates)
            {
                _frameRates.Add(new FrameRateItem()
                {
                    DisplayName = $"{frameRate}fps",
                    FrameRate = frameRate,
                });
            }
            FrameRateComboBox.ItemsSource = _frameRates;
            FrameRateComboBox.SelectedIndex = GetFrameRateIndex(settings.FrameRate);
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new GraphicsCapturePicker();
            var item = await picker.PickSingleItemAsync();
            if (item != null)
            {
                StartPreview(item);
            }
            else
            {
                StopPreview();
            }
        }

        private void StartPreview(GraphicsCaptureItem item)
        {
            CapturePreviewGrid.Visibility = Visibility.Visible;
            CapturePreviewGrid.Width = item.Size.Width;
            CapturePreviewGrid.Width = item.Size.Height;
            CaptureInfoTextBlock.Text = item.DisplayName;

            var compositor = Window.Current.Compositor;
            _preview?.Dispose();
            _preview = new CapturePreview(_device, item);
            var surface = _preview.CreateSurface(compositor);
            _previewBrush.Surface = surface;
            _preview.StartCapture();
        }

        private void StopPreview()
        {
            CapturePreviewGrid.Visibility = Visibility.Collapsed;
            CaptureInfoTextBlock.Text = "Pick something to capture";
            _preview?.Dispose();
            _preview = null;
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

        private async Task<StorageFile> GetTempFileAsync()
        {
            var folder = ApplicationData.Current.TemporaryFolder;
            var name = DateTime.Now.ToString("yyyyMMdd-HHmm-ss");
            var file = await folder.CreateFileAsync($"{name}.mp4");
            return file;
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

        private AppSettings GetCurrentSettings()
        {
            var resolutionItem = (ResolutionItem)ResolutionComboBox.SelectedItem;
            var width = resolutionItem.Resolution.Width;
            var height = resolutionItem.Resolution.Height;
            var bitrateItem = (BitrateItem)BitrateComboBox.SelectedItem;
            var bitrate = bitrateItem.Bitrate;
            var frameRateItem = (FrameRateItem)FrameRateComboBox.SelectedItem;
            var frameRate = frameRateItem.FrameRate;

            return new AppSettings { Width = width, Height = height, Bitrate = bitrate, FrameRate = frameRate };
        }

        private AppSettings GetCachedSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var result =  new AppSettings
            {
                Width = 1920,
                Height = 1080,
                Bitrate = 18000000,
                FrameRate = 60,
            };
            
            // Resolution
            if (localSettings.Values.TryGetValue(nameof(AppSettings.Width), out var width) &&
                localSettings.Values.TryGetValue(nameof(AppSettings.Height), out var height))
            {
                result.Width = (uint)width;
                result.Height = (uint)height;
            }
            // Support the old settings
            else if (localSettings.Values.TryGetValue("UseSourceSize", out var useSourceSize) &&
                (bool)useSourceSize == true)
            {
                result.Width = 0;
                result.Height = 0;
            }
            else if (localSettings.Values.TryGetValue("Quality", out var quality))
            {
                var videoQuality = ParseEnumValue<VideoEncodingQuality>((string)quality);

                var temp = MediaEncodingProfile.CreateMp4(videoQuality);
                result.Width = temp.Video.Width;
                result.Height = temp.Video.Height;
            }

            // Bitrate
            if (localSettings.Values.TryGetValue(nameof(AppSettings.Bitrate), out var bitrate))
            {
                result.Bitrate = (uint)bitrate;
            }
            // Suppor the old setting
            else if (localSettings.Values.TryGetValue("Quality", out var quality))
            {
                var videoQuality = ParseEnumValue<VideoEncodingQuality>((string)quality);

                var temp = MediaEncodingProfile.CreateMp4(videoQuality);
                result.Bitrate = temp.Video.Bitrate;
            }

            // Frame rate
            if (localSettings.Values.TryGetValue(nameof(AppSettings.FrameRate), out var frameRate))
            {
                result.FrameRate = (uint)frameRate;
            }

            return result;
        }

        public void CacheCurrentSettings()
        {
            var settings = GetCurrentSettings();
            CacheSettings(settings);
        }

        public void EndCurrentRecording()
        {
            _encoder?.Dispose();
        }

        private static void CacheSettings(AppSettings settings)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[nameof(AppSettings.Width)] = settings.Width;
            localSettings.Values[nameof(AppSettings.Height)] = settings.Height;
            localSettings.Values[nameof(AppSettings.Bitrate)] = settings.Bitrate;
            localSettings.Values[nameof(AppSettings.FrameRate)] = settings.FrameRate;
        }

        private int GetResolutionIndex(uint width, uint height)
        {
            for (var i = 0; i < _resolutions.Count; i++)
            {
                var resolution = _resolutions[i];
                if (resolution.Resolution.Width == width &&
                    resolution.Resolution.Height == height)
                {
                    return i;
                }
            }
            return -1;
        }

        private int GetBitrateIndex(uint bitrate)
        {
            for (var i = 0; i < _bitrates.Count; i++)
            {
                if (_bitrates[i].Bitrate == bitrate)
                {
                    return i;
                }
            }
            return -1;
        }

        private int GetFrameRateIndex(uint frameRate)
        {
            for (var i = 0; i < _frameRates.Count; i++)
            {
                if (_frameRates[i].FrameRate == frameRate)
                {
                    return i;
                }
            }
            return -1;
        }

        private static T ParseEnumValue<T>(string input)
        {
            return (T)Enum.Parse(typeof(T), input, false);
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

        struct AppSettings
        {
            public uint Width;
            public uint Height;
            public uint Bitrate;
            public uint FrameRate;
        }

        private IDirect3DDevice _device;
        private Encoder _encoder;

        private List<ResolutionItem> _resolutions;
        private List<BitrateItem> _bitrates;
        private List<FrameRateItem> _frameRates;

        private CapturePreview _preview;
        private SpriteVisual _previewVisual;
        private CompositionSurfaceBrush _previewBrush;
    }
}
