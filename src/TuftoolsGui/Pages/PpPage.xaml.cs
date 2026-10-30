using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TuftoolsGui.Models;
using TuftoolsGui.Services;

namespace TuftoolsGui.Pages;

public sealed partial class PpPage : Page {
    public PpPage() {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }
    async void Calc_Click(object sender, RoutedEventArgs e) {
        JsonObject req;
        try {
            req = BuildRequest();
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
            return;
        }
        Ui.Busy(BusyRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(req, default, 180);
            var r = resp.Result;
            var rows = new List<KeyValueRow> {
                new() { Key = "得分", Value = Fmt.Num(r.Get("score").Num(), 3) },
                new() { Key = "原始分", Value = Fmt.Num(r.Get("raw_score").Num(), 3) },
                new() { Key = "准确率", Value = Fmt.Num(r.Get("accuracy_pct").Num(), 4) + "%" },
                new() { Key = "加成倍率", Value = Fmt.Num(r.Get("multiplier").Num(), 4) },
                new() { Key = "基础分", Value = Fmt.Num(r.Get("base_score").Num(), 2) + "（来源 " + r.Get("base_source").Str("-") + "）" },
                new() { Key = "物量", Value = r.Get("tilecount").Str("-") },
                new() { Key = "速度倍率", Value = Fmt.Num(r.Get("speed_mod").Num(), 3) },
                new() { Key = "无长条倍率", Value = Fmt.Num(r.Get("empty_tap_mod").Num(), 3) },
                new() { Key = "默认设置倍率", Value = Fmt.Num(r.Get("default_setting_mod").Num(), 3) }
            };
            var level = r.Get("level");
            if (level is not null) {
                rows.Add(new() { Key = "谱面", Value = level.Get("song").Str("-") + " / " + level.Get("artist").Str("-") + " / " + level.Get("difficulty", "name").Str("-") + "（ID " + level.Get("id").Str("-") + "）" });
            }
            var rankChange = r.Get("rank_change");
            if (rankChange is not null) {
                rows.Add(new() { Key = "排位分", Value = Fmt.Num(rankChange.Get("ranked_score", "old").Num(), 2) + " 变为 " + Fmt.Num(rankChange.Get("ranked_score", "new").Num(), 2) + "，增加 " + Fmt.Num(rankChange.Get("ranked_score", "added").Num(), 2) });
                rows.Add(new() { Key = "全球名次", Value = rankChange.Get("global", "old").Str("-") + " 变为 " + rankChange.Get("global", "new").Str("-") + "，变化 " + rankChange.Get("global", "delta").Str("-") });
                rows.Add(new() { Key = "国家名次", Value = rankChange.Get("country", "old").Str("-") + " 变为 " + rankChange.Get("country", "new").Str("-") + "，变化 " + rankChange.Get("country", "delta").Str("-") });
            }
            rows.AddRange(r.Flatten());
            SummaryText.Visibility = Visibility.Collapsed;
            ResultRows.ItemsSource = rows;
            Ui.Info(StatusBar, "计算完成，耗时 " + resp.ElapsedMs + " ms。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            SummaryText.Visibility = Visibility.Visible;
            SummaryText.Text = "计算失败: " + ex.Message;
            ResultRows.ItemsSource = null;
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(BusyRing, false);
        }
    }
    JsonObject BuildRequest() {
        var req = new JsonObject { ["op"] = "pp.calc" };
        var levelId = Ui.Text(LevelIdBox);
        if (levelId.Length > 0) {
            req["levelId"] = levelId;
        } else {
            var baseScore = Ui.OptDouble(BaseScoreBox, "基础分", out var bs);
            var tiles = Ui.OptDouble(TilecountBox, "物量", out var tc);
            if (!baseScore || !tiles) throw new ToolException("invalid_input", "基础分或物量不是合法数字。");
            if (bs is not null) req["baseScore"] = bs.Value;
            if (tc is null) throw new ToolException("invalid_input", "不按谱面 ID 计算时必须填写物量。");
            req["tilecount"] = tc.Value;
            var diff = Ui.Text(DifficultyBox);
            if (diff.Length > 0) req["difficulty"] = diff;
        }
        var xaccText = Ui.Text(XaccBox);
        if (xaccText.Length == 0) throw new ToolException("invalid_input", "必须填写准确率。");
        if (!double.TryParse(xaccText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var xacc)) throw new ToolException("invalid_input", "准确率不是合法数字: " + xaccText);
        req["xacc"] = xacc;
        var misses = Ui.OptDouble(MissesBox, "漏击", out var miss);
        if (!misses) throw new ToolException("invalid_input", "漏击不是合法数字。");
        if (miss is double m && m > 0) req["misses"] = m;
        var speedOk = Ui.OptDouble(SpeedBox, "速度", out var speed);
        if (!speedOk) throw new ToolException("invalid_input", "速度不是合法数字。");
        if (speed is double sp && Math.Abs(sp - 1) > 1e-9) req["speed"] = sp;
        if (NoHoldTapSwitch.IsChecked == true) req["noHoldTap"] = true;
        if (MarathonSwitch.IsChecked == true) req["marathon"] = true;
        if (RankChangeSwitch.IsChecked == true) {
            var key = Ui.Text(PlayerKeyBox);
            if (key.Length == 0) throw new ToolException("invalid_input", "开启排位分预览时必须填写玩家 ID 或名称。");
            req["rankChange"] = true;
            switch (PlayerBy.SelectedIndex) {
                case 2: req["discord"] = key; break;
                case 1: req["query"] = key; break;
                default: req["id"] = key; break;
            }
        }
        return req;
    }
}
