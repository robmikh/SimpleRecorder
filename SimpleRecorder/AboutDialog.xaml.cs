using System;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;

namespace SimpleRecorder
{
    public sealed partial class AboutDialog : ContentDialog
    {
        public AboutDialog()
        {
            this.InitializeComponent();
        }

        public static string GetAppVersion()
        {

            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;

            return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
        }
        public string version = GetAppVersion();

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            
            string gitHub = @"https://github.com/robmikh/SimpleRecorder";
            var uri = new Uri(gitHub);
            var uriOpened = await Windows.System.Launcher.LaunchUriAsync(uri);
        }        
    }
}
