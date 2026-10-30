using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TuftoolsGui.Services;

namespace TuftoolsGui.Pages;

public sealed partial class OverviewPage : Page {
    public OverviewPage() {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        Loaded += (_, _) => Reload();
    }
    void Reload() {
        var s = AppState.Settings;
        ExeBox.Text = s.ExePath;
        ProxyBox.Text = s.Proxy;
        DownloadBox.Text = s.DownloadDir;
        NoCacheSwitch.IsOn = s.NoCache;
        if (s.ExePath.Length == 0 || !File.Exists(s.ExePath)) {
            Ui.Info(StatusBar, "还没有可用的 tuftools.exe：点「从 GitHub 下载最新版」自动获取，或用「导入 tuftools.exe」选择本机文件。", InfoBarSeverity.Warning);
        } else {
            InstallText.Text = "当前文件: " + TuftoolsInstaller.Describe(s.ExePath);
        }
    }
    async void Browse_Click(object sender, RoutedEventArgs e) {
        try {
            var path = await Pickers.PickExeAsync();
            if (string.IsNullOrEmpty(path)) return;
            ExeBox.Text = path;
            SaveSettings();
            Ui.Info(StatusBar, "已设置程序路径。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        }
    }
    void Detect_Click(object sender, RoutedEventArgs e) {
        var path = AppState.DetectExe();
        if (string.IsNullOrEmpty(path)) {
            Ui.Info(StatusBar, "没有找到 tuftools.exe，请手动浏览选择。", InfoBarSeverity.Warning);
            return;
        }
        ExeBox.Text = path;
        SaveSettings();
        Ui.Info(StatusBar, "已找到: " + path, InfoBarSeverity.Success);
    }
    async void BrowseDir_Click(object sender, RoutedEventArgs e) {
        try {
            var path = await Pickers.PickFolderAsync();
            if (string.IsNullOrEmpty(path)) return;
            DownloadBox.Text = path;
            SaveSettings();
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        }
    }
    void OpenDir_Click(object sender, RoutedEventArgs e) {
        var dir = DownloadBox.Text.Trim();
        if (dir.Length == 0) return;
        try {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        }
    }
    void Save_Click(object sender, RoutedEventArgs e) {
        SaveSettings();
        Ui.Info(StatusBar, "设置已保存。", InfoBarSeverity.Success);
    }
    void SaveSettings() {
        var s = AppState.Settings;
        s.ExePath = Ui.Text(ExeBox);
        s.Proxy = Ui.Text(ProxyBox);
        s.DownloadDir = Ui.Text(DownloadBox);
        s.NoCache = NoCacheSwitch.IsOn;
        AppState.NotifySettingsChanged();
    }
    async void Test_Click(object sender, RoutedEventArgs e) {
        SaveSettings();
        Ui.Busy(BusyRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "version" });
            var version = resp.Result.Get("version").Str("未知");
            LoadDetails(resp, version);
            Ui.Info(StatusBar, "连接成功，tuftools 版本 " + version + "。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            VersionText.Text = "测试失败: " + ex.Message;
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }

    async void Import_Click(object sender, RoutedEventArgs e) {
        var path = await Pickers.PickExeAsync();
        if (string.IsNullOrEmpty(path)) return;
        InstallRing.IsActive = true;
        ImportButton.IsEnabled = false;
        DownloadButton.IsEnabled = false;
        InstallText.Text = "";
        try {
            var progress = new Progress<string>(message => InstallText.Text = message);
            var installed = await TuftoolsInstaller.ImportAsync(path, progress);
            ApplyInstalled(installed, "已导入");
        } catch (Exception ex) {
            InstallText.Text = "导入失败，原文件未改动";
            Ui.Fail(StatusBar, ex);
        } finally {
            InstallRing.IsActive = false;
            ImportButton.IsEnabled = true;
            DownloadButton.IsEnabled = true;
        }
    }

    async void Download_Click(object sender, RoutedEventArgs e) {
        SaveSettings();
        InstallRing.IsActive = true;
        ImportButton.IsEnabled = false;
        DownloadButton.IsEnabled = false;
        InstallText.Text = "正在查询 GitHub release。";
        try {
            var release = await TuftoolsInstaller.FetchLatestAsync(AppState.Settings.Proxy, CancellationToken.None);
            InstallText.Text = "正在下载 " + release.Tag + "（" + (release.Size / 1048576.0).ToString("0.0") + " MB）。";
            var progress = new Progress<string>(message => InstallText.Text = message);
            var installed = await TuftoolsInstaller.DownloadAsync(release, AppState.Settings.Proxy, progress, CancellationToken.None);
            ApplyInstalled(installed, "已下载 " + release.Tag);
        } catch (Exception ex) {
            InstallText.Text = "下载失败，原文件未改动";
            Ui.Fail(StatusBar, ex);
        } finally {
            InstallRing.IsActive = false;
            ImportButton.IsEnabled = true;
            DownloadButton.IsEnabled = true;
        }
    }

    async void ApplyInstalled(string installed, string prefix) {
        ExeBox.Text = installed;
        SaveSettings();
        InstallText.Text = prefix + "，正在校验文件。";
        try {
            var resp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "version" }, default, 60);
            var version = resp.Result.Get("version").Str("未知");
            LoadDetails(resp, version);
            InstallText.Text = prefix + "，版本 " + version + "，" + TuftoolsInstaller.Describe(installed);
            Ui.Info(StatusBar, prefix + "，版本 " + version + "，协议 " + resp.Result.Get("protocol").Str("未知") + "。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            InstallText.Text = prefix + "，但校验调用失败: " + ex.Message;
            Ui.Info(StatusBar, InstallText.Text, InfoBarSeverity.Warning);
        }
    }

    void LoadDetails(ToolResponse resp, string version) {
        var r = resp.Result;
        var lines = new List<string> {
            "名称: " + r.Get("name").Str("未知"),
            "版本: " + version,
            "协议: " + r.Get("protocol").Str("未知"),
            "构建时间: " + r.Get("build_time").Str("未知"),
            "API: " + r.Get("api_base").Str("未知"),
            "耗时: " + resp.ElapsedMs + " ms",
            "文件: " + TuftoolsInstaller.Describe(AppState.Settings.ExePath),
            "能力: " + r.Get("ops").JoinStrings(", ")
        };
        VersionText.Text = string.Join(Environment.NewLine, lines);
    }
}
