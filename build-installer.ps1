<#
.SYNOPSIS
    Build and package WinSTerm.

.DESCRIPTION
    Publishes WinSTerm as a self-contained single-file executable, then
    creates an installer via Inno Setup. Falls back to a portable ZIP
    when Inno Setup is not installed.

.PARAMETER SkipBuild
    Skip the dotnet publish step (reuse existing publish output).

.PARAMETER CreateZip
    Force creation of a ZIP archive even when Inno Setup is available.
#>
param(
    [switch]$SkipBuild,
    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"

$repoRoot    = $PSScriptRoot
$publishDir  = Join-Path $repoRoot "publish"
$installerDir = Join-Path $repoRoot "installer"
$outputDir   = Join-Path $installerDir "output"
$projectFile = Join-Path $repoRoot "src\WinSTerm\WinSTerm.csproj"

# --- Clean previous artifacts ---
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $outputDir)  { Remove-Item $outputDir  -Recurse -Force }
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# --- Publish ---
if (-not $SkipBuild) {
    Write-Host "Publishing WinSTerm..." -ForegroundColor Cyan

    dotnet publish $projectFile `
        -c Release `
        -r win-x64 `
        --self-contained `
        -o $publishDir `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:PublishReadyToRun=true

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    Write-Host "Publish succeeded." -ForegroundColor Green
}

# --- Locate Inno Setup compiler ---
$isccPath = $null
$isccCmd  = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($isccCmd) {
    $isccPath = $isccCmd.Source
}
else {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $isccPath = $c; break }
    }
}

# --- Build installer or ZIP ---
if ($isccPath -and -not $CreateZip) {
    Write-Host "Building installer with Inno Setup..." -ForegroundColor Cyan
    & $isccPath (Join-Path $installerDir "winsterm-setup.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE" }
    Write-Host "Installer created in $outputDir" -ForegroundColor Green
}
else {
    if (-not $isccPath) {
        Write-Host "Inno Setup not found. Creating portable ZIP..." -ForegroundColor Yellow
    }
    else {
        Write-Host "Creating portable ZIP (CreateZip flag)..." -ForegroundColor Cyan
    }
    $zipPath = Join-Path $outputDir "WinSTerm-portable.zip"
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
    Write-Host "Created: $zipPath" -ForegroundColor Green
}

Write-Host "`nDone." -ForegroundColor Cyan
