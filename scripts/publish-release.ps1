# Builds a ready-to-run Windows zip for GitHub Releases.
# Usage:
#   .\scripts\publish-release.ps1
#   .\scripts\publish-release.ps1 -Version 1.0.0

[CmdletBinding()]
param(
    [string]$Version = "",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "DnDMapHelper\DnDMapHelper.csproj"

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$csproj = Get-Content $project
    $Version = $csproj.Project.PropertyGroup.Version |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "Specify -Version or set <Version> in the .csproj."
    }
}

[xml]$csprojXml = Get-Content $project
$informationalVersion = $csprojXml.Project.PropertyGroup.InformationalVersion |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($informationalVersion)) {
    $informationalVersion = $Version
}

$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$stageDir = Join-Path $repoRoot "artifacts\stage\DnDMapHelper"
$zipPath = Join-Path $repoRoot "artifacts\DnDMapHelper-v$Version-$Runtime.zip"

Write-Host "Publishing DnDMapHelper $Version (display: $informationalVersion, $Runtime, self-contained)..."

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path $stageDir -Parent) -Force | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -p:InformationalVersion=$informationalVersion

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Copy-Item $publishDir $stageDir -Recurse -Force

$readmePath = Join-Path $stageDir "README.txt"
@"
DnDMapHelper v$informationalVersion ($Version)
======================

Как запустить
-------------
1. Распакуйте архив в любую папку.
2. Запустите DnDMapHelper.exe.
3. Установка .NET не требуется (сборка self-contained).

Системные требования
--------------------
- Windows 10 / 11 (x64)
- Второй монитор или проектор — по желанию, для экрана игроков

Сохранения
----------
Проекты хранятся в файлах .dndmap (Открыть / Сохранить в программе).

Поддержка
---------
dndtools.lebedev@proton.me
"@ | Set-Content -Path $readmePath -Encoding UTF8

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $stageDir,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true)

Write-Host ""
Write-Host "Ready:"
Write-Host "  $zipPath"
Write-Host ""
Write-Host "Next: upload this zip to a GitHub Release (tag v$Version)."
