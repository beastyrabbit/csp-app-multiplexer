# Installation

Windows 10 or newer, x64. No installer — the download is a single `.exe` you put
wherever you want.

## Which download

Every release ships two builds of the same app.

| Build | Size | Prerequisite | Pick it when |
| --- | --- | --- | --- |
| `CSP-Mux-win-x64.exe` (framework-dependent) | 2.3 MB | .NET 8 Desktop Runtime | You have the runtime, or you do not mind installing it. Less likely to trip antivirus. |
| `CSP-Mux-win-x64-self-contained.exe` | 73.2 MB | none | You will not install a runtime. |

Both are single-file and compressed. Neither is trimmed or AOT-compiled: the app
uses Windows Forms for the tray icon and for QR rendering, and the .NET SDK
refuses to trim a project that references Windows Forms (`NETSDK1175`).

## .NET 8 Desktop Runtime

Only needed for the 2.3 MB build. If it is missing, Windows shows a dialog naming
the missing framework when you launch the app.

Get **.NET Desktop Runtime 8.0.x, x64** from
<https://dotnet.microsoft.com/download/dotnet/8.0>. The ASP.NET Core runtime and
the plain .NET runtime are not enough — WPF needs the *Desktop* runtime.

Check what is installed:

```powershell
dotnet --list-runtimes | Select-String WindowsDesktop
```

If you also run CSP Palette Companion, one runtime install covers both.

## Verify the download

Each release lists a SHA256 for every file. Compare before running:

```powershell
Get-FileHash .\CSP-Mux-win-x64.exe -Algorithm SHA256
```

If the hash does not match the release page, do not run the file. Download it
again; if it still does not match, open an issue.

## Antivirus and SmartScreen

Heuristic scanners flag these builds sometimes. This is expected and the reason
is mechanical.

### Why it happens

A single-file .NET app is a self-extracting bundle: the `.exe` contains the
runtime payload and unpacks it into a temporary directory the first time it
launches. Unpacking-then-executing is also what packed malware does, so
signature-free heuristics score it. The binaries are unsigned — there is no code
signing certificate on this project — so nothing offsets that score.

The 73.2 MB self-contained build carries the whole runtime and unpacks more, so
it trips scanners more often than the 2.3 MB framework-dependent build.

The Mux also opens a listening socket, which some endpoint products score
separately. See [Connection Scope](Connection-Scope) for what it binds and why.

### SmartScreen

Unsigned executables also produce **Windows protected your PC** on first launch.
Select **More info**, then **Run anyway**. That prompt is about the missing
signature, not about a detection.

### What to do

| Step | Action |
| --- | --- |
| 1 | Verify the SHA256 against the release page. A matching hash means the file is byte-for-byte the one that was published. |
| 2 | If it matches and your scanner still quarantines it, add an exclusion for that one file. |
| 3 | If it does not match, delete it. Do not add an exclusion for a file whose hash you could not confirm. |

### Windows Defender exclusion

Windows Security > Virus & threat protection > Manage settings > Exclusions >
Add or remove exclusions > Add an exclusion > File > pick the `.exe`.

Exclude the specific file, not its folder. Other scanners have an equivalent
setting; the vendor's documentation is the place to look.

### Reporting a false positive

Most vendors accept false-positive submissions. Microsoft's is at
<https://www.microsoft.com/en-us/wdsi/filesubmission>. Submitting the file helps
the next release as well as yours.

## Where the app keeps things

| What | Path |
| --- | --- |
| Settings | `%LOCALAPPDATA%\CSP App Multiplexer\settings.json` |
| Session handoff file | `%LOCALAPPDATA%\CSP Suite\mux-session.json` |

`settings.json` is plain JSON and safe to delete — the app rewrites it with
defaults. No registry keys are written.

`mux-session.json` exists only while the Mux is sharing on loopback. It is
deleted on a clean stop and reaped by the next start if a crash left it behind.
Its contents and permissions are documented in
[Palette Companion Integration](Palette-Companion-Integration).

## Firewall

Loopback needs no firewall rule. LAN mode may raise the Windows Firewall prompt
the first time. See [Connection Scope](Connection-Scope).

## Uninstall

1. Exit the app from the tray icon. This deletes `mux-session.json`.
2. Delete the `.exe`.
3. Delete `%LOCALAPPDATA%\CSP App Multiplexer` if you do not want the settings.
4. Remove any firewall rule you allowed for it.

## Build from source

Prerequisite: .NET 8 SDK on Windows. WPF cannot be built on Linux or macOS.
`CspMultiplexer.Protocol` and `CspMultiplexer.Broker` are plain `net8.0`
libraries and build anywhere; `CspMultiplexer.App` does not.

```powershell
dotnet restore CspAppMultiplexer.sln
dotnet build CspAppMultiplexer.sln -c Release --no-restore
dotnet test CspAppMultiplexer.sln -c Release --no-build --no-restore
```

Framework-dependent single file:

```powershell
dotnet publish src/CspMultiplexer.App/CspMultiplexer.App.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true `
  -o dist/win-x64
```

Self-contained single file:

```powershell
dotnet publish src/CspMultiplexer.App/CspMultiplexer.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o dist/win-x64-self-contained
```

`EnableCompressionInSingleFile` is what takes the self-contained build from
154.9 MB to 73.2 MB.
