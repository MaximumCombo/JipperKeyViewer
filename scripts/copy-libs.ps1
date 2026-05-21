# Copy Unity reference DLLs from Steam game directory to local libs/ folder
# 从 Steam 游戏目录复制 Unity 引用 DLL 到本地 libs/ 文件夹

param(
    [string]$GamePath = "D:\Program Files\Steam\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$LibsDir = Join-Path (Split-Path -Parent $ScriptDir) "libs"

if (-not (Test-Path $GamePath)) {
    Write-Host "Game Managed directory not found: $GamePath"
    Write-Host "Usage: .\copy-libs.ps1 [-GamePath <path-to-Managed>]"
    exit 1
}

$dlls = @(
    "Unity.TextMeshPro.dll",
    "UnityEngine.dll",
    "UnityEngine.AssetBundleModule.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.JSONSerializeModule.dll",
    "UnityEngine.TextCoreFontEngineModule.dll",
    "UnityEngine.TextCoreTextEngineModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.UIModule.dll",
    "UnityModManager\UnityModManager.dll"
)

New-Item -ItemType Directory -Force -Path $LibsDir | Out-Null

foreach ($dll in $dlls) {
    $src = Join-Path $GamePath $dll
    $name = Split-Path -Leaf $dll
    $dst = Join-Path $LibsDir $name
    if (Test-Path $src) {
        Copy-Item $src $dst -Force
        Write-Host "OK  $name"
    } else {
        Write-Host "MISSING  $src"
    }
}

Write-Host ""
Write-Host "Done. DLLs copied to $LibsDir"
