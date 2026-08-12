---
title: How to use this harness
slug: how-to-use-this-harness
category: getting-started
related-concepts: [herald-native-mode, fuzz-testing, docker-compose-root]
last-reviewed: 2026-08-10
---

# How to use this harness

This guide is for the moment right after you clone MMP.SlotDemo for a
new project. It covers what you get out of the box, how to run it, and how to
grow it into whatever you're building.

## What this repo is

MMP.SlotDemo is the companion site for the *Building a Slot Machine RTP
Simulator* series: one interactive page per episode, a live PAR sheet, a
reading library, and a 10-million-spin proving ground, all running the
engine's own code on the server.

It grew out of a general-purpose .NET 10 + Vue 3 harness, and that skeleton is
still underneath if you want it as a starting point for something else. Most
new projects start the same way: a server that serves a single-page app, an API
under it, a place to add a database or a second service later, and a dashboard
that already looks finished. Building that from scratch costs a day you would
rather spend on the product. Clone this, strip the chapter
pages, and you have a working server, SPA, structured logging, a docker
compose root, and two fuzz test suites, wired together and passing.

The **STAT** button on the Start page is the surviving piece of that original
harness: one click exercises the server, the log relay, and the viewer, which
makes it a fast end-to-end smoke test.

### Adding a page

The chapter registry (`CSharp/web/src/chapters/registry.ts`) is the single list
the nav and the router both read. A new page is one entry — id, label, title,
blurb, component — plus the component itself and, if it needs server work, a
`Map…` extension alongside the others in `CSharp/src/SlotDemo.Server/Chapters/`.

## Prerequisites

- **.NET 10 SDK** — the server targets `net10.0`.
- **Node.js 22+** — the SPA build and the Vitest suite need it. The Docker
  image pins `node:22-alpine`.
- **Docker** (optional) — only needed if you want the compose path instead of
  running the server and SPA directly.

## Run it

### Dev loop

Two terminals, two dev servers, one proxy between them:

```bash
# Terminal 1 — the API
dotnet run --project CSharp/src/SlotDemo.Server

# Terminal 2 — the SPA, hot-reloading
cd CSharp/web && npm install && npm run dev
```

Vite serves the SPA at `:5173` and proxies `/api/*` requests to the server at
`:5090`. Open `http://localhost:5173` and edit Vue components. Changes show
up without a rebuild.

### Production-shaped run

```bash
cd CSharp/web && npm install && npm run build && cd ../..
dotnet run --project CSharp/src/SlotDemo.Server
```

`npm run build` compiles the SPA into `CSharp/web/dist`. The server checks
for that folder at startup and serves it as static files with a
history-mode fallback to `index.html`. If it isn't there, the server still
starts and answers `/` with a plain-text reminder to build the SPA first.
Open `http://localhost:5090`.

### Docker

```bash
cd docker && docker compose up --build
```

The `Dockerfile` is a three-stage build: a Node stage compiles the SPA, a
.NET SDK stage publishes the server, and a slim ASP.NET runtime stage copies
both in. The container answers on `:5090`.

## The dashboard and the STAT flow

The dashboard loads, fetches `/api/hello` for a footer status line, and
waits. Press **STAT** and the frontend calls `/api/stats`. The backend runs
`AiSystemProbe.CaptureAsync`, a version check plus a live process scan for
each catalog entry (Claude Code, GitHub Copilot, Codex, Cursor, Gemini,
Ollama), and returns a snapshot: machine info, and one card's worth of data
per AI system. The dashboard renders each as a `SystemCard`, showing whether
it's installed, whether it's currently running, and how many processes and
how much memory it's using.

> 💡 **Quick picture.** Think of STAT the way a hospital uses the word: a
> fast, on-demand check of vital signs. The server looks only when you ask.
> There's no background timer and no polling loop.

Every value that reaches a Vue component passes through a sanitize layer
first (`CSharp/web/src/api/sanitize.ts`). The backend's JSON is external
data as far as the frontend is concerned, and the sanitize layer's job is to
turn anything malformed into a safe default rather than let a bad field
crash a component. This is why the frontend fuzz suite (below) can throw
broken payloads at the app and expect it to keep rendering.

