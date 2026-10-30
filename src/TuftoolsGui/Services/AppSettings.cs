using System.Text.Json;
using System.Text.Json.Serialization;

namespace TuftoolsGui.Services;

public sealed class AppSettings {
    public string ExePath { get; set; } = "";
    public string Proxy { get; set; } = "";
    public bool NoCache { get; set; }
    public string DownloadDir { get; set; } = "";
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuftoolsGui");
    public static string FilePath => Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static AppSettings Load() {
        try {
            if (File.Exists(FilePath)) {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
                if (loaded is not null) return loaded;
            }
        } catch (Exception) {
        }
        return new AppSettings();
    }
    public void Save() {
        try {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        } catch (Exception) {
        }
    }
}

public static class AppState {
    public static AppSettings Settings { get; private set; } = new();
    public static Microsoft.UI.Xaml.Window? MainWindow { get; set; }
    public static event Action? SettingsChanged;
    public static void Initialize() {
        Settings = AppSettings.Load();
        if (string.IsNullOrWhiteSpace(Settings.ExePath) || !File.Exists(Settings.ExePath)) Settings.ExePath = DetectExe();
        if (string.IsNullOrWhiteSpace(Settings.DownloadDir)) Settings.DownloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
    public static void NotifySettingsChanged() {
        Settings.Save();
        SettingsChanged?.Invoke();
    }
    public static string DetectExe() {
        var candidates = new List<string> {
            Path.Combine(AppContext.BaseDirectory, "tuftools.exe"),
            Path.Combine(AppContext.BaseDirectory, "app", "tuftools.exe"),
            Path.Combine(AppSettings.Dir, "tools", "tuftools.exe"),
            @"D:\Code\MiniWorks\tuftools\build\tuftools.exe",
            @"D:\Code\MiniWorks\tuftools\tuftools.exe",
            @"D:\Code\MiniWorks\tuftools\build\Release\tuftools.exe",
            @"D:\Code\MiniWorks\tuftools\build\Debug\tuftools.exe"
        };
        foreach (var c in candidates) {
            try { if (File.Exists(c)) return c; } catch (Exception) { }
        }
        return "";
    }
}
