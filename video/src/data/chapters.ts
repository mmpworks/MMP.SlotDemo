/**
 * The nine chapters, as data.
 *
 * Every line here is derived from `docs/articles/0N-*.md` in this repository —
 * the articles are the source, this file is a transcription of them into slide
 * shape. Nothing is invented: a number on a slide appears in its article, and
 * `source` records where. When an article changes, this file changes with it.
 *
 * The chapter title cards, the deck openers, and the deck bodies all read from
 * this one array, so there is a single place a chapter title is written down.
 */

export type Slide =
  /** Heading plus short claims. The workhorse. */
  | { kind: 'points'; heading: string; points: string[] }
  /** Heading plus large figures. `note` carries provenance in small type. */
  | { kind: 'stat'; heading: string; stats: Stat[]; note?: string }
  /** A verbatim excerpt. `source` is the file the excerpt belongs to. */
  | { kind: 'code'; heading: string; source: string; lines: string[]; caption?: string }
  /** The chapter's mechanism animation, full-bleed under a heading. */
  | { kind: 'anim'; heading: string; caption: string };

export type Stat = { value: string; label: string };

export type Chapter = {
  /** Two-digit id — matches the article filename and the render output name. */
  id: string;
  number: number;
  /** Display title. Short enough for a 4-second card at one line. */
  title: string;
  /** Positioning line above the title. */
  kicker: string;
  /** The chapter's core claim, shown on the deck's opening slide. */
  thesis: string;
  /** The article this chapter was transcribed from. */
  article: string;
  slides: Slide[];
};

