# CSP Mux

Clip Studio Paint offers exactly one Companion Mode connection, and pairing a
second app invalidates the first. CSP Mux scans CSP's QR once, holds that
connection, and re-shares it as a proxy QR that up to eight apps can scan.

![CSP Mux sharing, with the proxy QR on screen](docs/assets/mux-sharing.png)

## Use it

1. In CSP: **File > Connect to smartphone** (**Datei > Mit Smartphone
   verbinden...**). Leave the QR on screen. The menu item is a toggle —
   selecting it while Companion Mode is on turns it off.
2. Run `CSP Mux.exe`. Pick a **Connection scope** in Settings; it locks while
   sharing.
3. Select **Scan CSP QR**. Wait for status **Sharing** and the proxy QR.
4. Point each app at the proxy QR. CSP's QR can be dismissed now.

Window 460x620. Closing hides to the tray; exit from the tray icon.

## Connection scope

| Scope | Reachable from | Cost |
|---|---|---|
| **This computer only · 127.0.0.1** (default) | apps on this PC | none |
| **A private IPv4 address** | a phone or tablet on that network | one Windows Firewall prompt |

Only the picked address is bound; non-private ones are refused.

## Hide QR

**Hide QR** blurs the code in place; the proxy and every connected app keep
running. Settings can hide it after the first app connects.

![CSP Mux with the proxy QR blurred](docs/assets/mux-qr-hidden.png)

## CSP Palette Companion

No QR scan. Sharing on loopback publishes
`%LOCALAPPDATA%\CSP Suite\mux-session.json`, which the Companion connects from —
a proxy credential only, never CSP's.
[Details](https://git.heerlab.com/beasty/csp-app-multiplexer/wiki/Palette-Companion-Integration).

## Download

Windows 10/11, 64-bit. Both files are the same app.

| Download | Size | You need |
|---|---|---|
| `CSP-Mux-<version>-win-x64-needs-dotnet8.exe` | 2.3 MB | the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| `CSP-Mux-<version>-win-x64-standalone.exe` | 68.7 MB | nothing |

Take the small one unless you would rather not install the runtime. Both are
unsigned single-file bundles that scanners sometimes flag —
[Installation](https://git.heerlab.com/beasty/csp-app-multiplexer/wiki/Installation)
has the `SHA256SUMS.txt` check and the exclusion steps.

## Build

.NET 8 SDK, Windows only —
[Build from source](https://git.heerlab.com/beasty/csp-app-multiplexer/wiki/Build-from-Source).

## Status

Proof of concept, verified against CSP PRO 4.0.10 with a German UI: CSP to Mux
to Palette Companion, a 12-colour palette extracted through the proxy.
Unproven: other Companion clients, upstream reconnection after CSP drops the
socket, load, and per-client permissions — every app has the same capabilities.
Companion Mode is reverse-engineered and breaks if CSP changes its protocol.

[Wiki](https://git.heerlab.com/beasty/csp-app-multiplexer/wiki) · built on
MIT-licensed
[`chocolatkey/clipremote`](https://github.com/chocolatkey/clipremote) and
ZXing.Net (`THIRD-PARTY-NOTICES.md`) · (C) 2026 beasty, [GPL-3.0](LICENSE).
