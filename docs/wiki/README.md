# Wiki source

These files are the source of truth for the CSP Mux wiki at
<https://git.heerlab.com/beasty/csp-app-multiplexer/wiki>.

Forgejo serves a wiki from a separate git repository. Edit the pages here, commit
them with the code, then publish them to the wiki repo. Do not edit pages in the
Forgejo wiki UI — the next publish overwrites them.

## Page map

| File | Wiki page title |
| --- | --- |
| `Home.md` | Home |
| `Installation.md` | Installation |
| `How-It-Works.md` | How It Works |
| `Connection-Scope.md` | Connection Scope |
| `Palette-Companion-Integration.md` | Palette Companion Integration |

Forgejo maps a hyphen in a wiki page name to a space in the title. Keep the flat
`Word-Word.md` naming so the mapping stays predictable.

## Images

Image links are written as `docs/assets/<name>.png`. Publishing copies
`docs/assets/` from this repository into the wiki repository at the same
relative path, so the links resolve without rewriting them.

The canonical copies of the shared screenshots live in the CSP Palette Companion
repository's `docs/assets/`. Keep this repository's copies in sync with those.

## Publishing

```powershell
git clone https://git.heerlab.com/beasty/csp-app-multiplexer.wiki.git
Copy-Item docs/wiki/*.md    <wiki-clone>/            -Force
Copy-Item docs/assets/*.png <wiki-clone>/docs/assets/ -Force
```

`README.md` in this folder is not published.
