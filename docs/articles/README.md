# Article Series — Building a Slot Game Engine

Nine articles for Medium, walking through the design, math, proof, and measured optimization of MMP.SlotGame.
Each has a matching 20-minute recording script in `docs/scripts/`.

| # | Article | Script | Core topic |
|---|---|---|---|
| 1 | [System Design: A Slot-Game RTP Simulator](01-system-design.md) | `01-system-design-script.md` | Requirements → architecture, the two-path pipeline |
| 2 | [Money You Can Trust: Integer Millicents and Deterministic Randomness](02-money-and-randomness.md) | `02-money-and-randomness-script.md` | `Millicents` (incl. `ScaledMultiply`), `SpinRng`, invariants M1/M2/R3 |
| 3 | [Reels Are Strips, Not Dice: Modeling Slot Geometry](03-reels-and-paylines.md) | `03-reels-and-paylines-script.md` | `StripReelSet`, `Payline`, `LinePayEvaluator`, 3–5 row windows |
| 4 | [The PAR-Sheet Math in Code: Expected RTP and Variance](04-paytable-math.md) | `04-paytable-math-script.md` | `Paytable`, `PaytableSolver`, `AnalyticMath`, σ |
| 5 | [Counting Every Outcome Without Playing Every Spin](05-weighted-enumeration.md) | `05-weighted-enumeration-script.md` | `GameAnalyzer`, weighted enumeration, recursion, loaded-game RTP |
| 6 | [A Replayable Parallel Simulation Engine](06-simulation-engine.md) | `06-simulation-engine-script.md` | `SimulationEngine`, `RunTotals`, channels, convergence |
| 7 | [Games as Data: Loading a Third-Party Slot Deconstruction](07-games-as-data.md) | `07-games-as-data-script.md` | `GameDefinition`, `WinEvaluator`, `GameRunner`, Orca Dive |
| 8 | [Proving the Machine: Ground Truth, Statistics, and Bit-for-Bit Determinism](08-proving-the-machine.md) | `08-proving-the-machine-script.md` | Test architecture, exhaustive enumeration, the acceptance suite |
| 9 | [Optimize the Machine You Proved](09-optimization.md) | `09-optimization-script.md` | Baselines, paired implementations, byte windows, failed experiments |

## Publishing notes

- **Diagrams.** Articles carry Mermaid source. Medium does not render Mermaid —
  before publishing, export each block to PNG via [mermaid.ink](https://mermaid.ink),
  the [Mermaid Live Editor](https://mermaid.live), or `mmdc -i in.mmd -o out.png`,
  and replace the code block with the image. Each block is marked with an
  `<!-- EXPORT -->` comment.
- **Code blocks.** Medium's editor keeps fenced code blocks; paste as-is or use
  GitHub gists for syntax highlighting. Comment facts a learner cannot recover from
  the syntax, such as units, ownership, flat-array layout, domain rules, or why setup
  work moved out of a hot loop. Do not comment a line merely by restating it.
- **Optimization notebooks.** End each construction chapter with a short summary and
  a bulleted list of the techniques discussed. Each bullet names the technique and
  the correctness check or measurement that controls whether it is accepted.
- **Order.** The series reads front to back, but articles 2–9 each stand alone.
  Article 1 links forward; articles 2+ open with a one-paragraph recap and a link
  back to article 1.
- **Repo link.** Add the repository URL to each article's footer once the repo is
  public. The placeholder is `(repo link)`.
- **Companion labs.** Articles carry "Try it live" callouts pointing at the
  MMP.SlotDemo harness (`dotnet run` from `CSharp/src/SlotDemo.Server`, then
  <http://localhost:5090>). Articles 2–9 use `#/ch02` through `#/ch09`. The live
  convergence run is at `#/finale`; the optimization race is at `#/ch09`.
