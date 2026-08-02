[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$windowsRoot = Join-Path $repositoryRoot "windows"
$buildPropertiesPath = Join-Path $windowsRoot "Directory.Build.props"
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$buildProperties = Get-Content -Raw -Encoding UTF8 -LiteralPath $buildPropertiesPath
    $Version = $buildProperties.Project.PropertyGroup |
        ForEach-Object { [string]$_.VersionPrefix } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use major.minor.patch format, for example 0.1.0."
}

$assemblyVersion = "$Version.0"
$artifactsDirectory = Join-Path $windowsRoot "artifacts"
$publishDirectory = Join-Path $windowsRoot "artifacts\publish"
$installerAssets = Join-Path $windowsRoot "artifacts\installer"
$solution = Join-Path $windowsRoot "Sub2Bar.Windows.sln"
$project = Join-Path $windowsRoot "src\Sub2Bar.Windows\Sub2Bar.Windows.csproj"

New-Item -ItemType Directory -Force -Path $publishDirectory, $installerAssets | Out-Null

dotnet test $solution --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}

dotnet restore $project `
    --runtime win-x64 `
    --force
if ($LASTEXITCODE -ne 0) {
    throw "Restore for win-x64 failed with exit code $LASTEXITCODE."
}

dotnet clean $project `
    --configuration $Configuration `
    --runtime win-x64
if ($LASTEXITCODE -ne 0) {
    throw "Clean failed with exit code $LASTEXITCODE."
}

dotnet publish $project `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:Version=$Version `
    -p:VersionPrefix=$Version `
    -p:AssemblyVersion=$assemblyVersion `
    -p:FileVersion=$assemblyVersion `
    -p:InformationalVersion=$Version `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

$publishedExecutable = Join-Path $publishDirectory "Sub2Bar.exe"
$publishedVersion = (Get-Item -LiteralPath $publishedExecutable).VersionInfo.ProductVersion.Trim()
if ($publishedVersion -ne $Version) {
    throw "Published EXE version is $publishedVersion, expected $Version."
}

$bootstrapper = Join-Path $installerAssets "MicrosoftEdgeWebview2Setup.exe"
Invoke-WebRequest "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $bootstrapper

$innoCompiler = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($innoCompiler)) {
    $innoCompiler = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($innoCompiler)) {
    Write-Host "Publish output is ready at $publishDirectory"
    Write-Host "Install Inno Setup 6 and run: iscc.exe /DAppVersion=$Version windows\installer\Sub2Bar.iss"
    exit 0
}

Get-ChildItem -LiteralPath $artifactsDirectory -File -Filter "Sub2Bar-*-win-x64-setup.exe" |
    Remove-Item -Force

& $innoCompiler "/DAppVersion=$Version" (Join-Path $windowsRoot "installer\Sub2Bar.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $artifactsDirectory "Sub2Bar-$Version-win-x64-setup.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer was not created at $installer."
}

$installerVersion = (Get-Item -LiteralPath $installer).VersionInfo.ProductVersion.Trim()
if ($installerVersion -ne $Version) {
    throw "Installer version is $installerVersion, expected $Version."
}

Write-Host "Installer is ready at $installer"
