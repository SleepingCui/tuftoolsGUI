param(
    [string]$Configuration = "Release",
    [string]$TuftoolsExe = "D:\Code\MiniWorks\tuftools\build\tuftools.exe"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\TuftoolsGui\TuftoolsGui.csproj"
$output = Join-Path $root "src\TuftoolsGui\bin\$Configuration\net8.0-windows10.0.19041.0\win-x64"
$target = Join-Path $root "app"

Get-Process TuftoolsGui -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
dotnet build $project -c $Configuration | Select-String -Pattern "error|已成功"
if ($LASTEXITCODE -ne 0) { throw "构建失败" }

$removed = $false
if (Test-Path $target) {
    for ($i = 0; $i -lt 3 -and -not $removed; $i++) {
        try { Remove-Item $target -Recurse -Force -ErrorAction Stop; $removed = $true }
        catch { Start-Sleep -Seconds 1 }
    }
    if (-not $removed) { "旧目录被占用，改为覆盖复制" }
}
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item (Join-Path $output "*") $target -Recurse -Force
if (Test-Path $TuftoolsExe) {
    Copy-Item $TuftoolsExe $target -Force
    "已复制 tuftools.exe: $TuftoolsExe"
} else {
    "未找到 tuftools.exe（$TuftoolsExe），请在概览页手动指定路径"
}
$files = (Get-ChildItem $target -Recurse -File)
"已刷新 $target，共 $($files.Count) 个文件，$([math]::Round((($files | Measure-Object Length -Sum).Sum) / 1MB, 1)) MB"
