using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TuftoolsGui.Models;
using TuftoolsGui.Services;

namespace TuftoolsGui.Pages;

public sealed partial class RankPage : Page {
    private static string RankText(JsonElement? value) {
        if (!value.HasValue || value.Value.ValueKind == JsonValueKind.Null) return "未上榜";
        return value.Value.Str("-");
    }

    private static string DeltaText(JsonElement? value) {
        if (!value.HasValue || value.Value.ValueKind == JsonValueKind.Null) return "无变化";
        var delta = value.Value.Int();
        if (delta == 0) return "持平";
        return delta < 0 ? "前进 " + (-delta) + " 名" : "后退 " + delta + " 名";
    }
    public RankPage() {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }
    async void Preview_Click(object sender, RoutedEventArgs e) {
        var key = Ui.Text(PlayerKeyBox);
        if (key.Length == 0) {
            Ui.Info(StatusBar, "请填写玩家 ID、名称或 Discord 用户名。", InfoBarSeverity.Warning);
            return;
        }
        var req = new JsonObject { ["op"] = "rank.preview" };
        switch (PlayerBy.SelectedIndex) {
            case 2: req["discord"] = key; break;
            case 1: req["query"] = key; break;
            default: req["id"] = key; break;
        }
        try {
            var scoreOk = Ui.OptDouble(ScoreBox, "候选分数", out var score);
            if (!scoreOk) throw new ToolException("invalid_input", "候选分数不是合法数字。");
            if (score is null) throw new ToolException("invalid_input", "必须填写候选分数。");
            req["score"] = score.Value;
            var levelId = Ui.Text(LevelIdBox);
            if (levelId.Length > 0) req["levelId"] = levelId;
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
            return;
        }
        Ui.Busy(BusyRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(req, default, 240);
            var r = resp.Result;
            var rows = new List<KeyValueRow> {
                new() { Key = "玩家", Value = r.Get("player", "name").Str("-") + "（ID " + r.Get("player", "id").Str("-") + "，国家 " + r.Get("player", "country").Str("-") + "）" },
                new() { Key = "候选分数", Value = Fmt.Num(r.Get("candidate_score").Num(), 2) }
            };
            if (r.Get("level_id").Str().Length > 0) rows.Add(new() { Key = "替换谱面", Value = r.Get("level_id").Str() });
            rows.Add(new() { Key = "排位分", Value = Fmt.Num(r.Get("ranked_score", "old").Num(), 2) + " 变为 " + Fmt.Num(r.Get("ranked_score", "new").Num(), 2) + "，增加 " + Fmt.Num(r.Get("ranked_score", "added").Num(), 2) });
            rows.Add(new() { Key = "全球名次", Value = RankText(r.Get("global", "old")) + " 变为 " + RankText(r.Get("global", "new")) + "，" + DeltaText(r.Get("global", "delta")) });
            rows.Add(new() { Key = "国家名次", Value = RankText(r.Get("country", "old")) + " 变为 " + RankText(r.Get("country", "new")) + "，" + DeltaText(r.Get("country", "delta")) });
            rows.Add(new() { Key = "匹配方式", Value = r.Get("matched_by").Str("-") + "，耗时 " + resp.ElapsedMs + " ms" });
            rows.AddRange(r.Flatten());
            SummaryText.Visibility = Visibility.Collapsed;
            ResultRows.ItemsSource = rows;
            Ui.Info(StatusBar, "预览完成。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            SummaryText.Visibility = Visibility.Visible;
            SummaryText.Text = "预览失败: " + ex.Message;
            ResultRows.ItemsSource = null;
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
}
