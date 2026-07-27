# Installation

Windows 10 or newer, x64. No installer: one `.exe`, put it anywhere.

## Which download

| Build | Size | Needs |
| --- | --- | --- |
| `CSP-Mux-<version>-win-x64-needs-dotnet10.exe` | 0.9 MiB | latest .NET 10 Desktop Runtime |
| `CSP-Mux-<version>-win-x64-standalone.exe` | 73.8 MiB | nothing |

Take the small one unless you refuse to install a runtime; it trips antivirus
less often. Neither is trimmed or AOT-compiled — Windows Forms (tray icon, QR
rendering) cannot be trimmed (`NETSDK1175`).

Get the latest **.NET Desktop Runtime 10, x64** from
<https://dotnet.microsoft.com/download/dotnet/10.0>. Neither the plain .NET nor
the ASP.NET Core runtime works.

## Verify the download, and antivirus

Compare the SHA256 with the release page:

```powershell
Get-FileHash ".\CSP-Mux-<version>-win-x64-standalone.exe" -Algorithm SHA256
```

A single-file .NET app unpacks itself into a temp directory on first launch —
the same shape as packed malware — and it is unsigned, so heuristic scanners
flag it, the standalone build most of all.

- SmartScreen **Windows protected your PC**: **More info** > **Run anyway**.
  That is the missing signature, not a detection.
- Hash matches, scanner quarantines it: exclude that one file, not its folder —
  Windows Security > Virus & threat protection > Manage settings > Exclusions.
- Hash does not match: delete it, download again. Never exclude a file you could
  not verify.

## Files, firewall, uninstall

`%LOCALAPPDATA%\CSP App Multiplexer\settings.json` is plain JSON, safe to
delete. `%LOCALAPPDATA%\CSP Suite\mux-session.json` exists only while sharing on
loopback. No registry keys.

Loopback needs no firewall rule; LAN mode may prompt once, see
[Connection Scope](Connection-Scope).

Uninstall: **Exit** from the tray icon, delete the `.exe` and
`%LOCALAPPDATA%\CSP App Multiplexer`.

## Build from source

.NET 10 SDK on Windows; WPF does not build on Linux or macOS.

```powershell
dotnet publish src/CspMultiplexer.App -c Release -r win-x64 -p:PublishSingleFile=true
# standalone: --self-contained true, IncludeNativeLibrariesForSelfExtract, EnableCompressionInSingleFile
```
