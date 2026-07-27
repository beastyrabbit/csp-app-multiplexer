# How it works

The Mux is a Companion Mode client and server at once. It holds CSP's single
connection and offers a connection that looks like CSP's.

```
Clip Studio Paint
        │  one upstream connection
        ▼
   ┌─────────┐
   │ CSP Mux │  proxy QR, own credentials
   └─────────┘
     │   │   │  independent sessions
     ▼   ▼   ▼
  App  App  App
```

It scans CSP's real QR from the screen and authenticates as the smartphone-side
client; CSP never learns anything else exists. If the upstream drops, so does
every downstream session.

Each app that scans the proxy QR gets a full independent session on the Mux's
own credentials and never sees CSP's. Authentication, password rotation and the
serial number space stop at the Mux; commands and preview tails are forwarded. A
dropped app rejoins with the same invitation, which is why **Hide QR**
disconnects nobody. Defaults: 8 clients, 4 reads, 32 MB frames.

## Serial remapping

The protocol tags each request with a serial so a reply can be matched to it,
and two apps both start counting at the same place. The Mux remaps every
downstream serial onto its own upstream space and maps the reply back, so each
app sees only its own response. Broadcasts are fanned out the other way.

## Command scheduling

Mutating commands — `SetCurrentColor`, `SetColorSelectionModel`, `SetBrushSize`,
`SetAlpha`, `DoGesture`, `DoNavigator`, `DoQuickAccess`,
`SetServerSelectedTabKind`, `DoModeChange` — go through an ordered queue, one at
a time, so one app's colour change never lands inside another's brush change.
Everything else is a read and runs concurrently, so a large canvas preview does
not block everyone's state queries. `DoQuickAccess` is what CSP Palette
Companion's
[Auto Action](https://git.heerlab.com/beasty/csp-color-palette-gen/wiki/Selection-Canvas-Auto-Action)
uses.

Broker and scheduler live in `src/CspMultiplexer.Broker/`, the wire format in
`src/CspMultiplexer.Protocol/`, after the MIT-licensed
[`chocolatkey/clipremote`](https://github.com/chocolatkey/clipremote). Companion
Mode is reverse-engineered: a CSP update that changes the private wire protocol
can break it.
