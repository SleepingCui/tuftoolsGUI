using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml.Controls;
using TuftoolsGui.Models;
using TuftoolsGui.Services;

namespace TuftoolsGui;

// 冒烟自检：仅在设置环境变量 TUFTOOLS_SMOKE=1 时由 App 调用，用于验证各页面可加载、各 op 可调用。
public static class Smoke {
    public static async Task RunAsync(MainWindow window, string logPath) {
        var sb = new StringBuilder();
        var file = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
        void Log(string line) {
            sb.AppendLine(line);
            file.WriteLine(line);
        }
        Log("== 初始化 ==");
        Log("exe: " + AppState.Settings.ExePath);
        Log("proxy: " + (AppState.Settings.Proxy ?? "(直连)"));
        try {
            Log("版本: " + (await Try(() => TuftoolsClient.CallAsync(new JsonObject { ["op"] = "version" }), "version")));
        } catch (Exception ex) {
            Log("版本调用失败: " + ex.Message);
        }
        Log("== 页面加载 ==");
        var tags = new[] { "overview", "player", "level", "pp", "rank", "xacc", "console" };
        foreach (var tag in tags) {
            try {
                window.NavigateForTest(tag);
                await Task.Delay(500);
                var content = window.CurrentPageTypeName;
                Log(tag + " -> " + (content ?? "(null)"));
            } catch (Exception ex) {
                Log(tag + " 加载失败: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        Log("== 数据接口 ==");
        try {
            var search = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "player.search", ["query"] = "jipper", ["limit"] = 3 });
            var rows = search.Result.Get("results").Items().Select(PlayerRow.From).ToList();
            Log("player.search count=" + rows.Count + (rows.Count > 0 ? " first=" + rows[0].Id + " " + rows[0].Name + " ranked=" + rows[0].Ranked : ""));
        } catch (Exception ex) {
            Log("player.search 失败: " + ex.Message);
        }
        try {
            var details = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "player.details", ["id"] = "25", ["includeRanks"] = true, ["includePasses"] = true, ["passLimit"] = 5 });
            var player = PlayerRow.From(details.Result.Get("player").GetValueOrDefault());
            var ranks = details.Result.Get("ranks");
            var passes = details.Result.Get("passes");
            var passRows = passes.Get("rows").Items().Select(PassRow.From).ToList();
            Log("player.details " + player.Name + " rankedScore=" + player.Ranked + " rankedGlobal=" + ranks.Get("rankedScore_global").Str() + " passesTotal=" + passes.Get("total").Str() + " rows=" + passRows.Count);
            if (passRows.Count > 0) Log("first pass " + passRows[0].LevelId + " " + passRows[0].Song + " " + passRows[0].Difficulty + " " + passRows[0].Score + " " + passRows[0].Accuracy + " " + passRows[0].Note);
            Log("flatten rows=" + details.Result.Flatten().Count);
        } catch (Exception ex) {
            Log("player.details 失败: " + ex.Message);
        }
        try {
            var ls = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "level.search", ["query"] = "taiga", ["sort"] = "CLEARS_DESC", ["limit"] = 3 });
            var rows = ls.Result.Get("results").Items().Select(LevelRow.From).ToList();
            Log("level.search count=" + rows.Count + (rows.Count > 0 ? " first=" + rows[0].Id + " " + rows[0].Song + " " + rows[0].Difficulty + " clears=" + rows[0].Clears : ""));
        } catch (Exception ex) {
            Log("level.search 失败: " + ex.Message);
        }
        try {
            var cdn = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "level.cdn", ["id"] = "11372" });
            var level = cdn.Result.Get("level");
            Log("level.cdn " + level.Get("song").Str() + " tilecount=" + level.Get("tilecount").Str() + " transform_options=" + cdn.Result.Get("transform_options").IsObject() + " presets=" + cdn.Result.Get("builtin_presets").Count());
            Log("level.cdn flatten rows=" + cdn.Result.Flatten("", 10).Count);
        } catch (Exception ex) {
            Log("level.cdn 失败: " + ex.Message);
        }
        try {
            var pp = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "pp.calc", ["baseScore"] = 2500, ["tilecount"] = 800, ["xacc"] = 98.5 });
            Log("pp.calc score=" + pp.Result.Get("score").Str() + " accuracy_pct=" + pp.Result.Get("accuracy_pct").Str() + " multiplier=" + pp.Result.Get("multiplier").Str());
        } catch (Exception ex) {
            Log("pp.calc 失败: " + ex.Message);
        }
        try {
            var calc = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "xacc.calc", ["counts"] = new JsonObject { ["perfect"] = 1000, ["failMiss"] = 5 } });
            Log("xacc.calc xacc=" + calc.Result.Get("xacc_text").Str() + " total=" + calc.Result.Get("total").Str());
        } catch (Exception ex) {
            Log("xacc.calc 失败: " + ex.Message);
        }
        try {
            var rev = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "xacc.reverse", ["total"] = 1000, ["xacc"] = "99.28", ["solver"] = "automatic" });
            Log("xacc.reverse xacc=" + rev.Result.Get("xacc").Str() + " perfect=" + rev.Result.Get("counts").Get("perfect").Str() + " solver=" + rev.Result.Get("solver").Str());
        } catch (Exception ex) {
            Log("xacc.reverse 失败: " + ex.Message);
        }
        try {
            var eq = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "xacc.equivalents", ["total"] = 1000, ["xacc"] = "99.28", ["maxSolutions"] = 3 });
            var sols = eq.Result.Get("solutions").Items().Select((el, i) => SolutionRow.From(i + 1, el)).ToList();
            Log("xacc.equivalents count=" + eq.Result.Get("count").Str() + " rows=" + sols.Count + (sols.Count > 0 ? " firstPerfect=" + sols[0].Perfect : ""));
        } catch (Exception ex) {
            Log("xacc.equivalents 失败: " + ex.Message);
        }
        try {
            var model = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "xacc.model" });
            Log("xacc.model solvers=" + model.Result.Get("solvers").Count() + " costs=" + model.Result.Get("costs").IsObject() + " order=" + model.Result.Get("order").Count());
        } catch (Exception ex) {
            Log("xacc.model 失败: " + ex.Message);
        }
        try {
            var preview = await TuftoolsClient.CallAsync(new JsonObject { ["op"] = "rank.preview", ["id"] = "25" });
            Log("rank.preview ok=" + preview.Ok + " rows=" + preview.Result.Flatten("", 12).Count + " raw=" + preview.Raw.Substring(0, Math.Min(400, preview.Raw.Length)));
        } catch (ToolException ex) {
            Log("rank.preview 预期失败: [" + ex.Code + "] " + ex.Message);
        } catch (Exception ex) {
            Log("rank.preview 失败: " + ex.Message);
        }
        Log("== 完成 ==");
        var importSource = Environment.GetEnvironmentVariable("TUFTOOLS_SMOKE_IMPORT");
        if (!string.IsNullOrWhiteSpace(importSource)) {
            Log("== 导入校验 ==");
            try {
                var installed = await Services.TuftoolsInstaller.ImportAsync(importSource!, null);
                var version = await Services.TuftoolsInstaller.ProbeJsonAsync(installed, AppState.Settings.Proxy);
                Log("导入成功: " + installed + " 版本=" + version + " " + Services.TuftoolsInstaller.Describe(installed));
            } catch (Exception ex) {
                Log("导入失败: " + ex.Message);
            }
        }
        file.Dispose();
    }
    static async Task<string> Try(Func<Task<ToolResponse>> action, string name) {
        var resp = await action();
        return resp.Result.Get("name").Str() + " " + resp.Result.Get("version").Str() + " ops=" + resp.Result.Get("ops").Count();
    }
}
