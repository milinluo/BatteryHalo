param(
    [string]$Winmd = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
$source = Join-Path $root "源程序\AirPodsHelper.cs"
$out = Join-Path $root "AirPodsHelper.exe"

if (-not (Test-Path -LiteralPath $source)) {
    Write-Host "找不到源文件：$source"
    exit 1
}

$winmdRefs = @()
if (-not [string]::IsNullOrWhiteSpace($Winmd)) {
    if (-not (Test-Path -LiteralPath $Winmd)) {
        Write-Host "指定的 Windows.winmd 不存在：$Winmd"
        exit 2
    }
    $winmdRefs += "/r:$Winmd"
}
else {
    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\UnionMetadata"),
        (Join-Path $env:ProgramFiles "Windows Kits\10\UnionMetadata"),
        (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\References")
    )
    foreach ($r in $roots) {
        if ([string]::IsNullOrWhiteSpace($r) -or -not (Test-Path -LiteralPath $r)) { continue }
        $hit = Get-ChildItem -LiteralPath $r -Filter "Windows.winmd" -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($hit) {
            $winmdRefs += "/r:" + $hit.FullName
            break
        }
    }

    if ($winmdRefs.Count -eq 0) {
        # Windows 自带 WinRT 元数据，无需安装 Windows SDK。
        $meta = Join-Path $env:WINDIR "System32\WinMetadata"
        if (Test-Path -LiteralPath $meta) {
            foreach ($name in @("Windows.Foundation.winmd", "Windows.Devices.winmd", "Windows.Storage.winmd")) {
                $f = Join-Path $meta $name
                if (Test-Path -LiteralPath $f) { $winmdRefs += "/r:$f" }
            }
        }
    }
}

if ($winmdRefs.Count -eq 0) {
    Write-Host "未找到 Windows.winmd，也未找到 System32\\WinMetadata。"
    Write-Host "请安装 Windows 10/11 SDK，或手动传入："
    Write-Host "  .\build-helper.ps1 -Winmd 'C:\...\Windows.winmd'"
    exit 2
}

$fw = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path -LiteralPath (Join-Path $fw "csc.exe"))) {
    $fw = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319"
}
$runtime = Join-Path $fw "System.Runtime.dll"
$winrt = Join-Path $fw "System.Runtime.WindowsRuntime.dll"
$interop = Join-Path $fw "System.Runtime.InteropServices.WindowsRuntime.dll"
foreach ($r in @($runtime, $winrt, $interop)) {
    if (-not (Test-Path -LiteralPath $r)) {
        Write-Host "缺少框架程序集：$r"
        exit 2
    }
}

$running = Get-Process -Name "电环" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "请先退出正在运行的主程序再编译 AirPodsHelper.exe"
    exit 1
}

$refs = @(
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:$runtime",
    "/r:$winrt",
    "/r:$interop"
) + $winmdRefs

& $csc /nologo /target:exe "/out:$out" @refs $source

if ($LASTEXITCODE -ne 0) {
    Write-Host "AirPodsHelper 编译失败"
    exit 1
}
Write-Host "编译完成：$out"
