using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TuftoolsGui.Services;

public sealed class ToolException : Exception {
    public string Code { get; }
    public ToolException(string code, string message) : base(message) { Code = code; }
}

public sealed class ToolResponse {
    public bool Ok { get; init; }
    public string Op { get; init; } = "";
    public long ElapsedMs { get; init; }
    public string ErrorCode { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
    public string Raw { get; init; } = "";
    public JsonElement Result { get; init; }
    public bool HasResult { get; init; }
}

public static class TuftoolsClient {
    public static async Task<ToolResponse> SendAsync(JsonObject request, CancellationToken ct = default, int timeoutSeconds = 120) {
        return await SendRawAsync(request.ToJsonString(), ct, timeoutSeconds);
    }
    public static async Task<ToolResponse> SendRawAsync(string payload, CancellationToken ct = default, int timeoutSeconds = 120) {
        var stdout = await RunAsync(payload, ct, timeoutSeconds);
        var text = stdout.Trim();
        using var doc = ParseResponse(text);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ToolException("bad_output", "响应不是 JSON 对象: " + Excerpt(text));
        JsonElement result = default;
        var hasResult = false;
        if (root.Get("result") is JsonElement r && r.ValueKind != JsonValueKind.Null && r.ValueKind != JsonValueKind.Undefined) {
            result = r.Clone();
            hasResult = true;
        }
        return new ToolResponse {
            Ok = root.Get("ok") is JsonElement okEl && okEl.ValueKind == JsonValueKind.True,
            Op = root.Get("op").Str(),
            ElapsedMs = (long)(root.Get("elapsed_ms").Num() ?? 0),
            ErrorCode = root.Get("error", "code").Str(),
            ErrorMessage = root.Get("error", "message").Str(),
            Raw = text,
            Result = result,
            HasResult = hasResult
        };
    }
    public static async Task<ToolResponse> CallAsync(JsonObject request, CancellationToken ct = default, int timeoutSeconds = 120) {
        var resp = await SendAsync(request, ct, timeoutSeconds);
        if (!resp.Ok) throw new ToolException(string.IsNullOrEmpty(resp.ErrorCode) ? "error" : resp.ErrorCode, string.IsNullOrEmpty(resp.ErrorMessage) ? "tuftools 返回失败，且没有错误信息。" : resp.ErrorMessage);
        return resp;
    }
    static async Task<string> RunAsync(string payload, CancellationToken ct, int timeoutSeconds) {
        var settings = AppState.Settings;
        var exe = settings.ExePath;
        if (string.IsNullOrWhiteSpace(exe)) throw new ToolException("no_exe", "尚未设置 tuftools.exe 路径，请到概览页设置。");
        if (!File.Exists(exe)) throw new ToolException("no_exe", "tuftools.exe 不存在: " + exe);
        var psi = new ProcessStartInfo {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
        };
        psi.ArgumentList.Add("-j");
        if (!string.IsNullOrWhiteSpace(settings.Proxy)) {
            psi.ArgumentList.Add("--proxy");
            psi.ArgumentList.Add(settings.Proxy.Trim());
        }
        if (settings.NoCache) psi.ArgumentList.Add("--no-cache");
        using var proc = new Process { StartInfo = psi };
        try {
            if (!proc.Start()) throw new ToolException("start_failed", "无法启动 tuftools.exe。");
        } catch (Exception ex) when (ex is not ToolException) {
            throw new ToolException("start_failed", "启动 tuftools.exe 失败: " + ex.Message);
        }
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        try {
            var bytes = new UTF8Encoding(false).GetBytes(payload);
            await proc.StandardInput.BaseStream.WriteAsync(bytes, ct);
            await proc.StandardInput.BaseStream.FlushAsync(ct);
            proc.StandardInput.Close();
        } catch (IOException) {
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try {
            await proc.WaitForExitAsync(timeout.Token);
        } catch (OperationCanceledException) {
            try { proc.Kill(true); } catch { }
            if (ct.IsCancellationRequested) throw;
            throw new ToolException("timeout", "调用超过 " + timeoutSeconds + " 秒仍未结束，已终止进程。");
        }
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (stdout.Trim().Length == 0) throw new ToolException("no_output", "tuftools 没有返回内容。" + (string.IsNullOrWhiteSpace(stderr) ? "" : "\nstderr: " + Excerpt(stderr)));
        return stdout;
    }
    static JsonDocument ParseResponse(string text) {
        try { return JsonDocument.Parse(text); } catch (JsonException) { }
        var lines = text.Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--) {
            var line = lines[i].Trim();
            if (line.Length == 0 || line[0] != '{') continue;
            try { return JsonDocument.Parse(line); } catch (JsonException) { }
        }
        throw new ToolException("bad_output", "无法解析 tuftools 的响应: " + Excerpt(text));
    }
    static string Excerpt(string s) {
        s = s.Trim();
        return s.Length <= 500 ? s : s.Substring(0, 500) + "...";
    }
}