## Logging: what a Herald-in-native-mode call site looks like

The server logs through **Herald.OSS running in native mode**. A call site
names a category and a level, then a structured template:

```csharp
log.Information(appCategory, "Hello endpoint served for harness {Harness}",
    new LogProperty("Harness", "CSharp"));
```

Custom levels read the same way through the general `Log` overload — a
level, a category, a template:

```csharp
log.Log(SlotDemoLevels.SysWarningLevel, someCategory, "template here",
    properties: null, context: null, eventId: null);
```

The first line is real, from `Program.cs`'s `/api/hello` handler.

### The 10-level set

Most loggers give you a severity scale: verbose, debug, information,
warning, error, fatal. This harness keeps that scale but splits four of its
rungs in two. `SlotDemoLevels.cs` is the single place the set and its
rank order live:

```
sys.verbose, verbose,
sys.debug,   debug,
sys.information, information,
sys.warning, warning,
error, fatal
```

The `sys.*` half of each pair is for framework noise: anything logged by a
`Microsoft.*` or `System.*` category, which in an ASP.NET Core app means
startup messages, dependency-injection chatter, hosting diagnostics. The
plain half is for the app's own events. `SystemAwareHeraldProvider` is the
piece that sorts one from the other: it wraps every `ILogger` ASP.NET Core
hands out, checks the category name, and routes to the `sys.` level when the
category starts with `Microsoft` or `System`.

> 💡 **Quick picture.** Think of the level set as two lanes on one highway.
> A `sys.information` event and a plain `information` event matter at the
> same severity; one came from the framework and one came from your code.
> Filtering by rank works across both lanes, and the lane tells you which is
> which.

Error and fatal stay shared between framework and app on purpose: a failure
is a failure, whoever raised it.

### Why the harness logs this way

Splitting framework noise from application signal at the level, rather than
by category filtering after the fact, means one `WithMinimumLevel` threshold
can drop verbose ASP.NET Core startup chatter while keeping the harness's
own `information`-level events. The reverse works too, if you want the
framework's verbosity without your own.

### Where the logs land

- **Console** — every log line prints to the terminal you started the
  server from, rendered for reading.
- **File** — `logs/slotdemo-.ndjson` (relative to the working directory
  the server runs from), rolling daily, five days retained, capped at 10 MB
  per file before Herald starts a new one. The `.ndjson` extension tells
  Herald to write structured JSON: one object per line carrying `time`,
  `level`, `level_key`, `level_rank`, `category`, `message_template`, the
  rendered `message`, and every template property as data — ready for jq,
  ingestion, or the dashboard without parsing log text.

On shutdown, `await herald.DisposeAsync()` runs in a `finally` block so any
buffered file-sink output is written before the process exits.

### Packages

| Package | Version | What it's for |
|---|---|---|
| `Herald.OSS` | 0.12.11 | The logging engine |
| `MMP.Herald.Sinks.File` | 0.2.1 | The rolling file sink |

