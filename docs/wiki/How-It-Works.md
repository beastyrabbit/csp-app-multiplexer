# How it works

The Mux is one Companion Mode client and one Companion Mode server at the same
time. It holds CSP's single connection, and offers a connection that looks like
CSP's to everything else.

```
Clip Studio Paint
        │  one authenticated upstream connection
        ▼
   ┌─────────┐
   │ CSP Mux │  proxy QR, own credentials
   └─────────┘
     │   │   │  independent downstream sessions
     ▼   ▼   ▼
  App  App  App
```

## Upstream — the one connection

The Mux scans CSP's real QR code from the screen, decodes the pairing
invitation, and authenticates as the smartphone-side client. From then on it
owns that connection. CSP has no idea anything else exists.

If the upstream drops, every downstream session goes with it. Controlled
upstream reconnection is not implemented yet.

## Downstream — the sessions

The Mux starts a CSP-compatible server on the address you chose, generates its
own pairing invitation, and renders it as the proxy QR. Each app that scans it
gets a full, independent session:

| Terminated at the Mux | Forwarded upstream |
| --- | --- |
| Downstream authentication | Companion commands |
| Downstream password rotation | Binary preview tails |
| Serial number space | — |

Downstream credentials are the Mux's own. An app never sees CSP's.

| Limit | Default |
| --- | --- |
| Maximum clients | 8 |
| Maximum concurrent reads | 4 |
| Maximum frame length | 32 MB |

## Serial remapping

The Companion protocol tags each request with a serial number so a response can
be matched to it. Two apps that both open a session both start counting at the
same place, so their serials collide the moment they share one upstream.

The Mux remaps every downstream serial onto its own upstream serial space and
maps the response back. Each app sees only its own reply, at the serial it used.

The integration fixture connects two clients that both use serial `7`,
deliberately completes their upstream calls out of order, and asserts that each
receives only its own response.

Server pushes go the other way: a broadcast from CSP is fanned out to every
downstream session with a serial each of them will accept.

## Command scheduling

One upstream connection, several apps, and some commands change CSP's state.
Sending those concurrently would interleave them.

The scheduler sorts every forwarded command into one of two gates.

| Gate | Commands | Concurrency |
| --- | --- | --- |
| Ordered queue | `SetCurrentColor`, `SetColorSelectionModel`, `SetBrushSize`, `SetAlpha`, `DoGesture`, `DoNavigator`, `DoQuickAccess`, `SetServerSelectedTabKind`, `DoModeChange` | 1 |
| Bounded reads | everything else | 4 |

Mutating commands run one at a time, in arrival order, so a colour change from
one app never lands in the middle of a brush change from another. Reads run
concurrently up to the bound, so one app requesting a large canvas preview does
not block every other app's state queries.

`DoQuickAccess` is on the mutating list. That is the command CSP Palette
Companion uses to trigger its
[Auto Action](https://git.heerlab.com/beasty/csp-color-palette-gen/wiki/Selection-Canvas-Auto-Action).

## Reconnection

A downstream app that drops can reconnect with the same proxy invitation while
the Mux is still sharing, which is why **Hide QR** does not disconnect anyone —
it only stops showing the code.

## What is covered by tests

| Area | Coverage |
| --- | --- |
| Serial collision | Two clients on serial `7`, upstream completions out of order |
| Proxy QR | Encode/decode round trip |
| Broadcasts | Server state pushes fanned out to multiple sessions |
| Reconnection | Downstream reconnect against a live proxy |

## Source

| Type | File |
| --- | --- |
| Broker | `src/CspMultiplexer.Broker/CompanionMultiplexer.cs` |
| Scheduler | `src/CspMultiplexer.Broker/CompanionCommandScheduler.cs` |
| Limits | `src/CspMultiplexer.Broker/CompanionMultiplexerOptions.cs` |
| Upstream client | `src/CspMultiplexer.Broker/UpstreamCompanionClient.cs` |
| Wire format | `src/CspMultiplexer.Protocol/` |

`CspMultiplexer.Protocol` and `CspMultiplexer.Broker` are plain `net8.0`
libraries with no Windows dependency, so they build and test on any platform.
`CspMultiplexer.App` is WPF and requires Windows.

## Credit

The Companion Mode protocol implementation follows the MIT-licensed work in
[`chocolatkey/clipremote`](https://github.com/chocolatkey/clipremote). QR
decoding uses ZXing.Net. See `THIRD-PARTY-NOTICES.md`.

Companion Mode is an unofficial, reverse-engineered integration. A CSP update
that changes the private wire protocol can break it.
