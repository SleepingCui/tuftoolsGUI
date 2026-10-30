using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TuftoolsGui.Services;

namespace TuftoolsGui.Pages;

public sealed partial class ConsolePage : Page {
    const string TemplateRequest = "{\n  \"op\": \"player.details\",\n  \"id\": \"25\",\n  \"includePasses\": false\n}";
    public ConsolePage() {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        if (RequestBox.Text.Length == 0) RequestBox.Text = TemplateRequest;
    }
    async void Send_Click(object sender, RoutedEventArgs e) {
        var text = RequestBox.Text.Trim();
        if (text.Length == 0) {
            Ui.Info(StatusBar, "请输入 JSON 请求。", InfoBarSeverity.Warning);
            return;
        }
        try {
            JsonNode.Parse(text);
        } catch (Exception ex) {
            Ui.Info(StatusBar, "请求不是合法的 JSON: " + ex.Message, InfoBarSeverity.Error);
            return;
        }
        Ui.Busy(BusyRing, true);
        ElapsedText.Text = "";
        try {
            var resp = await TuftoolsClient.SendRawAsync(text, default, 600);
            ResponseBox.Text = PrettyCheck.IsChecked == true ? TryPretty(resp.Raw) : resp.Raw;
            ElapsedText.Text = "耗时 " + resp.ElapsedMs + " ms";
            if (resp.Ok) Ui.Info(StatusBar, "操作 " + resp.Op + " 成功。", InfoBarSeverity.Success);
            else Ui.Info(StatusBar, "操作失败: [" + resp.ErrorCode + "] " + resp.ErrorMessage, InfoBarSeverity.Error);
        } catch (Exception ex) {
            ResponseBox.Text = ex.Message;
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
    void Template_Click(object sender, RoutedEventArgs e) {
        RequestBox.Text = TemplateRequest;
        Ui.Info(StatusBar, "已填入 player.details 模板。", InfoBarSeverity.Informational);
    }
    async void ClearCache_Click(object sender, RoutedEventArgs e) {
        Ui.Busy(BusyRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "cache.clear" });
            var r = resp.Result;
            var cleared = r.Get("cleared").Str("0");
            var skipped = r.Get("skipped").Bool();
            ResponseBox.Text = PrettyCheck.IsChecked == true ? TryPretty(resp.Raw) : resp.Raw;
            Ui.Info(StatusBar, skipped ? "缓存已跳过（当前使用 --no-cache）: " + r.Get("reason").Str("") : "已清空 " + cleared + " 项缓存。", skipped ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
    void Copy_Click(object sender, RoutedEventArgs e) {
        if (ResponseBox.Text.Length == 0) return;
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(ResponseBox.Text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        Ui.Info(StatusBar, "响应已复制到剪贴板。", InfoBarSeverity.Success);
    }
    static string TryPretty(string raw) {
        try {
            var node = JsonNode.Parse(raw);
            if (node is null) return raw;
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        } catch (Exception) {
            return raw;
        }
    }
}
