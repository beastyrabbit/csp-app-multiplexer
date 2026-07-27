#requires -Version 7
[CmdletBinding()]
param(
    [ValidateSet('Check','Push','Pull')] [string] $Mode = 'Check',
    [string] $Other
)

$ErrorActionPreference = 'Stop'
$here = Resolve-Path (Join-Path $PSScriptRoot '..')
$names = @('csp_color_palette_gen','csp-app-multiplexer')
if (-not $Other) {
    $parent = Split-Path $here -Parent
    $mine   = Split-Path $here -Leaf
    $Other  = $names | Where-Object { $_ -ne $mine } |
              ForEach-Object { Join-Path $parent $_ } |
              Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Other) { Write-Host 'suite-sync: nothing to reconcile.'; exit 0 }

function Get-SyncHash([string] $path) {
    $text = [IO.File]::ReadAllText($path)
    $text = [Regex]::Replace($text,
        '(?s)(//|<!--)\s*──\s*SYNC-LOCAL BEGIN\s*──.*?SYNC-LOCAL END\s*──\s*(-->)?', '')
    $text = $text -replace "`r`n", "`n"
    $sha  = [Security.Cryptography.SHA256]::Create()
    ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)) |
        ForEach-Object { $_.ToString('x2') }) -join ''
}

# suite-sync.manifest: one "<tier>|<relative-path-in-this-repo>|<relative-path-in-the-other>"
$rows  = Get-Content (Join-Path $PSScriptRoot 'suite-sync.manifest') |
         Where-Object { $_ -and -not $_.StartsWith('#') }
$drift = @()

foreach ($row in $rows) {
    $tier, $mineRel, $theirsRel = $row -split '\|'
    $a = Join-Path $here  $mineRel
    $b = Join-Path $Other $theirsRel
    if (-not (Test-Path $a)) { Write-Error "missing locally: $mineRel"; exit 2 }
    if (-not (Test-Path $b)) { $drift += ,@($a,$b,'absent on the other side'); continue }
    if ((Get-SyncHash $a) -ne (Get-SyncHash $b)) {
        $newer = if ((Get-Item $a).LastWriteTimeUtc -ge (Get-Item $b).LastWriteTimeUtc)
                 { 'this repo' } else { 'the other repo' }
        $drift += ,@($a,$b,"differs; newer side: $newer")
    }
}

if (-not $drift) { Write-Host "suite-sync: $($rows.Count) file(s) in sync."; exit 0 }

if ($Mode -eq 'Check') {
    foreach ($d in $drift) { Write-Warning "suite-sync drift: $($d[0]) <-> $($d[1]) — $($d[2])" }
    exit 1
}

foreach ($d in $drift) {
    $src, $dst = if ($Mode -eq 'Push') { $d[0], $d[1] } else { $d[1], $d[0] }
    # Preserve the destination's SYNC-LOCAL block; replace everything else.
    $pattern = '(?s)((?://|<!--)\s*──\s*SYNC-LOCAL BEGIN\s*──.*?SYNC-LOCAL END\s*──\s*(?:-->)?)'
    $keep = if (Test-Path $dst) { ([Regex]::Match([IO.File]::ReadAllText($dst), $pattern)).Value } else { '' }
    $text = [IO.File]::ReadAllText($src)
    if ($keep) { $text = [Regex]::Replace($text, $pattern, { $keep }, 1) }
    [IO.File]::WriteAllText($dst, $text, (New-Object Text.UTF8Encoding $true))   # BOM, per §6
    Write-Host "suite-sync: $Mode $($d[0]) -> $dst"
}
exit 0
