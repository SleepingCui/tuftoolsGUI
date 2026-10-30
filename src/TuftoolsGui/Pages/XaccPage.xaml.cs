using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TuftoolsGui.Models;
using TuftoolsGui.Services;

namespace TuftoolsGui.Pages;

public sealed partial class XaccPage : Page {
    public XaccPage() {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }
    void XPerfect_Toggled(object sender, RoutedEventArgs e) {
        if (XpPanel is not null) XpPanel.Visibility = XPerfectSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }
    async void Calc_Click(object sender, RoutedEventArgs e) {
        var counts = new JsonObject {
            ["perfect"] = ToInt(CPerfect.Value),
            ["lPerfect"] = ToInt(CLPerfect.Value),
            ["early"] = ToInt(CEarly.Value),
            ["late"] = ToInt(CLate.Value),
            ["tooEarly"] = ToInt(CTooEarly.Value),
            ["ePerfect"] = ToInt(CEPerfect.Value),
            ["failMiss"] = ToInt(CFailMiss.Value)
        };
        var xperfect = XPerfectSwitch.IsOn;
        if (xperfect) {
            counts["xperfect"] = ToInt(CXPerfect.Value);
            counts["+perfect"] = ToInt(CPerfectPlus.Value);
            counts["-perfect"] = ToInt(CPerfectMinus.Value);
        }
        Ui.Busy(CalcRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "xacc.calc", ["counts"] = counts, ["xperfect"] = xperfect });
            var r = resp.Result;
            var xacc = r.Get("xacc").Num();
            var rows = new List<KeyValueRow> {
                new() { Key = "XACC", Value = Fmt.Num(xacc, 6) + "%" },
                new() { Key = "加权得分单位", Value = r.Get("weighted").Str("-") },
                new() { Key = "判定总数", Value = r.Get("total").Str("-") },
                new() { Key = "得分单位", Value = r.Get("score_units").Str("-") },
                new() { Key = "缩放", Value = r.Get("score_scale").Str("-") },
                new() { Key = "模式", Value = r.Get("xperfect").Bool() ? "XPerfect（9 键）" : "普通（7 键）" },
                new() { Key = "判定分布", Value = CountsText(r.Get("counts")) }
            };
            rows.AddRange(r.Flatten());
            CalcSummary.Visibility = Visibility.Collapsed;
            CalcRows.ItemsSource = rows;
            Ui.Info(StatusBar, "计算完成。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            CalcSummary.Visibility = Visibility.Visible;
            CalcSummary.Text = "计算失败: " + ex.Message;
            CalcRows.ItemsSource = null;
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(CalcRing, false);
        }
    }
    async void Reverse_Click(object sender, RoutedEventArgs e) {
        JsonObject? req = null;
        try {
            req = BuildReverseRequest(false);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
            return;
        }
        Ui.Busy(RevRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(req, default, 240);
            var r = resp.Result;
            var revRows = new List<KeyValueRow> {
                new() { Key = "状态", Value = r.Get("status").Str("-") + "（" + r.Get("status_code").Str("-") + "）" },
                new() { Key = "目标准确率", Value = r.Get("target").Str("-") + "%" },
                new() { Key = "实际准确率", Value = r.Get("xacc_text").Str("-") + "%" },
                new() { Key = "小数位", Value = r.Get("decimals").Str("-") },
                new() { Key = "精确", Value = r.Get("exact").Bool() ? "是" : "否" },
                new() { Key = "最优", Value = r.Get("optimal").Bool() ? "是" : "否" },
                new() { Key = "求解器", Value = r.Get("solver").Str("-") },
                new() { Key = "节点", Value = r.Get("nodes").Str("-") },
                new() { Key = "剪枝", Value = r.Get("pruned").Str("-") },
                new() { Key = "单元", Value = r.Get("cells").Str("-") },
                new() { Key = "耗时", Value = r.Get("elapsed_ms").Str("-") + " ms" },
                new() { Key = "判定分布", Value = CountsText(r.Get("counts")) }
            };
            var message = r.Get("message").Str();
            if (message.Length > 0) revRows.Add(new() { Key = "说明", Value = message });
            var note = r.Get("note").Str();
            if (note.Length > 0) revRows.Add(new() { Key = "备注", Value = note });
            revRows.AddRange(r.Flatten());
            RevSummary.Visibility = Visibility.Collapsed;
            RevRows.ItemsSource = revRows;
            SolutionRows.ItemsSource = null;
            Ui.Info(StatusBar, "反推完成，状态 " + r.Get("status_code").Str("-") + "。", r.Get("found").Bool() ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        } catch (Exception ex) {
            RevSummary.Visibility = Visibility.Visible;
            RevSummary.Text = "反推失败: " + ex.Message;
            RevRows.ItemsSource = null;
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(RevRing, false);
        }
    }
    async void Equivalent_Click(object sender, RoutedEventArgs e) {
        JsonObject? req = null;
        try {
            req = BuildReverseRequest(true);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
            return;
        }
        Ui.Busy(RevRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(req, default, 480);
            var r = resp.Result;
            var solutions = r.Get("solutions");
            var rows = new List<SolutionRow>();
            var index = 1;
            foreach (var s in solutions.Items()) rows.Add(SolutionRow.From(index++, s));
            SolutionRows.ItemsSource = rows;
            var revRows = new List<KeyValueRow> {
                new() { Key = "状态", Value = r.Get("status").Str("-") + "（" + r.Get("status_code").Str("-") + "）" },
                new() { Key = "目标准确率", Value = r.Get("target").Str("-") + "%" },
                new() { Key = "实际准确率", Value = r.Get("xacc_text").Str("-") + "%" },
                new() { Key = "组合数", Value = r.Get("count").Str("-") + (r.Get("truncated").Bool() ? "（已达上限，结果被截断）" : "") },
                new() { Key = "求解器", Value = r.Get("solver").Str("-") },
                new() { Key = "耗时", Value = r.Get("elapsed_ms").Str("-") + " ms" },
                new() { Key = "判定分布", Value = CountsText(r.Get("counts")) }
            };
            var message = r.Get("message").Str();
            if (message.Length > 0) revRows.Add(new() { Key = "说明", Value = message });
            revRows.AddRange(r.Flatten());
            RevSummary.Visibility = Visibility.Collapsed;
            RevRows.ItemsSource = revRows;
            Ui.Info(StatusBar, "找到 " + rows.Count + " 组等效判定组合。", rows.Count > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        } catch (Exception ex) {
            RevSummary.Visibility = Visibility.Visible;
            RevSummary.Text = "换组合失败: " + ex.Message;
            SolutionRows.ItemsSource = null;
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(RevRing, false);
        }
    }
    JsonObject BuildReverseRequest(bool equivalents) {
        var req = new JsonObject { ["op"] = equivalents ? "xacc.equivalents" : "xacc.reverse" };
        req["total"] = (long)RevTotal.Value;
        var xacc = Ui.Text(RevXacc);
        if (xacc.Length == 0) throw new ToolException("invalid_input", "必须填写目标准确率。");
        req["xacc"] = xacc;
        if (RevXPerfect.IsOn) req["xperfect"] = true;
        var solver = RevSolver.SelectedIndex switch { 1 => "exact_dp", 2 => "branch_and_bound", 3 => "legacy_map_dp", _ => "automatic" };
        req["solver"] = solver;
        var fixedCounts = new JsonObject();
        AddFixed(fixedCounts, "perfect", LockPerfect);
        AddFixed(fixedCounts, "lPerfect", LockLPerfect);
        AddFixed(fixedCounts, "failMiss", LockFailMiss);
        AddFixed(fixedCounts, "early", LockEarly);
        if (fixedCounts.Count > 0) req["fixed"] = fixedCounts;
        if (equivalents) {
            var max = Ui.OptInt(RevMaxSolutions, "组合上限");
            if (max is double m) req["maxSolutions"] = (long)m;
        }
        return req;
    }
    static void AddFixed(JsonObject target, string key, TextBox box) {
        var text = Ui.Text(box);
        if (text.Length == 0) return;
        if (!long.TryParse(text, out var value)) throw new ToolException("invalid_input", "锁定项 " + key + " 不是合法整数: " + text);
        target[key] = value;
    }
    async void Model_Click(object sender, RoutedEventArgs e) {
        Ui.Busy(ModelRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "xacc.model" });
            var r = resp.Result;
            ModelRows.ItemsSource = r.Flatten();
            var costs = r.Get("costs");
            CostTooEarly.Value = costs.Get("tooEarly").Num() ?? 0;
            CostEarly.Value = costs.Get("early").Num() ?? 0;
            CostLate.Value = costs.Get("late").Num() ?? 0;
            CostEPerfect.Value = costs.Get("ePerfect").Num() ?? 0;
            CostLPerfect.Value = costs.Get("lPerfect").Num() ?? 0;
            CostPlusPerfect.Value = costs.Get("+perfect").Num() ?? 0;
            CostMinusPerfect.Value = costs.Get("-perfect").Num() ?? 0;
            MaxSecondsBox.Text = r.Get("max_seconds").Str();
            MaxNodesBox.Text = r.Get("max_nodes").Str();
            MaxDpCellsBox.Text = r.Get("max_dp_cells").Str();
            CostScaleBox.Text = r.Get("cost_scale").Str();
            Ui.Info(StatusBar, "已读取模型。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(ModelRing, false);
        }
    }
    async void Config_Click(object sender, RoutedEventArgs e) {
        JsonObject req;
        try {
            var costs = new JsonObject {
                ["tooEarly"] = CostTooEarly.Value,
                ["early"] = CostEarly.Value,
                ["late"] = CostLate.Value,
                ["ePerfect"] = CostEPerfect.Value,
                ["lPerfect"] = CostLPerfect.Value,
                ["+perfect"] = CostPlusPerfect.Value,
                ["-perfect"] = CostMinusPerfect.Value
            };
            req = new JsonObject { ["op"] = "xacc.config", ["costs"] = costs };
            var maxSeconds = Ui.OptDouble(MaxSecondsBox, "maxSeconds", out var ms);
            if (!maxSeconds) throw new ToolException("invalid_input", "maxSeconds 不是合法数字。");
            if (ms is double m) req["maxSeconds"] = m;
            var nodes = Ui.OptInt(MaxNodesBox, "maxNodes");
            if (nodes is double n) req["maxNodes"] = (long)n;
            var cells = Ui.OptInt(MaxDpCellsBox, "maxDpCells");
            if (cells is double c) req["maxDpCells"] = (long)c;
            var scale = Ui.OptDouble(CostScaleBox, "costScale", out var cs);
            if (!scale) throw new ToolException("invalid_input", "costScale 不是合法数字。");
            if (cs is double s) req["costScale"] = s;
            req["save"] = TryOnlySwitch.IsChecked != true;
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
            return;
        }
        Ui.Busy(ModelRing, true);
        try {
            var resp = await TuftoolsClient.CallAsync(req);
            var r = resp.Result;
            ModelRows.ItemsSource = r.Flatten();
            var modified = r.Get("modified").Bool();
            var saved = r.Get("saved").Bool();
            var rounded = r.Get("rounded").Bool();
            Ui.Info(StatusBar, "配置已应用" + (saved ? "并写入 settings.json" : "（仅试算，未落盘）") + (modified ? "" : "，没有改动") + (rounded ? "，系数有取整" : "") + "。", InfoBarSeverity.Success);
        } catch (Exception ex) {
            Ui.Fail(StatusBar, ex);
        } finally {
            Ui.Busy(ModelRing, false);
        }
    }
    static long ToInt(double value) => (long)Math.Round(Math.Max(0, value));
    static string CountsText(JsonElement? counts) {
        if (counts is not JsonElement e) return "-";
        var parts = new List<string>();
        foreach (var p in e.EnumerateObject()) parts.Add(p.Name + " " + JsonUtil.FormatScalar(p.Value));
        return string.Join("  ", parts);
    }
}
