# Article Series — Building a Slot Game Engine

Seven articles for Medium, walking through the design and math of MMP.SlotGame.
Each has a matching 20-minute recording script in `docs/scripts/`.

| # | Article | Script | Core topic |
|---|---|---|---|
| 1 | [System Design: A Slot-Game RTP Simulator](01-system-design.md) | `01-system-design-script.md` | Requirements → architecture, the two-path pipeline, ADR-001 |
| 2 | [Money You Can Trust: Integer Millicents and Deterministic Randomness](02-money-and-randomness.md) | `02-money-and-randomness-script.md` | `Millicents` (incl. `ScaledMultiply`), `SpinRng`, invariants M1/M2/R3 |
| 3 | [Reels Are Strips, Not Dice: Modeling Slot Geometry](03-reels-and-paylines.md) | `03-reels-and-paylines-script.md` | `StripReelSet`, `Payline`, `LinePayEvaluator`, 3–5 row windows |
| 4 | [The PAR-Sheet Math in Code: Expected RTP and Variance](04-paytable-math.md) | `04-paytable-math-script.md` | `Paytable`, `PaytableSolver`, `AnalyticMath`, σ |
| 5 | [A Replayable Parallel Simulation Engine](05-simulation-engine.md) | `05-simulation-engine-script.md` | `SimulationEngine`, `RunTotals`, channels, convergence |
| 6 | [Games as Data: Loading a Third-Party Slot Deconstruction](06-games-as-data.md) | `06-games-as-data-script.md` | `GameDefinition`, `WinEvaluator`, `GameRunner`, `GameAnalyzer`, Orca Dive |
| 7 | [Proving the Machine: Ground Truth, Statistics, and Bit-for-Bit Determinism](07-proving-the-machine.md) | `07-proving-the-machine-script.md` | Test architecture, exhaustive enumeration, AC-1..AC-7 |

## Publishing notes

- **Diagrams.** Articles carry Mermaid source. Medium does not render Mermaid —
  before publishing, export each block to PNG via [mermaid.ink](https://mermaid.ink),
  the [Mermaid Live Editor](https://mermaid.live), or `mmdc -i in.mmd -o out.png`,
  and replace the code block with the image. Each block is marked with an
  `<!-- EXPORT -->` comment.
- **Code blocks.** Medium's editor keeps fenced code blocks; paste as-is or use
  GitHub gists for syntax highlighting.
- **Order.** The series reads front to back, but articles 2–6 each stand alone.
  Article 1 links forward; articles 2+ open with a one-paragraph recap and a link
  back to article 1.
- **Repo link.** Add the repository URL to each article's footer once the repo is
  public. The placeholder is `(repo link)`.
- **Companion labs.** Articles carry "Try it live" callouts pointing at the
  MMP.SlotDemo harness (`dotnet run` from `CSharp/src/SlotDemo.Server`, then
  <http://localhost:5090>): chapters 2–7 map to `#/ch02`…`#/ch07`, and the live
  convergence run lives at `#/finale`. Update the host in those callouts when the
  harness is published somewhere public.
