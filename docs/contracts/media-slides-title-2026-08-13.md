# Contract: chapter slides, animations, script refresh, series title sequence

Engagement date: 2026-08-13 · Status: OPEN · First engagement of the contractor tier
(tier ratified by Steve 2026-08-13; mechanics per
`E:\dev\MMP.AiUpgrade\docs\contractors-proposal-2026-08-11.md`).

## Scope (acceptance-gate form)

1. **Per-chapter slide decks + animations** for the 9 video lessons
   (`docs/articles/01`–`09`, scripts at `docs/scripts/01`–`09`).
   - One deck per chapter, source-of-truth as code (Remotion or a Vue/HTML deck —
     contractor's call, recorded in the deliverable README), renderable to assets
     Steve can drop into a video edit.
   - Animations illustrate the chapter's core mechanism (e.g. ch3 reels/paylines,
     ch5 weighted enumeration, ch9 the byte-ID window optimization) — derived from
     the article content, never invented numbers.
   - GATE: every deck builds/renders from a clean checkout with one documented command.
2. **Lesson-script refresh** — `docs/scripts/*.md` brought current with the code and
   articles as they stand today (post episode-9 optimization pass, American spelling,
   current benchmark medians from `current.md`).
   - GATE: no script cites a number, API name, or file that the repo no longer contains.
3. **Series title sequence** — a COOL 6-second opener: "Programming Gems" / "Slot Games"
   (two-line lockup), used in all videos, plus a 4-second addition that identifies the
   chapter title (templated — one render per chapter).
   - GATE: 6s master + nine 4s chapter cards render to mp4 (1080p minimum) with one
     documented command; chapter card takes its title from data, not hand-edited copies.

## Skills granted

`remotion` (+ `remotion-best-practices`), `motion-designer`, `presentation-design`,
`storyboard`, `mmpworks-writing-voice` (scripts), `muir-slot-design` (domain),
`ffmpeg`. Visual register: MMPWorks house (1940s-50s sci-fi optimism per brand anchor)
unless Laura overrides in review.

## Sponsor

**Laura** (media designer / creative director). Independent reviewer: one
fool/apprentice-fool pass on the script refresh (factual drift check vs repo).

## Boundaries

- Work in a dedicated worktree/branch: `contract/media-2026-08-13`. The main tree and
  `codex/optimization-lab` are out of bounds.
- Files in scope: new `video/` (or similarly named) asset tree, `docs/scripts/*.md`
  edits, this contract file. Engine code (`CSharp/`), articles, PRD: READ-ONLY.
- STOP conditions: any change that would touch engine code or article content beyond
  reading it; any deliverable that needs a paid external service; scripts contradicting
  the repo (report the contradiction, don't resolve it unilaterally).

## Review clause

Sponsor (Laura persona) + independent factual pass review the branch diff + rendered
samples. Verdict is data (pass / blocker list). Max 2 change-request rounds, then
escalate to Steve. Merge is the sponsor's action, never the contractor's.

## Outcome (appended at close)

_pending_