Herald.OSS is open source. The engine behind these call sites is public and
ready to read at
[github.com/mmpworks/Herald.OSS](https://github.com/mmpworks/Herald.OSS).

### Notes for anyone reusing the logging setup

Two behaviours of the logging engine shape how this repo configures it, and
both are pinned by tests in `HeraldLoggingTests.cs` marked `PINNED ENGINE
BEHAVIOR` / `PINNED ENGINE BUG`: custom levels need their own floor re-check
(`SlotDemoLevels.AtOrAbove(...)`, installed alongside `WithMinimumLevel` in
`Program.cs` and every test pipeline), and an unclosed template brace is
handled differently on the native and Serilog-compat surfaces.

Read those tests before changing the pipeline. If either starts failing, the
engine changed the behaviour it pins. Revisit the workaround rather than
weakening the test.

## How to extend the harness

### Add a new API endpoint

Add a `MapGet` (or `MapPost`, etc.) call in `Program.cs` under `/api/*`:

```csharp
app.MapGet("/api/ping", () => Results.Ok(new { pong = true }));
```

Log inside the handler the way the existing endpoints do:
`log.Information(category, template, ...)` for a severity level, or
`log.Log(...)` for one of the domain levels. Pass a structured message
template rather than an interpolated string, so the fields stay queryable in
whatever sink reads the log later.

### Add a new AI system to the STAT probe

`AiSystemProbe.Catalog` in `CSharp/src/SlotDemo.Server/AiSystems.cs` is a
flat array of catalog rows. One row is one system, and adding a system means
adding a row:

```csharp
new("windsurf", "Windsurf", ["windsurf --version"], ["windsurf"]),
```

The four fields are the system's ID, its display name, the shell commands to
try for a version string (first one that exits 0 wins), and the process-name
substrings to match during the live scan. Add the row, and the probe, the
API response, and the dashboard card all pick it up automatically. No other
file changes.

The catalog does one job: describe a system. Version probing, process
scanning, JSON serialization, and card rendering all compose off that one
flat structure, so no system needs its own code path.

### Add a new service to docker compose

`docker/docker-compose.yml` keeps the harness's own service
(`slotdemo`) untouched and expects new services as additional blocks
below it: a database, a second frontend, a worker process.

```yaml
postgres:
  image: postgres:17-alpine
  environment:
    POSTGRES_PASSWORD: dev
  ports:
    - "5432:5432"
```

The compose file already carries commented-out examples in this form. Copy
one, adapt it, uncomment.

### Replace hello-world with your real app

There's no scaffolding to tear out. The pattern to follow:

1. Keep `/api/hello` if you like it as a health check, or delete it.
2. Add your real endpoints next to `/api/stats` in `Program.cs`, or split
   them into their own file the way `AiSystems.cs` keeps the STAT logic out
   of `Program.cs`.
3. Replace `App.vue`'s STAT button and card grid with your real UI. The
   `MachineStrip`, `SystemCard`, `LoadingState`, and `ErrorPanel` components
   are yours to keep, adapt, or delete. Keep the sanitize-layer pattern in
   `CSharp/web/src/api/sanitize.ts` whatever your API returns. Treating
   server responses as untrusted external data is a habit that pays for
   itself the first time a backend field changes form.
4. Point the existing fuzz suites (below) at your new API surface instead of
   deleting them. They cost little to keep and a lot to re-derive.

## Running the test suites

Both ends carry a seeded fuzz suite. The random generator starts from a
fixed seed, so a failing case replays the same input every run.

```bash
# Backend — xUnit
dotnet test CSharp/tests/SlotDemo.Server.Tests

# Frontend — Vitest
cd CSharp/web && npm run test
```

### What the backend suite checks

`HttpFuzzTests` runs 300 requests with randomized HTTP methods, paths, and
query strings against the live server through `WebApplicationFactory`. There
is no real network socket, just the in-process ASP.NET Core pipeline. Three
invariants hold across every request: the server never returns a 5xx, path
traversal attempts (`../`, percent-encoded `..%2f`, backslash variants)
never leak a file outside the SPA root, and the `/api/hello` and
`/api/stats` endpoints survive arbitrary methods and bodies without
crashing.

`VersionOutputFuzzTests` runs 10,000 iterations of random byte blobs
(control characters, wide unicode, mixed line endings) through the CLI
version-string parser and checks it never throws and always returns either
`null` or a single trimmed line.

### What the frontend suite checks

`fuzz.spec.ts` takes a valid `/api/stats` payload and mutates it at random,
deleting a field or replacing it with `NaN`, a huge string, or nested
garbage. A seeded pseudo-random generator drives it, so the same mutation
sequence runs every time. It checks three layers:

- the format helpers always return a string, whatever they're given;
- `sanitizeStats` always produces the contract shape the Vue components
  expect;
- a full `App.vue` mount never throws a component error, whether the
  response is malformed JSON, a non-2xx status, or a rejected request.

## Non-goals (v1)

Auth, persistence, SignalR, and multi-project templating machinery wait for
a project that needs them. The seams stay open: a stable `/api` route
prefix, and a compose file that expects one service per external dependency.
See [`PRD.md`](PRD.md) for the full v1 scope.
