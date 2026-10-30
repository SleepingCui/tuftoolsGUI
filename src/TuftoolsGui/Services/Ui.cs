using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace TuftoolsGui.Services;

public static class Ui {
    public static void Info(InfoBar bar, string message, InfoBarSeverity severity = InfoBarSeverity.Informational) {
        bar.Severity = severity;
        bar.Message = message;
        bar.IsOpen = true;
    }
    public static void Close(InfoBar bar) {
        bar.IsOpen = false;
    }
    public static void Clear(InfoBar bar) {
        bar.Severity = InfoBarSeverity.Informational;
        bar.Message = "";
        bar.IsOpen = false;
    }
    public static void Fail(InfoBar bar, Exception ex) {
        var msg = ex is ToolException te ? "[" + te.Code + "] " + te.Message : ex.Message;
        Info(bar, msg, InfoBarSeverity.Error);
    }
    public static void Busy(ProgressRing? ring, bool on) {
        if (ring is not null) ring.IsActive = on;
    }
    public static bool OptDouble(TextBox box, string name, out double? value) {
        value = null;
        var text = box.Text.Trim();
        if (text.Length == 0) return true;
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) {
            value = d;
            return true;
        }
        throw new ToolException("invalid_input", name + " 不是合法数字: " + text);
    }
    public static double? OptInt(TextBox box, string name) {
        var text = box.Text.Trim();
        if (text.Length == 0) return null;
        if (long.TryParse(text, out var l)) return l;
        throw new ToolException("invalid_input", name + " 不是合法整数: " + text);
    }
    public static string Text(TextBox box) => box.Text.Trim();
}

public static class Pickers {
    static void Init(object picker) {
        var window = AppState.MainWindow;
        if (window is null) throw new ToolException("picker", "主窗口尚未就绪。");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
    public static async Task<string?> PickExeAsync() {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add(".exe");
        Init(picker);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
    public static async Task<string?> PickFolderAsync() {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add("*");
        Init(picker);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
