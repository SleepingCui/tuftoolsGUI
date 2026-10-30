# TUF Tools GUI

tuftools 的 WinUI 3 图形外壳。所有数据都来自 `tuftools --json` 的 JSON 协议：GUI 用标准输入发送一条请求，读取标准输出里的响应，再展平到界面表格里。

## 目录结构

```
TUFTOOLS-GUI
  app\                      可直接运行的成品（自包含，含 tuftools.exe）
  src\TuftoolsGui\          C# + XAML 源码（WinUI 3 / .NET 8）
    App.xaml(.cs)           应用入口、全局样式、异常日志
    MainWindow.xaml(.cs)    导航外壳
    Pages\                  7 个功能页
    Services\               tuftools 进程调用、JSON 解析、设置、UI 辅助
    Models\                 表格行模型
    Smoke.cs                冒烟自检（可选，见下）
  docs\                     界面截图
  build.ps1                 重新编译并刷新 app\
  README.md
```

## 运行

双击 `app\TuftoolsGui.exe`。程序目录已经自带 `tuftools.exe`，首次启动会自动识别；也可以导入或下载自己的版本。

### 获取 tuftools.exe

概览页的「运行设置」下有三个入口：

- 导入 tuftools.exe：选本机的 tuftools.exe，复制到程序目录并切换过去。
- 从 GitHub 下载最新版：读取 `https://api.github.com/repos/SleepingCui/tuftools/releases/latest`，取 release 里的 exe，校验体积与 SHA-256 后安装。
- 打开 release 页面：用浏览器打开 releases 页面手动下载。
- 另外还有「浏览」直接指向任意位置（不复制文件）与「自动检测」（程序目录、程序目录\app、`%LOCALAPPDATA%\TuftoolsGui\tools`、本仓库 build 目录）。

下载与导入都会先用 `-j` 发一条 `version` 请求做能力校验，通过的才会替换现有文件；不通过则保留原文件并在提示条里说明原因。走代理下载时请在代理栏填好地址，GitHub API 与下载请求都会使用该代理。

> 注意：当前 release（20261023-812bec9 及更早）编译自尚未包含 JSON 协议的提交，`tuftools --help` 里没有 `-j` 选项，因此下载后会校验失败并提示「不支持 JSON 模式」。这种情况下请用「导入 tuftools.exe」指定支持 JSON 版本的二进制（例如本仓库源码编译出的 `build\tuftools.exe`），release 更新到含 JSON 协议的提交后「从 GitHub 下载最新版」即可直接使用。

网络要点：tuftools 访问 api.tuforums.com 时如果直连超时（报 WinHTTP error 12002），在「概览」页的代理栏填本机 HTTP 代理（例如 `http://127.0.0.1:1080`），点「保存设置」后重试。设置存放在 `%LOCALAPPDATA%\TuftoolsGui\settings.json`。

## 页面

- 概览：tuftools 路径、代理、下载目录、缓存开关；「测试连接」调用 `version` 并展示版本、构建时间与 17 个 op。
- 玩家查询：`player.search` 搜索，单击结果行查看 `player.details` 的 7 项分数（排位分/总分/无瑕分/首通分/首杀分/12K 分/全局分，含全球与国家名次）与通关记录，可分页「更多记录」（`player.passes`）。
- 谱面查询：`level.search` 检索，「查看详情」走 `level.get`，「CDN 与下载信息」走 `level.cdn`（含内置/自定义预设），下载用 `level.download`（原版/转换/v2）。
- PP 计算：`pp.calc`，按谱面 ID 自动取物量与基础分，也可手填；可附带 `rank.preview` 排位分变化。
- 排位预览：`rank.preview`，把候选成绩换成前 20 条记录之一，对比全球与国家名次变化（负数表示前进）。
- XACC 工具：`xacc.calc` 正算、`xacc.reverse`/`xacc.equivalents` 反推与等效组合、`xacc.model` 查看权重与难度系数、`xacc.config` 应用配置。
- JSON 控制台：直接发送任意请求，查看原始响应、测参数、清空缓存。

## 重新编译

需要 .NET SDK 8 或更高（已装 Windows App SDK 1.8 的 NuGet 包，不需要本机 Windows SDK 或 Windows App Runtime）：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

脚本执行 `dotnet build -c Release`，然后把 `bin\Release\net8.0-windows10.0.19041.0\win-x64` 的内容复制到 `app\`，并把 `D:\Code\MiniWorks\tuftools\build\tuftools.exe` 一并复制过去（路径可用参数覆盖）。

注意：不要用 `dotnet publish -o` 输出成品，该方式会漏掉 `TuftoolsGui.pri` 与 `.xbf`，启动即报 XamlParseException；`build.ps1` 直接从构建输出目录复制。

## 冒烟自检

设置环境变量后启动，程序会依次加载 7 个页面、调用 version/player/level/pp/xacc/rank 共 11 个 op，把结果写进日志并退出：

```powershell
$env:TUFTOOLS_SMOKE_LOG = "$env:TEMP\tuftools-smoke.log"
.\app\TuftoolsGui.exe
```

崩溃堆栈会追加到 `%LOCALAPPDATA%\TuftoolsGui\error.log`。

## 已知限制

- `rank.preview` 对部分玩家返回 `unavailable`：tuftools 在重算加权排位分时发现与玩家当前 `rankedScore` 不一致，属于数据侧限制，界面会原样显示 `[error.code] error.message`。
- `level.search` 必须带关键词，空关键词返回 0 条（服务端行为）。
- 转换下载依赖服务端可用的预设；`level.cdn` 的 `transform_options` 为空时不提供转换选项。
