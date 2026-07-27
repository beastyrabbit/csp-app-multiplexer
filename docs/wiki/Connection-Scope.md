# Connection scope

Connection scope decides what address the proxy listens on. It is in Settings
and applies at the next start of sharing.

| Scope | Binds | Use for |
| --- | --- | --- |
| **This computer only** (default) | `127.0.0.1` | Desktop tools, the Palette Companion handoff, untrusted networks |
| **Private IPv4 address** | The one address you pick | A phone or tablet on that network segment |

The proxy never binds `0.0.0.0` and never advertises a public address. The
Settings list offers only private IPv4 addresses that exist on this machine. If
a saved address is gone at the next start, the app says so instead of falling
back.

Windows Firewall prompts the first time LAN mode binds. Allow **Private
networks** only. If you dismissed it, sharing starts but nothing reaches it —
remove the blocking rule in Windows Defender Firewall and share again.

## Security posture

Downstream authentication is terminated at the Mux with its own credentials and
password rotation, and CSP's credentials never reach a downstream app. There is
no permission model and no rate limiting; up to 8 clients can attach.

Anyone who scans the proxy QR gets a full Companion Mode session against your
CSP. The QR **is** the credential — keep it out of screenshots, streams and
screen shares. Use **Hide QR** once your apps have paired; it drops nobody, and
Settings can hide it after the first app connects.

![CSP Mux with the QR hidden](../assets/mux-qr-hidden.png)
