# Clancy copy pass — MMP.SlotDemo web app

**Date:** 2026-08-11
**Scope:** user-visible English prose in `CSharp/web/src/` — chapter briefs, lab ledes, lab notes,
verdict labels, the ParSheet explainer dictionary, Library entries, registry blurbs, HarnessHome
intro, Finale prose. Code, CSS, and attribute names ignored.
**Mode:** suggestions only. No source file was edited.

Target register for every rewrite below: an engineer's plain teaching notes, high-school reading
level, warm but flat. Facts carry the weight.

---

## Cross-cutting patterns (read this before the per-file list)

Four habits repeat across nearly every file. Fixing them file-by-file works, but they are one
habit each, so it may be cheaper to sweep them:

1. **The closing flourish.** Almost every paragraph in the app ends on a short summarizing
   sentence or fragment that restates what was just said with more rhetoric ("...and that is why
   the simulation exists.", "The whole series on one chart.", "three ways of being wrong would
   have to agree."). Individually most are defensible; end to end they make the page read as
   persuasion. Cutting roughly two-thirds of them would change the whole register.
2. **Em-dash as rhythm.** ~30 mid-sentence em-dashes across the prose. Most attach a summarizing
   clause to a sentence that already ended. A period or a comma does the same job without the
   drumroll.
3. **The capitalized copula.** "This table IS the odds", "Our strips ARE this idea",
   "The strip is the distribution", "A game that is data can be enumerated", "NextInt is this
   paper". Five instances of the same move.
4. **The same fact told three times.** "The strip layout is the only source of odds" appears in
   `registry.ts` l.57, `Chapter03.vue` l.204, and `ParSheet.vue` l.106. The 2× Wild Orca vs 5000×
   Red 7 comparison appears in `Chapter04.vue` l.222 and `ParSheet.vue` l.130. The
   "100× spins buys 10× certainty" point appears in `Chapter04.vue` l.209 and `ParSheet.vue`
   l.154. Repetition-for-emphasis is itself a tell; pick one home for each and let the others
   point at it.

---

## `chapters/registry.ts`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.41 | "Requirements, the two-lane split between exact math and lossy telemetry, and the back-of-envelope that says one process is enough. The interactive blueprint lands here." | Rule of three; slogan closer ("lands here") | "Requirements, the split between exact math and lossy telemetry, and the estimate that says one process is enough." (drop the second sentence — the placeholder page already says the lab is pending) |
| l.49 | "Millicents and SpinRng: integer money down to the bit, seeded per-worker streams, and modulo bias you can see." | Rule of three (the blurb rhythm is identical in l.49/57/65/73/81) | Keep the content, break the cadence: "Millicents keeps money as integers. SpinRng gives each worker its own seeded stream. A third lab shows modulo bias." |
| l.57 | "A reel is a strip, a window is a slice of it, and the strip layout is the only source of odds in the whole engine." | Anaphora on "is"; triple; duplicates ParSheet l.106 and Ch03 l.204 | "A reel is a strip of symbols and a window shows a few of them. Nothing else in the engine sets the odds." |
| l.65 | "...closed-form sigma prices the confidence band before a single spin." | Dramatic flourish ("before a single spin") | "...and closed-form sigma gives the confidence band without running the game." |
| l.73 | "Fixed quotas, batched atomic totals, and a lossy telemetry lane you can starve without moving the truth." | Rule of three; second-person consequence framing; "the truth" as drama | "Fixed quotas, batched atomic totals, and a telemetry lane that can drop samples without changing the totals." |
| l.81 | "the loader compiles it or returns every problem at once, and declared facts are verified against the strips" | Rule of three inside one sentence | Fine as content; split into two sentences to break the list cadence. |
| l.89 | "Exhaustive enumeration referees the simulation: three implementations sharing only the game data, agreeing." | Mic-drop fragment ("agreeing.") | "Exhaustive enumeration referees the simulation. Three implementations share only the game data, and their answers match." |
| l.105 | "Every claim in these episodes has a shelf it came from." | Slogan ending | "Each episode's claims are sourced from these." |
| l.113 | "Ten million spins live: the measured RTP walking into the analytic band as the funnel narrows. The whole series on one chart." | Mic-drop fragment closer | "Ten million spins live, with the measured RTP converging into the analytic band as the funnel narrows." (cut the second fragment) |

## `chapters/HarnessHome.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.37–41 | "The controls run the episode's own types on the server rather than a JavaScript lookalike, and every step narrates itself through Herald, so the log stream at the bottom of the page is the same code path talking back." | Snap-contrast ("rather than a JavaScript lookalike"); personification ("narrates itself", "talking back") | "The controls call the episode's own types on the server, not a JavaScript copy. Each step logs through Herald, so the log stream at the bottom of the page comes from the code you just ran." |
| l.63–66 | "It stays because it exercises the server, the SSE log relay, and the viewer in one click — a quick way to confirm the plumbing before recording." | Rule of three plus em-dash tail | "It stays because one click exercises the server, the SSE log relay, and the viewer. Handy for confirming the plumbing before recording." |

## `chapters/Chapter02.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.22–23 | "Everything downstream gets to be ordinary because these two are strict." | Slogan closer; personification ("gets to be", "are strict") | "Because these two types are strict, the code downstream of them can be ordinary." Or cut — the paragraph already made the point. |
| l.43 | "The labs below run copies of those exact files" | Fluff intensifier ("exact") | "The labs below run copies of those files" |
| l.55–57 | "Episode 3 takes those numbers and asks what a reel actually is — and why a strip of positions behaves differently from a weighted die, even when the two look identical on a spec sheet." | Em-dash cadence; filler "actually" | "Episode 3 takes those numbers and asks what a reel is, and why a strip of positions behaves differently from a weighted die even when the two look identical on a spec sheet." |

## `chapters/Chapter03.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.107–110 | "That is a different object from a weighted die — adjacent stops travel together into the window... Orca Dive makes the point concretely:" | Em-dash cadence; claiming importance ("makes the point") | "That is a different object from a weighted die. Adjacent stops travel together into the window, so the strip's layout shapes what a multi-row window can show. In Orca Dive the reels are 26/29/26/29/26 stops..." |
| l.123–125 | "The same seed and index always produce the same window — invariant R3 doing its job." | Em-dash; personification tag | "The same seed and index always produce the same window. That is invariant R3." |
| l.199 | "Lab 2 — The strip is the distribution" | Copula-as-claim heading | "Lab 2 — Counting what the strip actually produces" |
| l.202–204 | "No probability table exists anywhere in the engine — the strip's layout is the only source of odds." | Em-dash; third repeat of the same sentence across the app | "The engine holds no probability table. The strip layout sets the odds." Keep the full statement in one place only. |
| l.241–243 | "Push the spin count up and the gap column shrinks — the same convergence the proving ground shows for the whole game." | Em-dash tail | "Push the spin count up and the gap column shrinks, the same convergence the proving ground shows for the whole game." |
| l.250–254 | "...are the seam the math chapter stands on: the paytable solver, the sigma calculation, and Orca's exhaustive enumeration all read the strips directly, which is how the analytic twin prices a game without playing a single spin." | Rule of three; metaphor ("the seam ... stands on", "analytic twin"); flourish ending | "Episode 4 uses `ProbabilityOf` and `JointProbabilityOf` directly. The paytable solver, the sigma calculation, and the exhaustive enumeration all read the strips, so a game can be priced without running it." |

## `chapters/Chapter04.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.80–81 | "...scales every pay by that one number — a closed form, no search loop." | Em-dash; snap-contrast fragment | "...scales every pay by that one number. It is a closed form, with no search loop." |
| l.153–156 | "Rare rows carry big pays, common rows small ones; the product of each row's pay and probability, summed, is the RTP. That sum is what the solver scaled." | Antithesis pairing plus restating closer | "Rare rows carry big pays and common rows carry small ones. Sum each row's pay times its probability and you have the RTP — the number the solver scaled." (one sentence goes, not both) |
| l.163–165 | "The band the finale draws is z·σ/√N — this table is that funnel as numbers." | Em-dash; copula slogan | "The band the finale draws is z·σ/√N. This table is the same figure in numbers." |
| l.207–211 | "Each factor of 100 in spins buys one decimal place of certainty — the square root in the denominator is why proving an RTP takes millions of spins, and why the simulation exists." | Em-dash; anaphora ("why... and why"); mic-drop ("why the simulation exists") | "Each factor of 100 in spins buys one decimal place of certainty. The square root in the denominator is why proving an RTP takes millions of spins." |
| l.218–223 | "The same arithmetic still decides everything — each row's pay times its probability is that row's slice of the RTP... Rows sort by contribution: the 2× Wild Orca single carries more of the game than the 5000× Red 7 jackpot." | Em-dash; "decides everything" overstatement; duplicates ParSheet l.130 | "The arithmetic is the same: each row's pay times its probability is that row's slice of the RTP, and the exhaustive enumerator supplies the probabilities. Sorted by contribution, the 2× Wild Orca single outweighs the 5000× Red 7 jackpot." (drop the duplicate in ParSheet, or here) |
| l.264–268 | "the solver walks from a target RTP to the pays; a published game walks from the pays to the RTP. Same equation, opposite direction — and the enumeration behind these probabilities is episode 7's referee." | Chiasmus word-play; snap-contrast fragment; em-dash | "The solver goes from a target RTP to the pays; a published game goes the other way. The probabilities here come from the enumeration episode 7 uses as its referee." |
| l.275–277 | "The analytic twin predicts; the engine measures. Episode 5 builds the machine that plays ten million spins fast enough to put the two on one chart." | Antithesis opener as slogan | "The analytic figures predict what the engine should measure. Episode 5 builds the engine that plays ten million spins fast enough to chart both." |

## `chapters/Chapter05.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.74–76 | "Telemetry rides a bounded drop-oldest channel carrying absolute snapshots — the lossy lane can lose everything but the truth." | Em-dash; word-play closer ("lose everything but the truth") | "Telemetry rides a bounded drop-oldest channel carrying absolute snapshots, so dropped samples cost chart points and nothing else." |
| l.84 | "Lab 1 — Same seed, same answer, any day" | Anaphora slogan heading | "Lab 1 — Same seed, same answer" |
| l.152–156 | "The returned column is the claim: ...and the wall-time column shows the schedule had no say in the answer." | Claiming importance ("is the claim"); personification ("the schedule had no say") | "Read the returned column: integer money (M2), fixed quotas, and seeded streams (R3) make an N-worker run reproducible. Wall time varies between runs; the totals do not." |
| l.161 | "Lab 2 — Starve the telemetry, keep the truth" | Slogan heading | "Lab 2 — Starve the telemetry lane" |
| l.164–166 | "Shrink the capacity and the drop rate climbs — and the exact totals underneath never move, because the two lanes never touch." | Em-dash | "Shrink the capacity and the drop rate climbs. The exact totals never move, because the two lanes never touch." |
| l.216–218 | "This is the design the finale page rides: the browser sees a consolidated curve while the integer counters stay lossless." | Metaphor ("rides") | "The finale page uses the same design: the browser sees a consolidated curve while the integer counters stay lossless." |

## `chapters/Chapter06.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.66–68 | "...so a PAR-sheet transcription error is caught at load, before it becomes a wrong RTP." | Mild consequence drama | "...so a PAR-sheet transcription error is caught at load rather than showing up as a wrong RTP." |
| l.137–140 | "Collecting the whole list is the design choice: an author fixes a file in one pass instead of replaying load-fix-load for each error." | Claiming importance ("is the design choice") | "The loader collects every problem before it answers, so an author fixes a file in one pass instead of load-fix-load for each error." |
| l.178 | "A game that is data can be enumerated." | Copula-as-claim opener | "Because the game is data, every stop combination can be walked." |

## `chapters/Chapter07.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.61–66 | "The enumerator walks every stop combination — no randomness, no sampling — and counts what each category pays. When the simulation's measured RTP lands inside the band around the enumerator's exact figure, the machine is proved, because three ways of being wrong would have to agree." | Em-dash pair; negative-pair rhythm ("no randomness, no sampling"); aphoristic mic-drop closer — the strongest AI-tell in the app | "The enumerator walks every stop combination, with no randomness and no sampling, and counts what each category pays. When the simulation's measured RTP lands inside the band around the enumerator's exact figure, all three methods agree — and they share nothing but the game data." |
| l.77–79 | "Every combination counted. The classic game's space is 11,616; Orca Dive's is 14,781,416. Both enumerate in well under a second, and the result is exact." | Fragment opener; closer restating "exact" (already said at l.62) | "The classic game's space is 11,616 combinations; Orca Dive's is 14,781,416. Both enumerate in well under a second." |
| l.210 | "The payoff" (heading) | Claiming importance | "The full run" |
| l.212–214 | "with the convergence curve walking into the narrowing band on screen" | Personification, third use of "walking into the band" across the app | "with the convergence curve settling into the narrowing band on screen" — or cut, since the finale blurb says the same thing. |

## `chapters/Finale.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.207–210 | "...the enumerated reference for Orca Dive is 86.11%, and the run has to walk into that band or the machine is wrong." | Second-person consequence drama ("or the machine is wrong"); em-dash | "A shipped game brings its own paytable, so there is no RTP to choose. The enumerated reference for Orca Dive is 86.11%, and the run should converge into the band around it." |
| l.212–214 | "The server refuses this rather than clamping it — try it and read the error." | Em-dash; snap-contrast ("rather than"); dare-the-reader imperative | "The server refuses the request instead of clamping it. Submit it to see the error." |
| l.274–276 | "the shaded funnel is what probability theory allows, and the line is the machine walking inside it." | Paired copulas; personification | "The shaded funnel is the range probability theory allows; the line is the measured RTP inside it." |
| l.296–300 | "Ten million spins become a couple hundred points; the browser never sees the flood, and the flood never waits for the browser." | Chiasmus word-play — the most conspicuous rhetorical figure in the app | "Ten million spins become a couple hundred points. The browser never receives the full stream, and the workers never wait on the browser." |
| l.299–300 | "Every start, point, and verdict also lands in the log stream below through Herald." | Rule of three | "Starts, curve points, and the final verdict all go to the log stream below through Herald." (or simply "The run also logs to the stream below through Herald.") |

## `chapters/Library.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.43 | "The closest published analogue to this series' whole arc — compare its spreadsheet workflow with our code-first one." | Em-dash | "The closest published analogue to this series. Compare its spreadsheet workflow with our code-first one." |
| l.102 | "The primary documents. Everything the series reconstructs, these sources show in the wild." | Inverted syntax for effect | "The primary documents. These sources show in the wild what the series reconstructs." |
| l.131 | "map many RNG values onto few physical stops and the odds detach from the cabinet. Every weighted strip descends from this filing." | Slogan closer | "...and the odds no longer depend on the physical reel. Every weighted strip in use today descends from this filing." (keep one of the two sentences) |
| l.132 | "Our strips ARE this idea — episode 3's even-interleave builder is a Telnaes mapping laid out in the open." | Capitalized copula for emphasis (named tic); em-dash | "Our strips implement this idea. Episode 3's even-interleave builder is a Telnaes mapping written out in the open." |
| l.141 | "The public benchmark for slot analysis done honestly." | Virtue label ("honestly") | "The public benchmark for open slot analysis." |
| l.177 | "Short, sharp, and immediately usable." | Rule of three | "Short and immediately usable." |
| l.178 | "NextInt is this paper; the chapter 2 bias lab draws its figure 1 live." | Copula-as-claim | "NextInt implements this paper; the chapter 2 bias lab draws its figure 1 live." |
| l.194 | "A simulator needs replayability; a regulator demands unpredictability. These documents are the other side of that line." | Antithesis plus slogan closer | "A simulator needs replayable runs; a regulator requires unpredictable ones. These documents cover the regulator's side." |
| l.204 | "The warning label on SpinRng — simulation-grade, NOT certified-gaming — is defined by this document's other half." | Capitalized NOT for emphasis; em-dash pair | "SpinRng is labelled simulation-grade rather than certified-gaming because of the requirements in this document." |
| l.238 | "Why a 22-stop strip is 'canonical' in episode 3: the mechanical machines made it so." | Slogan closer | "Where episode 3's 'canonical' 22-stop strip comes from: the mechanical machines." |
| l.290–293 | "The shelves above are the public literature that teaches the same techniques — the point of the series is that everything a PAR sheet encodes can be built, priced, and proved in the open." | Claiming importance ("the point of the series is"); rule of three; em-dash | "The shelves above are the public literature that teaches the same techniques. Everything a PAR sheet encodes can be built and checked from open sources." |

## `chapters/ParSheet.vue` — explainer dictionary

The dictionary is the strongest prose in the app: mostly plain, mostly definitional, and the
worked examples (`hits`, l.122) are exactly the target register. Six entries drift.

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.106 (`census`) | "This table IS the odds: a symbol's chance..." plus "There is no separate probability table inside the machine — the strip layout is the only source of randomness shaping." | Capitalized copula; em-dash; third repeat of the "only source of odds" line | "This table sets the odds: a symbol's chance of landing on a payline cell is its count divided by that strip's length. The machine holds no separate probability table." |
| l.114 (`scatter`) | "its absence from reels 2 and 4 is the trigger rule written into the strips." | Copula flourish restating the prior sentence | Cut the clause; "That is why Penguin appears only on reels 1, 3, and 5." already says it. |
| l.130 (`rtpSlice`) | "Sorting by this column is revealing — the humble 2× single Wild Orca contributes more than the 5000× Red 7 jackpot, because frequency beats size in expectation." | Claiming importance ("is revealing"); personification ("humble"); em-dash; aphorism closer | "Sort by this column and the 2× single Wild Orca contributes more than the 5000× Red 7 jackpot: it pays less but lands far more often." |
| l.138 (`hitFrequency`) | "Note the definition matters — this figure counts line wins only" | Claiming importance ("Note ... matters"); em-dash | "This figure counts line wins only. A simulator that counts bonus triggers as hits reports a slightly higher number (~11.4%), so a PAR sheet states which definition it uses." |
| l.154 (`bands`) | "This is the funnel on the proving-ground page, and it is why proving an RTP takes millions of spins." | Closer duplicating Chapter04 l.209 | "The proving-ground page draws this same band as a funnel." |
| l.166 (`strips`) | "This is the part of a PAR sheet manufacturers guard most closely — and here it is the open source of the game." | Em-dash; "open source" word-play mic-drop | "Manufacturers guard this part of a PAR sheet most closely. Here it is published with the game." |

## `chapters/ch02/MoneyLab.vue`, `RngLab.vue`, `BiasLab.vue`

The three labs are the cleanest prose in the app. Three items only.

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| MoneyLab l.80 | "**The type refused the conversion.**" | Dramatic personification, and it is the headline of a results panel | "Conversion refused." with the server's reason below it. (This is the one personification worth keeping if you want a single instance — but it is the loudest placement in the app.) |
| MoneyLab l.115–117 | "Nothing here is a mantissa and an exponent — it is one number, and every bit of it is money." | Em-dash; snap-contrast; slogan closer | "There is no mantissa or exponent here. It is one integer, and every bit of it counts money." |
| MoneyLab l.141–144 | "Integer addition has no such opinion, which is what makes an N-worker run match a 1-worker run bit for bit." | Personification ("no such opinion") | "Integer addition gives the same total in any order, which is what makes an N-worker run match a 1-worker run bit for bit." |

`RngLab.vue` and `BiasLab.vue`: no findings. The bias-lab note at l.125–131 is the model for what
the rest of the app should sound like — a mechanism explained, a consequence stated, nothing
performed.

## `chapters/ChapterPlaceholder.vue`

| Locator | Quoted text | Tell | Suggested rewrite |
|---|---|---|---|
| l.9 | "This lab lands with the episode." | Mild slogan; "lands" is used three other places in the app | "This lab ships with the episode." |

---

## Tally by tell type

| Tell | Count |
|---|---|
| Em-dash cadence (mid-sentence, as rhythm) | 24 |
| Slogan / mic-drop closer | 21 |
| Rule of three / anaphora / triple | 12 |
| Copula-as-claim ("X IS this idea", capitalized IS/ARE/NOT) | 7 |
| Claiming importance ("the point of", "is revealing", "is the claim", "Note ... matters", "makes the point", "The payoff") | 7 |
| Dramatic personification | 7 |
| Snap-contrast "X, not Y" / "rather than" | 6 |
| Word-play / chiasmus repetition | 4 |
| Duplicated fact stated as fresh emphasis | 3 |
| Second-person consequence drama | 2 |
| Fluff intensifier ("exact", "honestly") | 2 |
| **Total flagged** | **95** |

Heaviest files: `Chapter04.vue` (13), `registry.ts` (11), `Library.vue` (11), `ParSheet.vue` (6
of ~17 dictionary entries), `Finale.vue` (6). Cleanest: `BiasLab.vue` and `RngLab.vue` (0),
`Chapter06.vue` (3).

If only one sweep gets done, do the closing-flourish cut: roughly a fifth of the flagged items
are the last sentence of a paragraph that had already finished its work.
