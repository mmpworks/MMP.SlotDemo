# MMP.SlotDemo

A ready-to-clone project harness: a .NET 10 backend, a Vue 3 dashboard, and a
docker root for growing past the C# core. Clone it, rename it, and a new
project starts with a working web stack instead of an empty folder.

The harness ships one screen and one button. Press **STAT** and it scans the
machine for AI coding tools (Claude Code, GitHub Copilot, Codex, Cursor,
Gemini, Ollama) and shows what's installed and what's running. That is the
v1 feature. Everything else in this repo exists so the *next* project you
clone it for can replace that screen while the server, build, docker, and
test wiring underneath keeps working.

## Layout

| Path | What it is |
|---|---|
| `CSharp/` | The C# bootstrap: server + web dashboard |
| `CSharp/src/SlotDemo.Server/` | .NET 10 minimal API — serves the SPA and `/api/*` |
| `CSharp/web/` | Vue 3 + Vite + TypeScript dashboard |
| `CSharp/tests/` | xUnit fuzz suite for the backend |
| `CSharp/web/tests/` | Vitest fuzz suite for the frontend |
| `docker/` | Compose root — add external front/back services as new compose entries |
| `docs/` | `PRD.md` and `how-to-use-this-harness.md` |

## Run it

The production-shaped path:

```bash
# 1. Build the SPA
cd CSharp/web && npm install && npm run build && cd ../..

# 2. Start the server
dotnet run --project CSharp/src/SlotDemo.Server

# 3. Open http://localhost:5090 — click STAT
```

Dev loop for the SPA: `npm run dev` in `CSharp/web` (Vite on `:5173`, proxies
`/api` to `:5090`).

Docker instead:

```bash
cd docker && docker compose up --build
```

## What v1 does

- `GET /api/hello` — hello-world payload (use it as a health check).
- `GET /api/stats` — probes the machine for AI systems (Claude Code, GitHub
  Copilot, Codex, Cursor, Gemini, Ollama): CLI version + a live process scan
  (count, memory, start time).
- The dashboard renders it all behind one **STAT** button.

## Logging

The server logs through **Herald.OSS in native mode**, using a custom
10-level event set. The `sys.*` levels carry framework noise and the plain
levels carry application signal. Two sinks are wired by default: a rendered
console writer and a rolling NDJSON file (structured JSON, one object per
line).

[`docs/how-to-use-this-harness.md`](docs/how-to-use-this-harness.md) covers
the level set, what a call site looks like, the configuration code, and why
the harness logs this way.

## Testing

Both ends carry a **seeded fuzz suite**: the tests generate malformed input
from a fixed random seed, so a failing case replays the same input every run.

```bash
# Backend — xUnit, from CSharp/tests/SlotDemo.Server.Tests
dotnet test CSharp/tests/SlotDemo.Server.Tests

# Frontend — Vitest, from CSharp/web
cd CSharp/web && npm run test
```

[`docs/how-to-use-this-harness.md`](docs/how-to-use-this-harness.md) covers
what each suite asserts.

## Extending

- New API surface: add endpoints in `Program.cs` under `/api/*`.
- New AI system in the stats probe: one catalog row in `AiSystems.cs`.
- New external service (database, second frontend, worker): one block in
  `docker/docker-compose.yml`.

## License

Apache-2.0 — see [LICENSE](LICENSE).