export const CHAPTERS: Chapter[] = [
  {
    id: '01',
    number: 1,
    title: 'System Design',
    kicker: 'Requirements, tiers, one cap',
    thesis:
      'Ten million events per second on one side and ten chart updates per second on the other is a seven-orders-of-magnitude gap, and every structural decision answers it.',
    article: 'docs/articles/01-system-design.md',
    slides: [
      {
        kind: 'stat',
        heading: 'The gap the design answers',
        stats: [
          { value: '10,000,000', label: 'spin events produced per second' },
          { value: '10', label: 'chart updates consumed per second' },
        ],
        note: 'Seven orders of magnitude. Article 1.',
      },
      {
        kind: 'points',
        heading: 'Two kinds of data, two rules',
        points: [
          'Run totals are exact integer counters — every spin is counted',
          'Telemetry rides a bounded queue and drops the oldest when full',
          'A dropped sample costs one chart point, never a counted spin',
          'Analytic probabilities are a third concern, held in double',
        ],
      },
      {
        kind: 'anim',
        heading: 'Two lanes out of the worker pool',
        caption: 'Exact totals batch and publish. Telemetry is allowed to drop.',
      },
      {
        kind: 'points',
        heading: 'Three tiers',
        points: [
          'Core: engine class library — no ASP.NET, no logging, no I/O',
          'Server: REST, server-sent events, three logging sinks',
          'SPA: Vue 3 dashboard, live curve against the expected range',
          'Core has no host, so tests run 10M-spin sims with no web server',
        ],
      },
      {
        kind: 'code',
        heading: 'One cap, checked as integers',
        source: 'CSharp/src/MMP.SlotGame.Core/Simulation/SimulationConfig.cs',
        caption: 'RTP arrives as basis points, so the cap check never touches a float.',
        lines: [
          'var aggregate = (long)draft.BaseRtpBasisPoints',
          '              + draft.FreeSpinsRtpBasisPoints',
          '              + draft.PickBonusRtpBasisPoints;',
          '',
          'if (aggregate > MaxAggregateBasisPoints)',
          '    errs.Add($"Aggregate RTP {aggregate} bp exceeds the "',
          '           + $"{MaxAggregateBasisPoints} bp (99.00%) cap. "',
          '           + "Rejected, never clamped.");',
        ],
      },
      {
        kind: 'stat',
        heading: 'The numbers that set the shape',
        stats: [
          { value: '9,900 bp', label: 'aggregate RTP cap — 99.00%' },
          { value: '4,096', label: 'spins per worker batch' },
          { value: '1,024', label: 'telemetry slots, drop-oldest' },
          { value: '50,000', label: 'spins per curve point' },
        ],
        note: 'Article 1. One credit is 100,000 millicents.',
      },
    ],
  },
  {
    id: '02',
    number: 2,
    title: 'Money You Can Trust',
    kicker: 'Integers and replay',
    thesis:
      'Count in a unit small enough that every quantity you care about is a whole number, and the three ways floating-point money fails quietly stop being possible.',
    article: 'docs/articles/02-money-and-randomness.md',
    slides: [
      {
        kind: 'points',
        heading: 'Three failures in one line',
        points: [
          '0.1 + 0.2 == 0.3 returns false',
          'Representation error: 0.1 has no exact binary form',
          'Accumulated drift: millions of rows push the total off the truth',
          'Order dependence: parallel workers can land on different totals',
        ],
      },
      {
        kind: 'stat',
        heading: 'The fix is a unit change',
        stats: [
          { value: '100,000', label: 'millicents in one credit' },
          { value: '100', label: 'scale factor — 2.25X is stored as 225' },
        ],
        note: 'Banks add 1999 cents, never 19.99 dollars. Article 2.',
      },
      {
        kind: 'code',
        heading: 'Divide only when the remainder is zero',
        source: 'CSharp/src/MMP.SlotGame.Core/Money/Millicents.cs',
        caption: 'Fractional pays without fractional money.',
        lines: [
          'if (Value % ScaleFactor != 0)',
          '    throw new InvalidOperationException(...);',
          '',
          'return new Millicents(Value / ScaleFactor * scaledMultiplier);',
        ],
      },
      {
        kind: 'points',
        heading: 'What Millicents refuses',
        points: [
          'A readonly record struct — value semantics, no allocation',
          'Multiplication takes a long: money times money is meaningless',
          'No implicit conversion to double',
          'ToCredits() is the one named, greppable display exit',
        ],
      },
      {
        kind: 'anim',
        heading: 'One seed, eight replayable streams',
        caption:
          'SplitMix64 expands the master seed; each worker keeps a private xoshiro256** stream and a quota fixed before the run.',
      },
      {
        kind: 'stat',
        heading: 'Quotas are assigned, never raced for',
        stats: [
          { value: '10,000,000', label: 'spins requested' },
          { value: '8', label: 'workers' },
          { value: '1,250,000', label: 'spins each, decided up front' },
        ],
        note: 'Worker 0 absorbs any remainder. Article 2.',
      },
    ],
  },
  {
    id: '03',
    number: 3,
    title: 'Reels Are Strips',
    kicker: 'Geometry as data',
    thesis:
      'A reel is an ordered cyclic strip stopped once per spin, so the cells in one column are neighbors on a wire — which leaves every single-symbol probability right and every two-symbol probability wrong.',
    article: 'docs/articles/03-reels-and-paylines.md',
    slides: [
      {
        kind: 'anim',
        heading: 'One stop, one window',
        caption:
          'A single uniform stop index slides a fixed three-cell window along a cyclic strip. The cells are neighbors, not independent draws.',
      },
      {
        kind: 'points',
        heading: 'Where the weighted die breaks',
        points: [
          'Neighbors on the strip are fixed: that conditional probability is 1',
          'A scatter rule inspects every visible position on one reel',
          'Two paylines can read different positions from the same reel',
          'JointProbabilityOf walks all stops to answer that',
        ],
      },
      {
        kind: 'points',
        heading: 'Geometry is an argument, not a constant',
        points: [
          'Reel count, per-reel stop count, and window height are arguments',
          'Orca Dive runs 26/29/26/29/26, so StopCount is per reel',
          'The constructor copies caller arrays; new strips mean a new set',
          'DrawWindow fills a caller-owned Span and allocates nothing',
        ],
      },
      {
        kind: 'code',
        heading: 'The query a die model cannot answer',
        source: 'CSharp/src/MMP.SlotGame.Core/Reels/StripReelSet.cs',
        lines: [
          'public double JointProbabilityOf(',
          '    int reel, int rowA, byte aId, int rowB, byte bId)',
          '{',
          '    var strip = _strips[reel];',
          '    var n = strip.Length;',
          '    var count = 0;',
          '    for (var stop = 0; stop < n; stop++)',
          '        if (strip[(stop + rowA) % n].Id == aId',
          '         && strip[(stop + rowB) % n].Id == bId)',
          '            count++;',
          '    return (double)count / n;',
          '}',
        ],
      },
      {
        kind: 'points',
        heading: 'Five steps from stop to paid line',
        points: [
          'Draw one stop per reel, then read the window',
          'Walk each payline: one row index per reel, left to right',
          'Count the leftmost run before the first mismatch',
          'Pay if the run clears the minimum, then sum across lines',
        ],
      },
      {
        kind: 'stat',
        heading: 'Window height changes the shapes',
        stats: [
          { value: '3 / 5', label: 'MinRows and MaxRows' },
          { value: 'rows / 2', label: 'middleRow — integer division, rounds down' },
        ],
        note: 'At 5 rows both zigzag swings are 2; at 4 rows they are 2 and 1. Article 3.',
      },
    ],
  },
  {
    id: '04',
    number: 4,
    title: 'PAR-Sheet Math in Code',
    kicker: 'RTP and sigma, before any spin',
    thesis:
      'The expected return and the per-spin standard deviation both fall out of the strips and the paytable by counting, so the chart’s confidence band is priced before the first spin runs.',
    article: 'docs/articles/04-paytable-math.md',
    slides: [
      {
        kind: 'stat',
        heading: 'Expected value is a weighted average',
        stats: [
          { value: '0.5', label: 'EV — ten outcomes, one pays 5' },
          { value: '50%', label: 'RTP that produces' },
        ],
        note: 'Raise that pay to 8 and RTP is 80%. The chances never moved. Article 4.',
      },
      {
        kind: 'code',
        heading: 'Exactly k leading symbols',
        source: 'CSharp/src/MMP.SlotGame.Core/Rtp/AnalyticMath.cs',
        caption: 'The trailing mismatch stops four-of-a-kind counting as three.',
        lines: [
          'public static double ExactlyKLeading(',
          '    StripReelSet reels, Payline line, byte symbolId, int k)',
          '{',
          '    var p = 1.0;',
          '    for (var reel = 0; reel < k; reel++)',
          '        p *= reels.ProbabilityOf(reel, symbolId);',
          '    if (k < reels.ReelCount)',
          '        p *= 1.0 - reels.ProbabilityOf(k, symbolId);',
          '    return p;',
          '}',
        ],
      },
      {
        kind: 'anim',
        heading: 'Strips in, priced game out',
        caption:
          'Marginals and joint tables come off the strips, feed EV and sigma, and one scalar resizes the canonical paytable until its average return meets the target.',
      },
      {
        kind: 'points',
        heading: 'Why probabilities may be double',
        points: [
          'Millicents must survive an audit; a probability has no such contract',
          'These come from whole-number counts over a strip length',
          'Tests compare their sums to enumeration at 14 decimal places',
          'BaseEvMultiplier returns a ratio — no wager was passed in',
        ],
      },
      {
        kind: 'points',
        heading: 'Variance needs the covariance',
        points: [
          'Var(sum) = sum of variances + twice the pairwise covariances',
          'Shared cells make lines move together; the strips set sign and size',
          'Per-reel conditions are Match, Mismatch, or Any',
          'rowA == rowB falls out on the diagonal with no special case',
        ],
      },
      {
        kind: 'stat',
        heading: 'The band the dashboard draws',
        stats: [
          { value: '2.576', label: 'two-sided 99% normal quantile' },
          { value: '0.01 pp', label: 'realized-RTP drift tolerance' },
        ],
        note: 'Half-width is z·sigma/sqrt(N). A correct game still lands outside about 1% of the time. Article 4.',
      },
    ],
  },
  {
    id: '05',
    number: 5,
    title: 'Counting Every Outcome',
    kicker: 'Weights instead of spins',
    thesis:
      'Group identical reel outcomes and carry their counts as weights, and the exact average falls out of eight combinations instead of twenty-four — nothing estimated, nothing thrown away.',
    article: 'docs/articles/05-weighted-enumeration.md',
    slides: [
      {
        kind: 'stat',
        heading: 'Twenty-four outcomes, eight combinations',
        stats: [
          { value: '3 × 2 × 4 = 24', label: 'physical outcomes' },
          { value: '8', label: 'symbol combinations after grouping' },
          { value: '2 × 1 × 3 = 6', label: 'weight of three cherries' },
        ],
        note: 'The eight weights still total 24. Article 5.',
      },
      {
        kind: 'anim',
        heading: 'Descend, multiplying weight',
        caption:
          'One symbol box per reel. The running weight multiplies on the way down, so one evaluation at the leaf stands in for every physical stop combination it represents.',
      },
      {
        kind: 'points',
        heading: 'Two layers, one job each',
        points: [
          'Analyze checks whether the game is supported, then hands off',
          'The private Enumeration holds the totals for one calculation',
          'One payline only — multi-line variance needs covariance machinery',
          'Stack the quarters, then multiply by value',
        ],
      },
      {
        kind: 'code',
        heading: 'The descent',
        source: 'CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs',
        lines: [
          'var any = _anyStop[reel];',
          'var trigger = _triggerStop[reel];',
          'for (byte symbol = 0; symbol < any.Length; symbol++)',
          '{',
          '    if (any[symbol] == 0) continue;',
          '    _cells[reel] = symbol;',
          '    Descend(reel + 1,',
          '            weight * any[symbol],',
          '            triggerWeight * trigger[symbol]);',
          '}',
        ],
      },
      {
        kind: 'stat',
        heading: 'Guarding the work',
        stats: [
          { value: '8⁵ = 32,768', label: 'branches for 8 symbols on 5 reels' },
          { value: '200,000,000', label: 'branch ceiling the analyzer refuses to pass' },
        ],
        note: 'Repeated physical stops add weight, never branches. Article 5.',
      },
      {
        kind: 'points',
        heading: 'What Summarize divides out',
        points: [
          'Five tallies: hits, pay units, squared pay units, pay-and-trigger, trigger weight',
          'Multipliers stay scaled integers through the loop',
          'Line RTP = weighted payouts / scale / stop combinations',
          'variance = average(X²) − average(X)², cross term included',
        ],
      },
    ],
  },
  {
    id: '06',
    number: 6,
    title: 'A Replayable Engine',
    kicker: 'Determinism is scheduling',
    thesis:
      '“Same seed, same result” is mostly a property of the scheduler, so the engine hands every worker a fixed quota and one private stream before the first spin runs.',
    article: 'docs/articles/06-simulation-engine.md',
    slides: [
      {
        kind: 'anim',
        heading: 'Twelve spins, four workers',
        caption:
          'Each worker gets three spins, assigned before the run starts. The OS may run worker 3 first; worker 3 still plays its own three.',
      },
      {
        kind: 'points',
        heading: 'Why not Parallel.For',
        points: [
          'Dynamic partitioning decides at runtime which thread runs which iteration',
          'With mutable per-thread streams, totals can move between runs',
          'Parallel.For is reproducible when input derives from the index',
          'Fixed streams and quotas are the simpler contract to hold',
        ],
      },
      {
        kind: 'code',
        heading: 'The batch publishes four numbers',
        source: 'CSharp/src/MMP.SlotGame.Core/Simulation/RunTotals.cs',
        // Verbatim, parameter names included — only the signature's line break is
        // ours, to fit the panel. A `kind: 'code'` slide shows real source.
        lines: [
          'public void AddBatch(long spins, long wageredMillicents,',
          '                     long returnedMillicents, long hits)',
          '{',
          '    Interlocked.Add(ref _spins, spins);',
          '    Interlocked.Add(ref _wageredMillicents, wageredMillicents);',
          '    Interlocked.Add(ref _returnedMillicents, returnedMillicents);',
          '    Interlocked.Add(ref _hits, hits);',
          '}',
        ],
      },
      {
        kind: 'stat',
        heading: 'Batching buys back the channel',
        stats: [
          { value: '4,096', label: 'spins per batch' },
          { value: '4', label: 'atomic adds that publish it' },
          { value: '4,096×', label: 'less traffic on the shared channel' },
        ],
        note: 'The integer sum is identical either way. Article 6.',
      },
      {
        kind: 'points',
        heading: 'The snapshot wrinkle',
        points: [
          'Four counters are individually atomic, not atomic as a set',
          'A mid-run read can pair one batch’s wagered with another’s returned',
          'It affects the live display only',
          'The final snapshot is taken after Task.WhenAll, on a quiesced engine',
        ],
      },
      {
        kind: 'points',
        heading: 'What a game supplies',
        points: [
          'SpinPlay turns a ref SpinRng into a SpinOutcome',
          'SpinPlayFactory builds one per worker, with its own scratch buffers',
          'A delegate names the single call; an interface would imply more',
          'Reel strips are built once, copied at construction, shared read-only',
        ],
      },
    ],
  },
  {
    id: '07',
    number: 7,
    title: 'Games as Data',
    kicker: 'JSON in, validated game out',
    thesis:
      'Move the game out of code and into a validated JSON file, and new rules arrive as compiled pay categories rather than a growing set of game-specific flags in the evaluator.',
    article: 'docs/articles/07-games-as-data.md',
    slides: [
      {
        kind: 'points',
        heading: 'Parsing is not validation',
        points: [
          'Braces and commas can be perfect while the game is still invalid',
          '“Reel 1 declares 22 stops but contains 21”',
          '“Payline ‘Center’ refers to unknown symbol ‘Whale’”',
          'Reporting both lets the author fix the file in one pass',
        ],
      },
      {
        kind: 'anim',
        heading: 'File to running game',
        caption:
          'orca-dive.json is validated whole, compiled into neutral pay categories, then read by one evaluator and one analyzer.',
      },
      {
        kind: 'points',
        heading: 'Pay categories replace special cases',
        points: [
          'Two flat bool arrays: Continues and IsRequired, one index each',
          'Wild continues fish runs; it never satisfies the Mackerel category',
          'AnySeven is a group win from the same two arrays, no new code path',
          'Best pay wins, ties go to the longer run',
        ],
      },
      {
        kind: 'code',
        heading: 'One walk, two lookups per cell',
        source: 'CSharp/src/MMP.SlotGame.Core/Games/WinEvaluator.cs',
        lines: [
          'var best = LineWin.None;',
          'foreach (var category in _categories)',
          '{',
          '    var run = 0; var satisfied = false;',
          '    while (run < cells.Length',
          '        && category.Continues(cells[run]))',
          '    {',
          '        satisfied |= category.IsRequired(cells[run]);',
          '        run++;',
          '    }',
          '    if (!satisfied) continue;',
        ],
      },
      {
        kind: 'stat',
        heading: 'The bonus, simulated pick by pick',
        stats: [
          { value: '24 / 6', label: 'prizes and blanks in the pool' },
          { value: '1/7', label: 'chance any one prize is collected' },
        ],
        note: 'Drawn without replacement, pick until a blank. A prize is collected when it precedes every blank — 1/(b+1). Article 7.',
      },
      {
        kind: 'stat',
        heading: 'What the deconstruction reproduced',
        stats: [
          { value: '10.26%', label: 'line hit frequency — line wins only' },
          { value: '11.45%', label: 'any-award union, counting bonus triggers' },
          // 32, per docs/par-orca-dive.md ("all 32 integer combination counts
          // reproduce exactly") and OrcaDiveParSheetTests.Published, which holds
          // 32 entries. Article 07 says 31 and is the odd one out — reported.
          { value: '32', label: 'integer line-win combination counts matched' },
        ],
        note: 'Over a 10,000,000-spin statistical suite. Article 7.',
      },
    ],
  },
  {
    id: '08',
    number: 8,
    title: 'Proving the Machine',
    kicker: 'A referee with no shared code',
    thesis:
      'A simulator that verifies itself is a circular argument, so the suite brings in a referee that shares data with both the analytic and simulated paths and code with neither.',
    article: 'docs/articles/08-proving-the-machine.md',
    slides: [
      {
        kind: 'anim',
        heading: 'Three paths, one number',
        caption:
          'Closed form, sampled simulation, and an exhaustively enumerated referee. The referee shares only data with the other two.',
      },
      {
        kind: 'stat',
        heading: 'The anchor: enumerate everything',
        stats: [
          { value: '22³ = 10,648', label: 'equally likely Classic3 outcomes' },
        ],
        note: 'The test writes its own window builder and its own evaluation loop. That duplication is what gives the comparison its value. Total pay / total wager is the RTP, not an estimate of it. Article 8.',
      },
      {
        kind: 'stat',
        heading: 'Overflow headroom, itemized',
        stats: [
          { value: '9.22 × 10¹⁸', label: 'signed 64-bit ceiling' },
          { value: '1.0 × 10¹²', label: 'millicents wagered in a 10M-spin soak' },
          { value: '~107 trillion', label: 'spins to reach the ceiling at 86.1% return' },
        ],
        note: 'Article 8.',
      },
      {
        kind: 'points',
        heading: 'Squaring is where headroom disappears',
        points: [
          'A 5000X jackpot is 500,000,000 mc; its square is 2.5 × 10¹⁷',
          'Thirty-seven such squares wrap an unchecked long',
          'GameAnalyzer squares the multiplier, converting units once at the end',
          'Change the ruler, not the field',
        ],
      },
      {
        kind: 'code',
        heading: 'Determinism you can assert with ==',
        source: 'CSharp/tests/MMP.SlotGame.Tests/ConcurrencyTests.cs',
        lines: [
          '[Theory]',
          '[InlineData(2)] [InlineData(4)] [InlineData(8)]',
          'public async Task ParallelRun_EqualsSequentialReplication_BitForBit(',
          '    int workers)',
          '{',
          '    // Replay each worker quota sequentially on the same streams…',
          '    Assert.Equal(sequential.ReturnedMillicents,',
          '                 parallel.ReturnedMillicents);',
          '}',
        ],
      },
      {
        kind: 'points',
        heading: 'Tier by cost, gate by category',
        points: [
          'Fast tests by default; slow and stress work is opt-in',
          'SLOTGAME_SLOW_TESTS=1 selects the long-running classes',
          'Category=Slow and Category=Stress traits carry the gate',
          'CI policy decides which tiers block a merge',
        ],
      },
    ],
  },
  {
    id: '09',
    number: 9,
    title: 'Optimize the Machine',
    kicker: 'Checksums before speedups',
    thesis:
      'Optimize only the machine you already proved: the lab keeps the original DrawWindow beside the production version, runs both from one seed, and refuses to report a speedup unless the checksums match.',
    article: 'docs/articles/09-optimization.md',
    slides: [
      {
        kind: 'points',
        heading: 'Start with a Release baseline',
        points: [
          'Five samples inside one process, sorted, median reported',
          'The first sample carries tiered-JIT and dynamic-PGO warmup',
          'One process launch is a poor benchmark',
          'These numbers are regression markers for one machine',
        ],
      },
      {
        kind: 'anim',
        heading: 'Remove the remainder, extend the strip',
        caption:
          'Append Rows − 1 wrapped entries at construction, and a window that used to compute a remainder per cell becomes a contiguous read.',
      },
      {
        kind: 'stat',
        heading: 'What the remainder was costing',
        stats: [
          { value: '150,000,000', label: 'visible cells over a 10M-spin, 5×3 run' },
          { value: '43.5M → 75.5M', label: 'spins/sec, before and after' },
        ],
        note: 'Two to four extra entries per reel, whether the strip has 22 stops or 128. Article 9.',
      },
      {
        kind: 'points',
        heading: 'Give the worker the representation it uses',
        points: [
          'The UI needs names and flags; the evaluator needs IDs',
          'DrawWindow for symbols, DrawWindowIds for bytes',
          'One byte window per worker, overwritten every spin',
          'An equivalence test starts both from the same RNG state',
        ],
      },
      {
        kind: 'stat',
        heading: 'The dictionary lost; narrowing won',
        stats: [
          { value: '16.07M', label: 'window + rules, outcomes/sec' },
          { value: '2.14M', label: 'packed-key dictionary — 0.133×' },
          { value: '20.53M', label: 'progressive arrays — 1.277×' },
        ],
        note: 'A 1.5M-entry dictionary sent the CPU to scattered memory. Flat transition arrays narrow one reel at a time. Article 9.',
      },
      {
        kind: 'points',
        heading: 'The failed experiments belong in the lesson',
        points: [
          'Unrolled per-height methods fell to about 72M spins/sec',
          'One flattened drawing array gave inconsistent 71M–76M medians',
          'Forced AggressiveInlining reduced medians to about 71M–72M',
          'The JIT made better inlining decisions than the manual hints',
        ],
      },
    ],
  },
];

export const SERIES = {
  line1: 'Programming Gems',
  line2: 'Slot Games',
} as const;

export function chapterById(id: string): Chapter {
  const found = CHAPTERS.find((c) => c.id === id);
  if (!found) throw new Error(`No chapter with id "${id}"`);
  return found;
}
