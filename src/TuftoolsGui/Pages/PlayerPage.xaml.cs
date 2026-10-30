using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TuftoolsGui.Models;
using TuftoolsGui.Services;

namespace TuftoolsGui.Pages;

public sealed partial class PlayerPage : Page {
    string _passPlayerKey = "";
    int _passOffset;
    int _passTotal;
    bool _moreByQuery;
    readonly List<PassRow> _passRows = new();
    public PlayerPage() {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        Loaded += (_, _) => {
            if (PlayerQuery.Text.Length == 0) PlayerQuery.Focus(FocusState.Programmatic);
        };
    }
    async void PlayerQuery_KeyDown(object sender, KeyRoutedEventArgs e) {
        if (e.Key == Windows.System.VirtualKey.Enter) {
            Search_Click(sender, new RoutedEventArgs());
            await Task.CompletedTask;
        }
    }
    async void Search_Click(object sender, RoutedEventArgs e) {
        var q = Ui.Text(PlayerQuery);
        if (q.Length == 0) {
            Ui.Info(StatusBar, "请输入玩家名称、ID 或 Discord 用户名。", InfoBarSeverity.Warning);
            return;
        }
        Ui.Busy(BusyRing, true);
        try {
            var req = new JsonObject { ["op"] = "player.search" };
            if (SearchBy.SelectedIndex == 1) req["discord"] = q;
            else req["query"] = q;
            var resp = await TuftoolsClient.CallAsync(req);
            var results = resp.Result.Get("results");
            var rows = results.Items().Select(PlayerRow.From).ToList();
            PlayerResults.ItemsSource = rows;
            if (rows.Count == 0) {
                Ui.Info(StatusBar, "没有匹配的玩家。", InfoBarSeverity.Warning);
                ShowEmpty("没有匹配的玩家。");
                return;
            }
            Ui.Info(StatusBar, "找到 " + rows.Count + " 个玩家，单击一行查看详情。", InfoBarSeverity.Success);
            if (rows.Count == 1) {
                PlayerResults.SelectedIndex = 0;
                await LoadDetailsAsync(rows[0].Id, false);
            }
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
    async void PlayerResults_ItemClick(object sender, ItemClickEventArgs e) {
        if (e.ClickedItem is not PlayerRow row) return;
        await LoadDetailsAsync(row.Id, false);
    }
    async Task LoadDetailsAsync(string key, bool byQuery) {
        Ui.Busy(BusyRing, true);
        try {
            var req = new JsonObject {
                ["op"] = "player.details",
                ["includeRanks"] = true,
                ["includePasses"] = true,
                ["passLimit"] = 100,
                ["passesFetchCap"] = 64
            };
            if (byQuery) req["query"] = key;
            else req["id"] = key;
            var resp = await TuftoolsClient.CallAsync(req, default, 180);
            var player = resp.Result.Get("player");
            var ranks = resp.Result.Get("ranks");
            PassesHeader.Text = "";
            ShowDetails(player, ranks, resp.Result.Get("matched_by").Str());
            _passPlayerKey = player.Get("id").Str(key);
            _moreByQuery = false;
            var passes = resp.Result.Get("passes");
            _passRows.Clear();
            foreach (var r in passes.Get("rows").Items()) _passRows.Add(PassRow.From(r));
            Passes.ItemsSource = null;
            Passes.ItemsSource = _passRows;
            _passTotal = passes.Get("total").Int() ?? _passRows.Count;
            _passOffset = passes.Get("rows").Count();
            PassesHeader.Text = "共 " + _passTotal + " 条，已加载 " + _passRows.Count + (passes.Get("truncated").Bool() ? "（接口已截断）" : "");
            MorePassesButton.IsEnabled = _passRows.Count < _passTotal;
            Ui.Info(StatusBar, "已加载玩家 " + player.Get("name").Str("") + " 的详情。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
    void ShowEmpty(string message) {
        PlayerDetailPanel.Visibility = Visibility.Collapsed;
        PlayerEmpty.Visibility = Visibility.Visible;
        PlayerEmpty.Text = message;
    }
    void ShowDetails(JsonElement? player, JsonElement? ranks, string matchedBy) {
        if (player is null) {
            ShowEmpty("没有玩家数据。");
            return;
        }
        var p = player.Value;
        PlayerEmpty.Visibility = Visibility.Collapsed;
        PlayerDetailPanel.Visibility = Visibility.Visible;
        PlayerTitle.Text = p.Get("name").Str("未知");
        var meta = new List<string> {
            "ID " + p.Get("id").Str("?"),
            "国家 " + p.Get("country").Str("?")
        };
        var discord = p.Get("discord", "username").Str(p.Get("username").Str(""));
        if (discord.Length > 0) meta.Add("Discord " + discord);
        var topDiff = p.Get("topDiff", "name").Str("");
        if (topDiff.Length > 0) meta.Add("顶级难度 " + topDiff);
        var accuracy = p.Get("averageXacc").Num();
        if (accuracy is not null) meta.Add("平均准确率 " + Fmt.Num(accuracy * 100, 3) + "%");
        if (matchedBy.Length > 0) meta.Add("匹配方式 " + matchedBy);
        PlayerMeta.Text = string.Join("    ", meta);
        ScoreRows.ItemsSource = new List<ScoreRow> {
            ScoreRowOf(player, ranks, "rankedScore", "排位分"),
            ScoreRowOf(player, ranks, "totalScoreV2", "总分"),
            ScoreRowOf(player, ranks, "ppScore", "无瑕分"),
            ScoreRowOf(player, ranks, "wfScore", "首通分"),
            ScoreRowOf(player, ranks, "wfPPScore", "首杀分"),
            ScoreRowOf(player, ranks, "score12K", "12K 分"),
            ScoreRowOf(player, ranks, "generalScore", "全局分")
        };
        StatPasses.Text = p.Get("totalPasses").Str("-");
        StatUniversal.Text = p.Get("universalPassCount").Str("-");
        StatWf.Text = p.Get("worldsFirstCount").Str("-");
        StatWfPP.Text = p.Get("worldsFirstPPCount").Str("-");
    }
    static ScoreRow ScoreRowOf(JsonElement? player, JsonElement? ranks, string key, string label) {
        return new ScoreRow {
            Label = label,
            Value = Fmt.Num(player.Get(key).Num(), 2),
            Global = Fmt.RankText(ranks.Get(key + "_global").Str()),
            Country = Fmt.RankText(ranks.Get(key + "_country").Str())
        };
    }
    async void MorePasses_Click(object sender, RoutedEventArgs e) {
        if (_passPlayerKey.Length == 0) return;
        Ui.Busy(BusyRing, true);
        try {
            var req = new JsonObject {
                ["op"] = "player.passes",
                ["offset"] = _passOffset,
                ["limit"] = 100,
                ["passesFetchCap"] = 64
            };
            if (_moreByQuery) req["query"] = _passPlayerKey;
            else req["id"] = _passPlayerKey;
            var resp = await TuftoolsClient.CallAsync(req, default, 180);
            var rows = resp.Result.Get("rows");
            var added = 0;
            foreach (var r in rows.Items()) {
                _passRows.Add(PassRow.From(r));
                added++;
            }
            Passes.ItemsSource = null;
            Passes.ItemsSource = _passRows;
            _passOffset += added;
            PassesHeader.Text = "共 " + _passTotal + " 条，已加载 " + _passRows.Count;
            MorePassesButton.IsEnabled = added > 0 && _passRows.Count < _passTotal;
            if (added == 0) Ui.Info(StatusBar, "没有更多记录了。", InfoBarSeverity.Informational);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
}
