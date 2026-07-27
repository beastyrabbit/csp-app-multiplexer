# CSP Mux

Clip Studio Paint offers one Companion Mode connection. CSP Mux takes that one
connection and re-shares it, so several tools can use CSP at the same time.

![CSP Mux sharing, proxy QR visible](../assets/mux-sharing.png)

Window: 460 x 620. It lives in the system tray by default; the close button
hides it and **Exit** is in the tray menu.

## The problem it solves

CSP presents a single Companion Mode QR code and behaves like a single
remote-control host. Tools such as CSP Palette Companion and ColorPenguin each
want to be the smartphone-side client. Connecting one prevents the other from
connecting, or invalidates the credentials it already scanned.

| Without the Mux | With the Mux |
| --- | --- |
| CSP → one app | CSP → Mux → many apps |
| Second app is refused or steals the connection | Each app gets its own downstream session |
| Re-pairing every time you switch tools | Pair once, leave it running |

## Workflow

1. In CSP, select **File > Connect to smartphone** (German: *Datei > Mit Smartphone verbinden…*) and keep the real QR visible.
2. Open the Mux. Choose **This computer only** or a private IPv4 address under **Connection scope** in Settings.
3. Select **Scan CSP QR**.
4. Wait for the proxy QR to appear.
5. Point every Companion app at the **proxy** QR instead of CSP's real QR.

**File > Connect to smartphone** is a toggle with a checkmark, not a dialog.
Selecting it while it is already enabled turns Companion Mode **off**.

![CSP Mux connected with an app attached](../assets/mux-sharing.png)

The client pill counts attached apps. **Hide QR** blanks the code without
dropping CSP or any connected app; **Show QR** brings it back when another app
needs to join. Settings can hide it automatically after the first app connects.

![CSP Mux with the QR hidden](../assets/mux-qr-hidden.png)

## Verified

CSP → Mux (one upstream connection) → CSP Palette Companion (downstream through
the proxy) → a 12-colour palette extracted from the canvas. The Mux's client pill
read `1 app`; the Companion's status read `Ready — through CSP Mux`.

## Pages

| Page | Covers |
| --- | --- |
| [Installation](Installation) | The two downloads, the .NET 8 Desktop Runtime, SHA256 verification, antivirus, file locations |
| [How It Works](How-It-Works) | The broker: one upstream, downstream sessions, serial remapping, command scheduling |
| [Connection Scope](Connection-Scope) | Loopback vs LAN, the firewall prompt, security posture |
| [Palette Companion Integration](Palette-Companion-Integration) | The handoff file, its schema, its ACL, and the auto-connect flow |

## Status

Working proof of concept, not a production release. Still missing: controlled
upstream reconnection, rate limits, and a permission model for dangerous
commands.

## File locations

| What | Path |
| --- | --- |
| Settings | `%LOCALAPPDATA%\CSP App Multiplexer\settings.json` |
| Session handoff file (written while sharing) | `%LOCALAPPDATA%\CSP Suite\mux-session.json` |

Licensed GPL-3.0.
