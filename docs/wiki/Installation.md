# Installation

Windows 10 or newer, x64. No installer — one `.exe`, put it anywhere.

## Which download

| Build | Size | Needs |
| --- | --- | --- |
| `CSP-Mux-win-x64.exe` | 2.3 MiB | .NET 8 Desktop Runtime |
| `CSP-Mux-win-x64-self-contained.exe` | 68.7 MiB | nothing |

Take the small one unless you refuse to install a runtime; it also trips
antivirus less often. Neither build is trimmed or AOT-compiled: the tray icon
and QR rendering use Windows Forms, which the SDK will not trim (`NETSDK1175`).

Get **.NET Desktop Runtime 8.0.x, x64** from
<https://dotnet.microsoft.com/download/dotnet/8.0>. WPF needs the *Desktop*
runtime; the plain .NET and ASP.NET Core runtimes do not work.

## Verify the download, and antivirus

Compare the SHA256 with the release page before running:

```powershell
Get-FileHash .\CSP-Mux-win-x64.exe -Algorithm SHA256
```

A single-file .NET app unpacks itself into a temp directory on first launch, the
same shape as packed malware, and these binaries are unsigned — so heuristic
scanners flag them. The standalone build trips them more often.

- SmartScreen **Windows protected your PC**: **More info** > **Run anyway**.
  That is the missing signature, not a detection.
- Hash matches but the scanner quarantines it: exclude that one file, not its
  folder (Windows Security > Virus & threat protection > Manage settings >
  Exclusions). Report it at
  <https://www.microsoft.com/en-us/wdsi/filesubmission>.
- Hash does not match: delete it, download again. Never exclude a file you could
  not verify.

## Files, firewall, uninstall

`%LOCALAPPDATA%\CSP App Multiplexer\settings.json` is plain JSON, safe to delete
and rewritten with defaults. `%LOCALAPPDATA%\CSP Suite\mux-session.json` exists
only while sharing on loopback. No registry keys.

Loopback needs no firewall rule; LAN mode may prompt once, see
[Connection Scope](Connection-Scope).

Uninstall: **Exit** from the tray icon, delete the `.exe` and
`%LOCALAPPDATA%\CSP App Multiplexer`, remove any firewall rule.

## Build from source

.NET 8 SDK on Windows; WPF does not build on Linux or macOS.

```powershell
dotnet publish src/CspMultiplexer.App -c Release -r win-x64 -p:PublishSingleFile=true
```

Standalone build: add `--self-contained true`,
`-p:IncludeNativeLibrariesForSelfExtract=true`,
`-p:EnableCompressionInSingleFile=true`.
