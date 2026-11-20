param([switch]$WithAirPodsHelper)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }
$out = Join-Path $root "电环.exe"
$src = @(
    (Join-Path $root "源程序\HidCore.cs"),
    (Join-Path $root "源程序\AirPodsBridge.cs"),
    (Join-Path $root "源程序\AudioDevices.cs"),
    (Join-Path $root "源程序\HeadsetBattery.cs"),
    (Join-Path $root "源程序\Scanner.cs"),
    (Join-Path $root "源程序\GlassStyle.cs"),
    (Join-Path $root "源程序\MenuTheme.cs"),
    (Join-Path $root "源程序\Settings.cs"),
    (Join-Path $root "源程序\ColorPairForm.cs"),
    (Join-Path $root "源程序\ColorWheel.cs"),
    (Join-Path $root "源程序\TaskbarBadge.cs"),
    (Join-Path $root "源程序\Ui.cs")
)
$running = Get-Process -Name "电环" -ErrorAction SilentlyContinue
if ($running) { Write-Host "请先退出正在运行的程序再编译"; exit 1 }
& $csc /nologo /target:winexe "/out:$out" "/win32icon:$root\app.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll "/r:$root\lib\InTheHand.Net.Personal.dll" @src
if ($LASTEXITCODE -ne 0) { Write-Host "编译失败"; exit 1 }
Copy-Item (Join-Path $root "lib\InTheHand.Net.Personal.dll") $root -Force
Write-Host "编译完成：$out"

if ($WithAirPodsHelper) {
    & (Join-Path $root "build-helper.ps1")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}