#requires -Version 7
[CmdletBinding()]
param(
    [string] $Channel = '11.0',
    [string] $InstallDirectory
)

$ErrorActionPreference = 'Stop'

if (-not $InstallDirectory) {
    $baseDirectory = if ($env:LOCALAPPDATA) {
        $env:LOCALAPPDATA
    }
    else {
        [IO.Path]::GetTempPath()
    }

    $InstallDirectory = Join-Path $baseDirectory "CSP Mux CI\dotnet-$Channel-preview"
}

$InstallDirectory = [IO.Path]::GetFullPath($InstallDirectory)
New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null

$installer = Join-Path ([IO.Path]::GetTempPath()) "dotnet-install-$PID.ps1"
try {
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installer
    & $installer `
        -Channel $Channel `
        -Quality preview `
        -InstallDir $InstallDirectory `
        -NoPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-install failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
}

$dotnet = Join-Path $InstallDirectory 'dotnet.exe'
if (-not (Test-Path $dotnet)) {
    throw "The .NET SDK was not installed at $dotnet."
}

$env:DOTNET_ROOT = $InstallDirectory
$env:PATH = "$InstallDirectory$([IO.Path]::PathSeparator)$env:PATH"

if ($env:GITHUB_PATH) {
    Add-Content -Path $env:GITHUB_PATH -Value $InstallDirectory -Encoding utf8
}

if ($env:GITHUB_ENV) {
    Add-Content -Path $env:GITHUB_ENV -Value "DOTNET_ROOT=$InstallDirectory" -Encoding utf8
}

$version = & $dotnet --version
if ($LASTEXITCODE -ne 0) {
    throw "The installed dotnet executable failed with exit code $LASTEXITCODE."
}

Write-Host "Using .NET SDK $version from $InstallDirectory"
