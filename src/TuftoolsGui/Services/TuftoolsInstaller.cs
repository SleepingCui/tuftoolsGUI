using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace TuftoolsGui.Services;

public sealed record TuftoolsRelease(string Tag, string Name, string Url, long Size, string Sha256);

public static class TuftoolsInstaller {
    const string LatestApi = "https://api.github.com/repos/SleepingCui/tuftools/releases/latest";

    public static string ToolsDir => Path.Combine(AppSettings.Dir, "tools");

    static string TargetPath() {
        var sideBySide = Path.Combine(AppContext.BaseDirectory, "tuftools.exe");
        try {
            var probe = sideBySide + ".probe";
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return sideBySide;
        } catch (Exception) {
            return Path.Combine(ToolsDir, "tuftools.exe");
        }
    }

    public static HttpClient CreateClient(string proxy) {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        var trimmed = (proxy ?? "").Trim();
        if (trimmed.Length > 0 && Uri.TryCreate(trimmed, UriKind.Absolute, out var proxyUri)) {
            handler.Proxy = new WebProxy(proxyUri) { BypassProxyOnLocal = true };
            handler.UseProxy = true;
        }
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TuftoolsGui", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<TuftoolsRelease> FetchLatestAsync(string proxy, CancellationToken ct) {
        using var client = CreateClient(proxy);
        var json = await client.GetStringAsync(LatestApi, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException("这次 release 没有附带资源文件。");
        }
        JsonElement? chosen = null;
        foreach (var asset in assets.EnumerateArray()) {
            var fileName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            chosen = asset;
            if (fileName.Equals("tuftools.exe", StringComparison.OrdinalIgnoreCase)) break;
        }
        if (chosen is null) throw new InvalidOperationException("这次 release 里没有 tuftools.exe。");
        var picked = chosen.Value;
        var url = picked.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
        var size = picked.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
        var digest = picked.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
        var sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..] : "";
        if (url.Length == 0) throw new InvalidOperationException("资源缺少下载地址。");
        return new TuftoolsRelease(tag, name, url, size, sha);
    }

    public static async Task<string> DownloadAsync(TuftoolsRelease release, string proxy, IProgress<string>? progress, CancellationToken ct) {
        var target = TargetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".download";
        if (File.Exists(temp)) File.Delete(temp);
        using (var client = CreateClient(proxy)) {
            using var response = await client.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? release.Size;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var sink = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long written = 0;
            var lastReport = 0L;
            while (true) {
                var read = await source.ReadAsync(buffer, ct);
                if (read <= 0) break;
                await sink.WriteAsync(buffer.AsMemory(0, read), ct);
                written += read;
                if (progress is not null && total > 0 && written - lastReport >= 262144) {
                    lastReport = written;
                    progress.Report("已下载 " + (written / 1048576.0).ToString("0.0") + " / " + (total / 1048576.0).ToString("0.0") + " MB");
                }
            }
        }
        if (release.Size > 0 && new FileInfo(temp).Length != release.Size) {
            File.Delete(temp);
            throw new InvalidOperationException("下载体积与 release 记录不一致，已丢弃。");
        }
        if (release.Sha256.Length > 0) {
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temp))).ToLowerInvariant();
            if (!actual.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) {
                File.Delete(temp);
                throw new InvalidOperationException("SHA-256 校验失败，已丢弃下载文件。");
            }
        }
        progress?.Report("下载完成，正在校验 JSON 模式。");
        try {
            await ProbeJsonAsync(temp, proxy);
        } catch (Exception ex) {
            try { File.Delete(temp); } catch (Exception) { }
            throw new InvalidOperationException("这个 release（" + release.Tag + "）不可用：" + ex.Message, ex);
        }
        return Finish(temp, target);
    }

    public static async Task<string> ImportAsync(string sourcePath, IProgress<string>? progress) {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("没有这个文件: " + sourcePath, sourcePath);
        var target = TargetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) {
            progress?.Report("选择的已经是当前使用的文件。");
            await ProbeJsonAsync(target, "");
            return target;
        }
        var temp = target + ".import";
        progress?.Report("正在复制文件。");
        await Task.Run(() => {
            if (File.Exists(temp)) File.Delete(temp);
            File.Copy(sourcePath, temp, true);
        });
        progress?.Report("正在校验 JSON 模式。");
        try {
            await ProbeJsonAsync(temp, "");
        } catch (Exception ex) {
            try { File.Delete(temp); } catch (Exception) { }
            throw new InvalidOperationException("这个文件不能用：" + ex.Message, ex);
        }
        return Finish(temp, target);
    }

    public static async Task<string> ProbeJsonAsync(string exePath, string proxy) {
        var info = new ProcessStartInfo {
            FileName = exePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        info.ArgumentList.Add("-j");
        var trimmed = (proxy ?? "").Trim();
        if (trimmed.Length > 0) {
            info.ArgumentList.Add("--proxy");
            info.ArgumentList.Add(trimmed);
        }
        using var process = new Process { StartInfo = info };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        if (!process.Start()) throw new InvalidOperationException("无法启动这个程序。");
        process.StandardInput.Write("{\"op\":\"version\"}");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try {
            await process.WaitForExitAsync(timeout.Token);
        } catch (OperationCanceledException) {
            try { process.Kill(true); } catch (Exception) { }
            throw new InvalidOperationException("校验超时，这不是可用的 tuftools。");
        }
        var text = (await stdout).Trim();
        if (text.Length == 0) {
            var errorText = (await stderr).Trim();
            var firstLine = errorText.Length > 0 ? errorText.Split('\n')[0].Trim() : "没有任何输出";
            throw new InvalidOperationException("不支持 JSON 模式（-j）：" + firstLine + "。请改用「导入 tuftools.exe」指定支持 JSON 模式的版本。");
        }
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) {
            throw new InvalidOperationException("version 调用失败：" + text);
        }
        var version = root.TryGetProperty("result", out var result) && result.TryGetProperty("version", out var versionElement) ? versionElement.GetString() ?? "" : "";
        return version;
    }

    static string Finish(string temp, string target) {
        if (File.Exists(target)) {
            try { File.Delete(target); }
            catch (Exception) {
                var backup = target + ".old";
                try { if (File.Exists(backup)) File.Delete(backup); File.Move(target, backup); } catch (Exception) { }
            }
        }
        File.Move(temp, target);
        return target;
    }

    public static string Describe(string exePath) {
        try {
            if (!File.Exists(exePath)) return "文件不存在";
            var info = new FileInfo(exePath);
            var version = FileVersionInfo.GetVersionInfo(exePath);
            var product = version.ProductVersion ?? "";
            return (info.Length / 1048576.0).ToString("0.0") + " MB，修改时间 " + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + (product.Length > 0 ? "，文件版本 " + product : "");
        } catch (Exception ex) {
            return ex.Message;
        }
    }
}
