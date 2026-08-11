# MMP.SlotDemo

The companion site for the *Building a Slot Machine RTP Simulator* series. Every
episode gets a page here: a short written brief plus controls that run the
episode's own code on the server and narrate each step through Herald, so the log
stream at the bottom of the page shows the same computation from the inside.

Forked from [MMP.WorkHarnesses](../MMP.WorkHarnesses) — .NET 10 server, Vue 3 SPA,
Herald.OSS logging with an SSE relay into a live viewer. The harness's STAT probe
survives as the start page because it exercises the whole pipeline in one click,
which is a useful pre-record smoke test.

## Branch layout

One branch per chapter. `main` carries the shell — chapter registry, hash routing,
nav, the persistent log viewer — and nothing episode-specific.

| Branch | Contents |
|---|---|
| `main` | Chapter shell, start page, log viewer |
| `chapter-02` | Millicents / SpinRng labs: exact money, seeded streams, modulo bias |

A chapter branch adds its page under `CSharp/web/src/chapters/`, its endpoints
under `CSharp/src/SlotDemo.Server/Chapters/`, and flips its row in
`CSharp/web/src/chapters/registry.ts` from placeholder to built.

## Run it

```bash
# 1. Build the SPA
cd CSharp/web && npm install && npm run build && cd ../..

# 2. Start the server
dotnet run --project CSharp/src/SlotDemo.Server

# 3. Open http://localhost:5090
```

Dev loop for the SPA: `npm run dev` in `CSharp/web` (Vite on `:5173`, proxies
`/api` to `:5090`).

## Why the labs run server-side

A JavaScript reimplementation of `Millicents` would prove nothing about
`Millicents` — and JavaScript cannot even hold a 64-bit draw without losing bits.
The chapter endpoints carry copies of the episode's real C# files, so what the
page reports is what the simulator does. Raw 64-bit values cross the wire as hex
strings for the same reason.

## Chapter 2 endpoints

| Route | What it demonstrates |
|---|---|
| `POST /api/ch2/money` | Integer money against a `double` twin: drift, the 64-bit view, and the refusal an odd raw amount triggers |
| `POST /api/ch2/rng` | Per-worker streams under SplitMix64 seeding versus naive `seed + workerId` |
| `POST /api/ch2/bias` | Modulo bias against Lemire multiply-shift over a narrowed draw space |

## Logging

The server logs through Herald.OSS in native mode with a custom 10-level set;
`sys.*` levels carry framework noise, plain levels carry application signal. The
HttpJson sink posts to `/api/logs/ingest`, which fans out over SSE to the viewer.
See `docs/how-to-use-this-harness.md` for the harness-level detail.
