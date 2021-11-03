using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;

namespace SimpleRecorder
{
    public sealed partial class AboutDialog : ContentDialog
    {
        public AboutDialog()
        {
            this.InitializeComponent();

            var version = Package.Current.Id.Version;
            VersionTextBlock.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
