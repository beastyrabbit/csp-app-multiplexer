#requires -Version 7
<#
.SYNOPSIS
    Builds the two release downloads for CSP Mux, win-x64.

.DESCRIPTION
    Produces exactly what .forgejo/workflows/release.yml produces — the workflow calls this
    same script — so a release can be cut by hand without a runner.

        CSP-Mux-<version>-win-x64-needs-dotnet11.exe
            Framework-dependent, single file. Requires the latest .NET 11 Desktop Runtime.
        CSP-Mux-<version>-win-x64-standalone.exe
            Self-contained, single file, compressed. No prerequisites.
        THIRD-PARTY-NOTICES.md
        SHA256SUMS.txt          sha256sum format, covers the three files above
        release-body.md         the release description (not an asset)

    Trimming and NativeAOT are not options here: the app references WinForms (NotifyIcon,
    ZXing), and PublishTrimmed fails with NETSDK1175.

.EXAMPLE
    ./tools/publish-local.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    # Assembly/file version. Defaults to the tag with its leading "v" removed.
    [string] $Version,

    # Release tag. Defaults to the newest tag reachable from HEAD.
    [string] $Tag,

    # Where the finished files land. Cleared first.
    [string] $OutputDirectory,

    # Markdown placed above the download table. Defaults to docs/releases/<tag>.md.
    [string] $ReleaseNotesPath,

    # Used to build repository links in release-body.md.
    [string] $RepoUrl = 'https://git.heerlab.com/beasty/csp-app-multiplexer'
)

$ErrorActionPreference = 'Stop'

# UTF-8, no BOM, LF endings — so SHA256SUMS.txt verifies with `sha256sum -c` unchanged.
function Write-Text([string] $path, [string] $text) {
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
}

# Invariant culture, so a German-locale machine does not write "2,4 MB".
function Format-Size([string] $name) {
    [string]::Format([cultureinfo]::InvariantCulture, '{0:N1} MB',
        ((Get-Item (Join-Path $out $name)).Length / 1MB))
}

$repoRoot   = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project    = Join-Path $repoRoot 'src/CspMultiplexer.App/CspMultiplexer.App.csproj'
$notices    = Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md'
$builtExe   = 'CSP Mux.exe'
$stem       = 'CSP-Mux'

# ---- version ---------------------------------------------------------------------------
if (-not $Tag) { $Tag = (& git -C $repoRoot describe --tags --abbrev=0 2>$null) }
if (-not $Version -and $Tag) { $Version = $Tag -replace '^v', '' }
if (-not $Version) { $Version = '0.0.0-dev' }
if (-not $Tag) { $Tag = "v$Version" }

if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'dist/release' }
if (-not $ReleaseNotesPath) {
    $ReleaseNotesPath = Join-Path $repoRoot "docs/releases/$Tag.md"
}
$out     = [IO.Path]::GetFullPath($OutputDirectory)
$staging = Join-Path $out '.staging'

foreach ($d in @($out, $staging)) {
    if (Test-Path $d) { Remove-Item $d -Recurse -Force }
}
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Write-Host "version $Version   tag $Tag"
Write-Host "output  $out"

# ---- publish ---------------------------------------------------------------------------
# IncludeAllContentForSelfExtract is not optional. Without it a self-contained single-file
# publish leaves five native WPF DLLs (D3DCompiler_47_cor3, wpfgfx_cor3,
# PresentationNative_cor3, PenImc_cor3, vcruntime140_cor3 — 8.4 MB) loose beside the .exe,
# and both builds leave THIRD-PARTY-NOTICES.md loose. With it, each download is one file.
$common = @(
    '-c', 'Release'
    '-r', 'win-x64'
    '--nologo'
    '-p:PublishSingleFile=true'
    '-p:IncludeAllContentForSelfExtract=true'
    "-p:Version=$Version"
)

$builds = @(
    @{
        Name  = "$stem-$Version-win-x64-needs-dotnet11.exe"
        Dir   = Join-Path $staging 'framework-dependent'
        Extra = @('--self-contained', 'false')
    }
    @{
        Name  = "$stem-$Version-win-x64-standalone.exe"
        Dir   = Join-Path $staging 'self-contained'
        # EnableCompressionInSingleFile is what roughly halves this build.
        Extra = @('--self-contained', 'true', '-p:EnableCompressionInSingleFile=true')
    }
)

$assets = @()
foreach ($b in $builds) {
    Write-Host ''
    Write-Host "publishing $($b.Name)"
    & dotnet publish $project @common @($b.Extra) -o $b.Dir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($b.Name)" }

    $src = Join-Path $b.Dir $builtExe
    if (-not (Test-Path $src)) { throw "expected $src" }

    # Only the .exe ships. Anything else in the publish folder (other than symbols) would
    # be a runtime dependency left behind, so fail rather than ship a broken download.
    $stray = Get-ChildItem $b.Dir -Recurse -File |
             Where-Object { $_.Name -ne $builtExe -and $_.Extension -ne '.pdb' }
    if ($stray) {
        throw "publish left files beside the .exe: $($stray.Name -join ', ')"
    }

    Copy-Item $src (Join-Path $out $b.Name) -Force
    $assets += $b.Name
}

Copy-Item $notices (Join-Path $out 'THIRD-PARTY-NOTICES.md') -Force
$assets += 'THIRD-PARTY-NOTICES.md'

Remove-Item $staging -Recurse -Force

# ---- checksums -------------------------------------------------------------------------
# LF endings and no BOM, so `sha256sum -c SHA256SUMS.txt` works as-is.
$lines = foreach ($name in $assets) {
    $hash = (Get-FileHash (Join-Path $out $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
Write-Text (Join-Path $out 'SHA256SUMS.txt') (($lines -join "`n") + "`n")

# ---- release body ----------------------------------------------------------------------
$small = $assets[0]
$big   = $assets[1]
$avUrl = "$RepoUrl/wiki/Installation#verify-the-download-and-antivirus"
$releaseNotes = if (Test-Path $ReleaseNotesPath) {
    (Get-Content $ReleaseNotesPath -Raw).Trim()
}
else {
    "CSP Mux $Version for Windows."
}

$body = @"
$releaseNotes

## Downloads

Windows 10 or 11, 64-bit.

| Download | Size | You need |
| --- | --- | --- |
| ``$small`` | $(Format-Size $small) | the latest [.NET 11 Desktop Runtime preview](https://dotnet.microsoft.com/download/dotnet/11.0) |
| ``$big`` | $(Format-Size $big) | nothing |

Take the small one unless you would rather not install the runtime. Both are the same app.

``SHA256SUMS.txt`` holds the SHA-256 of each file. To check one:

``````
Get-FileHash "$big" -Algorithm SHA256
``````

Antivirus may flag either download. Both are unsigned single-file bundles that unpack
themselves at launch, and heuristic scanners score that as suspicious. The small build
trips it less often. [What to do about it]($avUrl)
"@

Write-Text (Join-Path $out 'release-body.md') ($body -replace "`r`n", "`n")

# ---- report ----------------------------------------------------------------------------
Write-Host ''
Get-ChildItem $out -File |
    Where-Object { $_.Name -ne 'release-body.md' } |
    Select-Object Name, @{ n = 'Size'; e = { Format-Size $_.Name } }, Length |
    Format-Table -AutoSize
