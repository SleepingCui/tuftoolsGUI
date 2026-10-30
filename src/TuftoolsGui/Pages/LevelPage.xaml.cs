using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TuftoolsGui.Models;
using TuftoolsGui.Services;

namespace TuftoolsGui.Pages;

public sealed partial class LevelPage : Page {
    readonly string[] _sortKeys = { "CLEARS", "DOWNLOADS", "LIKES", "FAVORITES", "BPM", "TILECOUNT", "DURATION", "BASESCORE", "ID" };
    LevelRow? _selected;
    public LevelPage() {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        LevelResults.SelectionChanged += (_, _) => {
            _selected = LevelResults.SelectedItem as LevelRow;
            SelectedHint.Text = _selected is null ? "" : "已选中: " + _selected.Id + " " + _selected.Song;
        };
    }
    async void LevelQuery_KeyDown(object sender, KeyRoutedEventArgs e) {
        if (e.Key == Windows.System.VirtualKey.Enter) {
            Search_Click(sender, new RoutedEventArgs());
            await Task.CompletedTask;
        }
    }
    async void Search_Click(object sender, RoutedEventArgs e) {
        var query = Ui.Text(LevelQuery);
        if (query.Length == 0) {
            Ui.Info(StatusBar, "请输入谱面关键字或 ID。", InfoBarSeverity.Warning);
            return;
        }
        var sort = _sortKeys[Math.Max(0, SortBox.SelectedIndex)] + (OrderBox.SelectedIndex == 1 ? "_ASC" : "_DESC");
        var limit = (int)(Ui.OptInt(LimitBox, "数量") ?? 20);
        if (limit < 1) limit = 1;
        if (limit > 100) limit = 100;
        Ui.Busy(BusyRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "level.search", ["query"] = query, ["sort"] = sort, ["limit"] = limit });
            var rows = resp.Result.Get("results").Items().Select(LevelRow.From).ToList();
            LevelResults.ItemsSource = rows;
            _selected = null;
            SelectedHint.Text = "";
            Ui.Info(StatusBar, "找到 " + rows.Count + " 个谱面（排序 " + sort + "）。" + (resp.Result.Get("hasMore").Bool() ? "还有更多结果。" : ""), InfoBarSeverity.Success);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
    bool RequireSelection() {
        if (_selected is not null && _selected.Id.Length > 0) return true;
        Ui.Info(StatusBar, "请先在搜索结果中选中一个谱面。", InfoBarSeverity.Warning);
        return false;
    }
    async void Detail_Click(object sender, RoutedEventArgs e) {
        if (!RequireSelection()) return;
        Ui.Busy(BusyRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "level.get", ["id"] = _selected!.Id });
            var rows = resp.Result.Flatten();
            DetailRows.ItemsSource = rows;
            Ui.Info(StatusBar, "已加载谱面详情，共 " + rows.Count + " 个字段。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
    async void Cdn_Click(object sender, RoutedEventArgs e) {
        if (!RequireSelection()) return;
        Ui.Busy(BusyRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "level.cdn", ["id"] = _selected!.Id }, default, 180);
            var rows = resp.Result.Flatten();
            DetailRows.ItemsSource = rows;
            LoadPresets(resp.Result);
            Ui.Info(StatusBar, "已加载 CDN 信息与转换预设。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
    void LoadPresets(System.Text.Json.JsonElement result) {
        var items = new List<PresetItem>();
        foreach (var p in result.Get("builtin_presets").Items()) {
            var name = p.Get("name").Str("预设");
            var key = p.Get("key").Str();
            items.Add(new PresetItem { Display = name + "（" + key + "）", Key = key.Length > 0 ? key : null });
        }
        foreach (var c in result.Get("custom_presets").Items()) {
            var name = c.Str();
            items.Add(new PresetItem { Display = "自定义: " + name, Custom = name });
        }
        PresetBox.ItemsSource = items;
        if (items.Count > 0) PresetBox.SelectedIndex = 0;
        var cdnError = result.Get("cdn_error").Str();
        if (cdnError.Length > 0) Ui.Info(StatusBar, "CDN 信息获取失败: " + cdnError, InfoBarSeverity.Warning);
    }
    void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        var transform = ModeBox.SelectedIndex == 1;
        if (PresetBox is not null) PresetBox.IsEnabled = transform;
    }
    async void Download_Click(object sender, RoutedEventArgs e) {
        if (!RequireSelection()) return;
        var mode = ModeBox.SelectedIndex switch { 1 => "transform", 2 => "v2", _ => "original" };
        var dir = AppState.Settings.DownloadDir;
        if (string.IsNullOrWhiteSpace(dir)) {
            Ui.Info(StatusBar, "请先在概览页设置下载目录。", InfoBarSeverity.Warning);
            return;
        }
        var req = new JsonObject { ["op"] = "level.download", ["id"] = _selected!.Id, ["mode"] = mode, ["directory"] = dir };
        if (mode == "transform") {
            if (PresetBox.SelectedItem is not PresetItem preset) {
                Ui.Info(StatusBar, "转换模式需要选择预设，请先点击“CDN 与下载信息”加载预设。", InfoBarSeverity.Warning);
                return;
            }
            if (preset.Key is not null) req["preset"] = preset.Key;
            else if (preset.Custom is not null) req["customPreset"] = preset.Custom;
        }
        var fileName = Ui.Text(FileNameBox);
        if (fileName.Length > 0) req["filename"] = fileName;
        Ui.Busy(DownloadRing, true);
        DownloadText.Text = "正在下载，请等待...";
        try {
            var resp = await TuftoolsClient.CallAsync(req, default, 3600);
            var path = resp.Result.Get("path").Str();
            var bytes = Fmt.Bytes(resp.Result.Get("bytes").Num());
            var suffixed = resp.Result.Get("suffixed").Bool();
            DownloadText.Text = "已保存: " + path + "（" + bytes + "）" + (suffixed ? "。同名文件已存在，自动改名。" : "");
            Ui.Info(StatusBar, "下载完成: " + path, InfoBarSeverity.Success);
            var full = resp.Result.Flatten();
            DetailRows.ItemsSource = full;
        } catch (Exception ex) {
            DownloadText.Text = "下载失败: " + ex.Message;
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(DownloadRing, false);
        }
    }
    void OpenDir_Click(object sender, RoutedEventArgs e) {
        var dir = AppState.Settings.DownloadDir;
        if (string.IsNullOrWhiteSpace(dir)) {
            Ui.Info(StatusBar, "请先在概览页设置下载目录。", InfoBarSeverity.Warning);
            return;
        }
        try {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        }
    }
}
