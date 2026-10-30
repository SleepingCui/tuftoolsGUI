using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TuftoolsGui.Pages;

namespace TuftoolsGui;

public sealed partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
        Title = "TUF Tools GUI";
        try {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(1320, 900));
        } catch (Exception) {
        }
        Nav.SelectedItem = Nav.MenuItems[0];
    }
    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag) return;
        Type page = tag switch {
            "player" => typeof(PlayerPage),
            "level" => typeof(LevelPage),
            "pp" => typeof(PpPage),
            "rank" => typeof(RankPage),
            "xacc" => typeof(XaccPage),
            "console" => typeof(ConsolePage),
            _ => typeof(OverviewPage)
        };
        if (MainFrame.CurrentSourcePageType != page) MainFrame.Navigate(page);
    }
    internal void NavigateForTest(string tag) {
        foreach (var entry in Nav.MenuItems) {
            if (entry is NavigationViewItem item && item.Tag is string t && t == tag) {
                Nav.SelectedItem = item;
                return;
            }
        }
    }
    internal string? CurrentPageTypeName => MainFrame.Content?.GetType().FullName;
}
