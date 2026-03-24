$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $root 'build'
$distDir = Join-Path $root 'dist'
$payloadZip = Join-Path $buildDir 'payload.zip'
$launcherProjectDir = Join-Path $buildDir 'single_launcher'
$launcherProject = Join-Path $launcherProjectDir 'single_launcher.csproj'
$launcherPayload = Join-Path $launcherProjectDir 'payload.zip'
$publishDir = Join-Path $launcherProjectDir 'publish'
$outExe = Join-Path $distDir 'SuperVPN_Single.exe'

if (!(Test-Path $distDir)) {
  New-Item -ItemType Directory -Path $distDir | Out-Null
}

# Build payload archive with runtime files required by Flutter app.
if (Test-Path $payloadZip) {
  Remove-Item $payloadZip -Force
}

Push-Location $root
try {
  tar -a -c -f $payloadZip super_vpn.exe flutter_windows.dll audioplayers_windows_plugin.dll screen_retriever_windows_plugin.dll window_manager_plugin.dll core data
} finally {
  Pop-Location
}

Copy-Item $payloadZip $launcherPayload -Force

if (Test-Path $publishDir) {
  Remove-Item $publishDir -Recurse -Force
}

dotnet publish $launcherProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:InvariantGlobalization=true -o $publishDir

$publishedExe = Join-Path $publishDir 'SuperVPN_Single.exe'
if (Test-Path $publishedExe) {
  Copy-Item $publishedExe $outExe -Force
}

# Cleanup transient build artifacts to keep workspace lightweight.
if (Test-Path $launcherPayload) {
  Remove-Item $launcherPayload -Force
}
if (Test-Path $payloadZip) {
  Remove-Item $payloadZip -Force
}
if (Test-Path $publishDir) {
  Remove-Item $publishDir -Recurse -Force
}
foreach ($dir in @('bin', 'obj')) {
  $full = Join-Path $launcherProjectDir $dir
  if (Test-Path $full) {
    Remove-Item $full -Recurse -Force
  }
}

if (Test-Path $outExe) {
  Write-Output "DONE: $outExe"
} else {
  throw "dotnet publish did not create output EXE: $outExe"
}
