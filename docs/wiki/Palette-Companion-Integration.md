# Palette Companion integration

[CSP Palette Companion](https://git.heerlab.com/beasty/csp-color-palette-gen) can
connect through the Mux without scanning the proxy QR. The Mux publishes a small
file while it is sharing; the Companion reads it and connects.

## Setup

| Where | Do |
| --- | --- |
| CSP Mux, Settings | Connection scope = **This computer only** |
| CSP Mux | **Scan CSP QR**, wait for the proxy QR |
| Palette Companion, Settings | Enable **Use CSP Mux when it is running** |
| Palette Companion | Select **Connect** |

The Companion's heading reads `CSP Mux is sharing` when the handoff is usable,
then `Connecting through CSP Mux`, then `Ready — through CSP Mux`. The Mux's
client pill goes to `1 app`.

![CSP Mux connected with the Companion attached](../assets/mux-sharing.png)

## The handoff file

| Property | Value |
| --- | --- |
| Path | `%LOCALAPPDATA%\CSP Suite\mux-session.json` |
| Schema version | 1 |
| Maximum size accepted | 4096 bytes |
| Written | Once per sharing session, only on loopback scope |
| Deleted | On clean stop, and by the next start if a crash left it behind |
| Writer | CSP Mux only. The Companion never writes, creates or deletes anything in this directory. |

### Schema

```json
{
  "schemaVersion": 1,
  "pairingUrl": "...",
  "processId": 12345,
  "processStartTimeUtc": "2026-07-27T09:14:02.1234567Z"
}
```

| Field | Purpose |
| --- | --- |
| `schemaVersion` | Exact match required. Higher means the reader is too old; lower means malformed. |
| `pairingUrl` | The proxy invitation, the same one encoded in the proxy QR. |
| `processId` | Identifies the writing process. |
| `processStartTimeUtc` | Distinguishes that process from a later process that reused the PID. |

Property names are camelCase on write and case-insensitive on read. Two
repositories share this contract with no compiler enforcing it, so a casing drift
on either side must not be able to break it silently.

Every field is required. A missing field, an explicit `null`, or a document that
is the JSON literal `null` is rejected.

## How it is written

| Step | Why |
| --- | --- |
| Write to a randomly named temp file in the same directory | So no reader ever sees a partial document |
| Set the DACL on the **temp** file, before any content is written | A file created and then re-ACLed is readable for the interval between the two calls |
| DACL: current user + `LOCAL SYSTEM`, Full Control, inheritance disabled | `%LOCALAPPDATA%` carries an inherited Full-Control ACE for an AppContainer capability SID. A low-integrity process is exactly the class that could otherwise reach this credential. |
| Medium integrity label, no-read-up | Best effort. If it fails the DACL still holds. |
| `FileOptions.WriteThrough` | A crash leaves the file complete or absent, never half |
| Atomic move over the destination | A polling reader sees the old file or the new one |

`File.Move` within a volume preserves the source's explicit ACEs and does not
re-inherit from the destination directory. That is why the ACL goes on the temp
file rather than being applied afterwards.

If publishing fails for any reason it fails silently and the QR path still works.
Nothing on this path is logged: a diagnostic sink here is how the pairing URL
ends up in a second place on disk that nothing cleans up.

## How it is read

The Companion polls the path every 2 seconds and refuses the file unless every
check passes.

| Check | Refusal |
| --- | --- |
| File present | `Absent` |
| Parses, ≤ 4096 bytes, all fields present and non-null | `Malformed` |
| `schemaVersion` not higher than the reader's | `VersionTooNew` |
| Named process is alive and its image name is `CSP Mux` | `Stale` |
| Its start time matches `processStartTimeUtc` (1 second tolerance) | `Stale` |
| Its start time could be read at all | `Unverifiable` |
| **Every** address in the pairing URL is a loopback address | `NotLoopback` |
| That process actually owns a listener on that address and port | `PortNotOwned` |

All checks pass → `Live`, and **Connect** routes through the Mux.

The address rule is *every* address, not *any*. The file path has no human
confirmation step, so it is deliberately tighter than the QR path's
private-or-loopback rule.

The port-ownership check is re-run on every poll even when the file has not
changed. That is what lets the Companion notice within one tick that the Mux
stopped listening without its process exiting.

At the moment **Connect** is pressed the file is re-read and re-validated from
scratch, cache bypassed. The polled result only drives the wording on screen; the
fresh read is what a credential is sent on.

## What the Companion shows

| Reader status | Heading | Instruction |
| --- | --- | --- |
| `Live` | CSP Mux is sharing | — |
| `Absent`, `Stale` | CSP Mux is not sharing | Start sharing in CSP Mux, or connect to CSP. |
| `NotLoopback` | Cannot use CSP Mux | CSP Mux is sharing on a network. Scan its QR. |
| `PortNotOwned` | Cannot use CSP Mux | CSP Mux is not sharing on that port. Scan its QR. |
| `Unverifiable` | Cannot use CSP Mux | Could not verify CSP Mux. Scan its QR instead. |
| `VersionTooNew` | Cannot use CSP Mux | CSP Mux is newer than this app. Update Companion. |
| `Malformed` | Cannot use CSP Mux | Cannot read CSP Mux. Scan CSP's QR instead. |

Every refusal is recoverable by scanning the proxy QR. The handoff is a
convenience, never the only route.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| Companion says **not sharing** while the Mux shows a proxy QR | Scope is a LAN address, so no file was written | Set **This computer only** and start sharing again, or scan the proxy QR. |
| **Cannot use CSP Mux — could not verify** | The two apps run at different integrity levels, so the process cannot be inspected | Run both as the same user without elevation, or scan the proxy QR. |
| **CSP Mux is newer than this app** | Handoff schema is ahead of the Companion build | Update CSP Palette Companion. |
| Handoff worked, then stopped without the Mux closing | The listener stopped; `PortNotOwned` | Start sharing again in the Mux. |
| Companion still offers the Mux after the Mux was killed | Should clear within one 2-second poll | If it does not, **Connect** re-validates and refuses. Nothing is sent to a dead session. |
| A stale `mux-session.json` after a crash | Reaped by the next Mux start or stop | Delete `%LOCALAPPDATA%\CSP Suite\mux-session.json` if you want it gone now. |

## Other Companion apps

The handoff is specific to CSP Palette Companion. Any other Companion client
joins by scanning the proxy QR, which works identically on loopback and LAN.
