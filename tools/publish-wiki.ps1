<#
.SYNOPSIS
  Publishes docs/wiki/*.md to this repository's Forgejo wiki.

.DESCRIPTION
  Forgejo serves a wiki from a sibling repository, <repo>.wiki.git, whose pages are FLAT
  markdown files at the root. Two consequences this script handles:

    * Page name  - the wiki page title is the file name. Home.md becomes the landing page.
    * Image path - docs/wiki/Page.md refers to images as ../assets/x.png, which resolves
                   inside the main repo but not from the wiki root. Each reference is
                   rewritten to an absolute raw URL into the main repo, so the images stay
                   versioned in one place instead of being duplicated into the wiki.

  The wiki repository is not created by pushing to it. Enable the wiki in
  Settings > Wiki and save one page in the web UI first, otherwise the remote does not
  exist and the clone below fails.

.EXAMPLE
  pwsh tools/publish-wiki.ps1
  pwsh tools/publish-wiki.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$wikiSource = Join-Path $repoRoot 'docs/wiki'
if (-not (Test-Path $wikiSource)) { throw "No docs/wiki directory at $wikiSource." }

$origin = (git -C $repoRoot remote get-url origin).Trim()
if (-not $origin) { throw 'No origin remote.' }

# https://host/owner/repo.git -> https://host/owner/repo.wiki.git and .../owner/repo
$base = $origin -replace '\.git$', ''
$wikiRemote = "$base.wiki.git"
$rawBase = "$base/raw/branch/$Branch/docs/assets"

Write-Host "repo   : $base"
Write-Host "wiki   : $wikiRemote"
Write-Host "images : $rawBase"

$work = Join-Path ([IO.Path]::GetTempPath()) ("wiki-" + [Guid]::NewGuid().ToString('N'))
try {
    git clone --quiet $wikiRemote $work 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not clone $wikiRemote. Enable the wiki in Settings > Wiki and save one page first."
    }

    # Replace the page set wholesale rather than merging: a page deleted from docs/wiki
    # must disappear from the wiki too, and .git must survive.
    Get-ChildItem $work -File -Filter *.md | Remove-Item -Force

    $published = 0
    foreach ($page in Get-ChildItem $wikiSource -File -Filter *.md) {
        $text = [IO.File]::ReadAllText($page.FullName)
        $text = $text -replace '\]\(\.\./assets/', "]($rawBase/"
        $text = $text -replace '\]\(docs/assets/', "]($rawBase/"
        [IO.File]::WriteAllText((Join-Path $work $page.Name), $text)
        $published++
        Write-Host "  + $($page.Name)"
    }

    git -C $work add -A
    if ((git -C $work status --porcelain).Length -eq 0) {
        Write-Host 'Wiki already up to date.'
        return
    }

    if ($PSCmdlet.ShouldProcess($wikiRemote, "push $published page(s)")) {
        git -C $work -c user.name='publish-wiki' -c user.email='publish-wiki@localhost' `
            commit --quiet -m "Publish wiki from docs/wiki ($(git -C $repoRoot rev-parse --short HEAD))"
        git -C $work push --quiet origin HEAD
        Write-Host "Published $published page(s)."
    }
}
finally {
    if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
}
