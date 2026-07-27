# Connection scope

Connection scope decides what address the proxy listens on. It is in Settings,
and it takes effect at the next start of sharing.

| Scope | Binds | Who can reach it | Firewall prompt |
| --- | --- | --- | --- |
| **This computer only** (default) | `127.0.0.1` | Apps on this machine | No |
| **Private IPv4 address** | Exactly the one address you pick | Devices on that network segment | Likely, once |

The proxy never binds `0.0.0.0`, never binds every interface, and never
advertises a public address. LAN mode binds the single private address selected
in the UI and nothing else.

## This computer only

The default, and the right choice for desktop tools. Loopback traffic does not
leave the machine and does not pass through the firewall.

CSP Palette Companion's automatic handoff only works in this mode — it refuses
the handoff file unless every address in it is a loopback address. See
[Palette Companion Integration](Palette-Companion-Integration).

## Private IPv4 address

Use this when a phone or tablet on the same Wi-Fi needs to scan the proxy QR.

The Settings list only offers private IPv4 addresses that are actually on this
machine. Pick the one on the network the other device is on.

| Range | Typical use |
| --- | --- |
| `192.168.0.0/16` | Home routers |
| `10.0.0.0/8` | Larger or corporate networks |
| `172.16.0.0/12` | Docker, VPNs, some routers |

If a saved address is gone at the next start — a docking station unplugged, a
VPN adapter down — the app says so instead of silently falling back.

## Firewall

Windows Firewall usually prompts the first time LAN mode binds. Allow it on
**Private networks** only. Decline **Public networks**.

If you dismissed the prompt, sharing starts but nothing can reach it. Remove the
blocking rule for the app in Windows Defender Firewall and start sharing again.

Loopback needs no rule at all.

## Security posture

What the proxy is, stated plainly:

| Property | Status |
| --- | --- |
| Downstream authentication | Terminated at the Mux, with its own credentials and password rotation |
| CSP's credentials | Never given to a downstream app |
| Anyone who scans the proxy QR | Gets a full Companion Mode session against your CSP |
| Command permission model | Not implemented — any forwarded command is forwarded |
| Rate limits | Not implemented |
| Maximum simultaneous clients | 8 |

The proxy QR **is** the credential. Treat it the way you treat CSP's own QR: do
not put it in a screenshot, a stream, or a screen share.

Use **Hide QR** after your apps have paired. It blanks the code without dropping
CSP or any connected app; **Show QR** brings it back when another app needs to
join. Settings can hide it automatically after the first app connects.

![CSP Mux with the QR hidden](../assets/mux-qr-hidden.png)

## Choosing

| Situation | Scope |
| --- | --- |
| CSP Palette Companion, ColorPenguin, other desktop tools | This computer only |
| Automatic handoff to CSP Palette Companion | This computer only — required |
| A phone or tablet as a Companion client | Private IPv4 address |
| Untrusted network, café Wi-Fi, shared office segment | This computer only |
