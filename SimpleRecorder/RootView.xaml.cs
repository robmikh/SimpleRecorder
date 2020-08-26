using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SimpleRecorder
{
    public sealed partial class RootView : Page
    {
        public RootView()
        {
            this.InitializeComponent();

            if (ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
            {
                CompactOverlayButton.IsEnabled = true;
            }
        }

        public Frame GetRootFrame()
        {
            return MainFrame;
        }

        private async void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog("Placeholder info dialog");
            await dialog.ShowAsync();
        }

        private async void CompactOverlayButton_Checked(object sender, RoutedEventArgs e)
        {
            var result = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
        }

        private async void CompactOverlayButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var result = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
        }
    }
}
