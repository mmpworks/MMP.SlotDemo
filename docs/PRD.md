# MMP.SlotDemo — PRD (v1)

**Status:** Accepted — v1 delivered and passing.
**Date:** 2026-08-10

## What

An open-source project harness: a ready-to-clone frontend/backend bootstrap for starting new
MMPWorks projects. It also serves as the recording set for MMPWorks YouTube videos.

## Why

Every new project re-derives the same skeleton: a .NET server that serves a Vite SPA plus an
API, a docker story for extra services, and a dashboard that looks good on camera. Build the
skeleton once and every project after it starts from a working stack.

## Layout

```
/CSharp            C# bootstrap — .NET 10 server + Vue 3/Vite/TS web dashboard
  /src/SlotDemo.Server   minimal API; serves web/dist statically + /api/*
  /web                      SPA (Vue 3 + Vite + TypeScript)
/docker            docker-compose root — add external front/back services beyond the C# core
/docs              this PRD + the how-to guide
```

## v1 acceptance

1. `dotnet run` from `CSharp/src/SlotDemo.Server` serves the SPA at `http://localhost:5090`.
2. Page loads a dashboard styled to the design intent below.
3. A **STAT** button calls `GET /api/stats`; the server probes the machine for running/installed
   AI systems (Claude Code, GitHub Copilot, Codex, Cursor, Gemini, Ollama) and returns
   process + CLI-version stats; the dashboard renders one card per system showing installed
   state, running state, process count, and memory.
4. `GET /api/hello` returns a hello-world payload.
5. `docker/docker-compose.yml` builds and runs the server container; extra services can be
   added as additional compose services without touching the C# core.

**Design intent:** a dark cinematic palette with art-deco detailing.

## Non-goals (v1)

- Auth, persistence, SignalR, multi-project templating machinery. Seams stay open: a stable
  `/api` route prefix, and one compose service per external dependency. The machinery waits
  until a project needs it.
