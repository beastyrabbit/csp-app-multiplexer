# Palette Companion integration

[CSP Palette Companion](https://git.heerlab.com/beasty/csp-color-palette-gen) can
connect through the Mux without scanning the proxy QR. The Mux publishes a small
file while it is sharing; the Companion reads it and connects.

## Setup

1. Mux Settings: connection scope = **This computer only**. The file is written
   on loopback scope only.
2. Mux: **Scan CSP QR**, wait for the proxy QR.
3. Companion Settings: enable **Use CSP Mux when it is running**.
4. Companion: select **Connect**.

The Companion's heading goes `CSP Mux is sharing` → `Connecting through CSP Mux`
→ `Ready — through CSP Mux`. The Mux's client pill reads `1 app`.

![CSP Mux connected with the Companion attached](../assets/mux-sharing.png)

## The handoff file

`%LOCALAPPDATA%\CSP Suite\mux-session.json` holds the proxy pairing URL and the
writing process's id and start time. The Mux is its only writer. It is deleted
on a clean stop, and by the next start if a crash left it behind.

The Companion polls it every 2 seconds and refuses it unless the named process
is alive, is `CSP Mux`, started at the recorded time, owns a listener on that
port, and every address in the pairing URL is loopback. **Connect** re-reads and
re-validates from scratch; the polled result only drives the wording on screen.

## When it refuses

Every refusal is recoverable by scanning the proxy QR instead.

| Says | Fix |
| --- | --- |
| CSP Mux is not sharing | No file was written, because scope is a LAN address. Set **This computer only** and share again. |
| Could not verify CSP Mux | The apps run at different integrity levels. Run both as the same user, without elevation. |
| CSP Mux is newer than this app | Update CSP Palette Companion. |
| Cannot use CSP Mux, after it worked | The listener stopped. Start sharing again in the Mux. |

The handoff is specific to CSP Palette Companion. Every other Companion client
joins by scanning the proxy QR, on loopback and on LAN alike.
