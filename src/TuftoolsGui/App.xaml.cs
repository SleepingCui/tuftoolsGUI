using Microsoft.UI.Xaml;
using TuftoolsGui.Services;

namespace TuftoolsGui;

public partial class App : Application {
    public App() {
        InitializeComponent();
        UnhandledException += (_, e) => {
            Log(e.Exception);
        };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args) {
        AppState.Initialize();
        var window = new MainWindow();
        AppState.MainWindow = window;
        window.Activate();
        var smokePath = Environment.GetEnvironmentVariable("TUFTOOLS_SMOKE_LOG");
        if (!string.IsNullOrEmpty(smokePath)) _ = RunSmokeAsync(window, smokePath);
    }
    static async Task RunSmokeAsync(MainWindow window, string logPath) {
        try {
            await Task.Delay(1200);
            await Smoke.RunAsync(window, logPath);
        } catch (Exception ex) {
            Log(ex);
            try {
                File.AppendAllText(logPath, "冒烟取消: " + ex + Environment.NewLine);
            } catch (Exception) {
            }
        } finally {
            try {
                File.AppendAllText(logPath, "冒烟结束，进程退出。" + Environment.NewLine);
            } catch (Exception) {
            }
            Current.Exit();
        }
    }
    static void Log(Exception? ex) {
        try {
            Directory.CreateDirectory(AppSettings.Dir);
            File.AppendAllText(Path.Combine(AppSettings.Dir, "error.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine);
        } catch (Exception) {
        }
    }
}
