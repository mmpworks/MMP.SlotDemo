#!/usr/bin/env python3
"""Which counting rule produces the sheet's Hit Chance of 0.41552?

Every candidate below is enumerated over the real reel strips. No sampling.
Spoiler: none of them lands on 0.41552, and one bound explains why the usual
suspects cannot: every payline is a path picking one row per reel, so the
union of ALL left-to-right path evaluations is the ways union, 0.40162
[Muir ch3: ways games modelled as scattered 1-line games]. A number above
that cannot come from any left-to-right line or ways count on these strips,
however many lines the evaluator runs.

Round 2 widens the hunt beyond path schemes:
  - window-walk bugs on the sheet author's side (a reel is a drum with N
    stops; a flat walk sees N-2 windows and shifts every count),
  - window-height slips (4 rows instead of 3),
  - hit TALLIES (expected count of winning symbol runs, not bounded by the
    union),
  - folding feature spins into the denominator (hit chance over all spins).
Best candidates: flat walk 0.41050 (-0.00502); flat walk folded over all
spins 0.41472 (-0.00080). Neither lands on the cell. A simulation of ~100k spins has
a standard deviation near 0.0016 on this probability [Hannum p.6, volatility
guidelines], so a sim-measured cell is consistent with the closest models.

References
----------
[Muir]   Robert Muir, "Elements of Slot Design", 3rd ed. (ch3 by Mark
         Sinosich). Cited by chapter only: the house library carries a
         chapter digest, not the paged book, so Muir page numbers would be
         unverifiable and are deliberately absent.
[Hannum] Robert C. Hannum, "A Guide to Casino Mathematics", UNLV Gaming
         Studies Research Center, 12 pp. Page numbers cite the PDF at
         MMP.Library/books/other/casino-mathematics-hannum/raw/casino_math.pdf.

Usage:
    python3 hit_chance_hypotheses.py ["path/to/Service Test Math.xls"] [--json out.json]
"""
import json
import sys
from collections import Counter
from pathlib import Path

from spinplay_analyzer import DEFAULT_XLS, SCATTER, WILD, line_pay, read_sheet

TARGET = 0.41552
ROWS = 3


# ------------------------------------------------------------------ windows

def windows(strip, mode, rows=ROWS):
    """The window list a walker sees, per walking discipline.

    'wrap' is the physical truth: the reel is a drum, N stops, the last
    windows wrap past the end back to the top. The other three modes are
    plausible walker bugs, tested because the sheet's Hit Chance exceeds
    the wrap-true ways union:
      'flat'  - stop at the end: N-2 windows, the two wrap windows dropped.
      'pad1'  - strip padded with its first symbol once: N-1 windows.
      'clamp' - indexes clamped at the end: N windows, last symbols doubled.
    """
    n = len(strip)
    if mode == "wrap":
        return [[strip[(i + r) % n] for r in range(rows)] for i in range(n)]
    if mode == "flat":
        return [[strip[i + r] for r in range(rows)] for i in range(n - rows + 1)]
    if mode == "pad1":
        p = strip + strip[:1]
        return [p[i:i + rows] for i in range(n - rows + 2)]
    if mode == "clamp":
        return [[strip[min(i + r, n - 1)] for r in range(rows)] for i in range(n)]
    raise ValueError(mode)


# ------------------------------------------------------------- ways engine

def make_classes(reels, pay_bits, all_mask, mode="wrap", rows=ROWS,
                 wild_subs=True, wild_as_scatter=False, scatter_extends=False):
    """Per reel, windows grouped by (alive-mask, scatter count, real-mask,
    wild-seen). Grouping by class keeps the 5-reel product enumerable."""
    out = []
    for strip in reels:
        c = Counter()
        for col in windows(strip, mode, rows):
            real = 0
            for x in col:
                real |= pay_bits.get(x, 0)
            mask = real
            if wild_subs and WILD in col:
                mask = all_mask
            if scatter_extends and SCATTER in col:
                mask = all_mask
            scat = col.count(SCATTER) + (col.count(WILD) if wild_as_scatter else 0)
            c[(mask, scat, real, WILD in col)] += 1
        out.append(list(c.items()))
    return out


def enumerate_hits(classes, hit_fn):
    """P(hit) over all stop combinations, by weighted classes. No sampling.
    Full enumeration carries no sampling error; a simulation only converges
    here as spins grow [Hannum p.5, law of large numbers]."""
    total = 1
    for c in classes:
        total *= sum(w for _, w in c)
    hits = 0
    c1, c2, c3, c4, c5 = classes
    for (m1, k1, r1, wl1), w1 in c1:
        for (m2, k2, r2, wl2), w2 in c2:
            w12 = w1 * w2
            for (m3, k3, r3, wl3), w3 in c3:
                w123 = w12 * w3
                k123 = k1 + k2 + k3
                for (m4, k4, r4, wl4), w4 in c4:
                    w1234 = w123 * w4
                    for (m5, k5, r5, wl5), w5 in c5:
                        if hit_fn((m1, m2, m3, m4, m5), k123 + k4 + k5,
                                  (r1, r2, r3, r4, r5),
                                  wl1 or wl2 or wl3 or wl4 or wl5):
                            hits += w1234 * w5
    return hits / total


# ------------------------------------------------------- tally (not union)

def expected_symbol_runs(reels, symbols, mode="wrap", rows=ROWS):
    """E[number of distinct symbols with a 3+ ways run], by linearity of
    expectation. A TALLY, not a probability: coinciding wins count once per
    symbol, so this may exceed the ways union. Tested because the sheet's
    value exceeds the union; Muir separates the two views — theory counts
    rule by rule, the player experiences coinciding wins [Muir ch7]."""
    total = 0.0
    for s in symbols:
        p = 1.0
        for strip in reels[:3]:
            cols = windows(strip, mode, rows)
            p *= sum(1 for col in cols
                     if any(c == s or c == WILD for c in col)) / len(cols)
        total += p
    return total


# ------------------------------------------------------------- line model

def line_hit_chance(reels, pays, scatter_pays, mode="wrap"):
    """P(win on the single middle-row line OR 3+ window scatters). This is
    what the programming notes describe ('single line only'); it lands near
    0.03, an order of magnitude under the sheet's cell. Kept in the catalog
    to show the sheet contradicts its own Lines=1 note."""
    # 'wild pays highest', same as the analyzer: a pure-wild run pays as the
    # best symbol it can stand for [prioritisation, Muir ch2]
    pays = dict(pays)
    pays[WILD] = {oak: max(p.get(oak, 0) for p in pays.values())
                  for oak in range(1, 6)}
    order = [s for s in pays if s != WILD] + [WILD]
    per = []
    for strip in reels:
        c = Counter()
        for col in windows(strip, mode):
            c[(col[1], col.count(SCATTER))] += 1
        per.append(list(c.items()))
    total = 1
    for c in per:
        total *= sum(w for _, w in c)
    cache = {}
    hits = 0
    from itertools import product
    for combo in product(*per):
        w = 1
        scat = 0
        cells = []
        for (mid, k), cnt in combo:
            w *= cnt
            scat += k
            cells.append(mid)
        key = tuple(cells)
        lp = cache.get(key)
        if lp is None:
            lp = cache[key] = line_pay(cells, pays, order)
        sp = scatter_pays.get(min(scat, 5), 0) if scat >= 3 else 0
        if lp + sp:
            hits += w
    return hits / total


# ----------------------------------------------------------------- report

def main():
    argv = list(sys.argv[1:])
    json_out = None
    if "--json" in argv:
        i = argv.index("--json")
        json_out = Path(argv[i + 1])
        del argv[i:i + 2]
    xls = Path(argv[0]) if argv else DEFAULT_XLS

    pays, scatter_pays, base_reels, feature_reels, _ = read_sheet(xls)
    pay_bits = {s: 1 << i for i, s in enumerate(pays)}
    all_mask = (1 << len(pays)) - 1
    symbols = [s for s in pays if s != WILD]

    def cls(reels, **kw):
        return make_classes(reels, pay_bits, all_mask, **kw)

    # hit rules: masks = per-reel alive sets, k = scatter tally,
    # reals = per-reel real-symbol sets, wild = any wild visible
    def left(masks, k, reals, wild):
        return bool(masks[0] & masks[1] & masks[2]) or k >= 3

    def runs_only(masks, k, reals, wild):
        return bool(masks[0] & masks[1] & masks[2])

    def left_scat2(masks, k, reals, wild):
        return bool(masks[0] & masks[1] & masks[2]) or k >= 2

    def left_scat2_wild(masks, k, reals, wild):
        return bool(masks[0] & masks[1] & masks[2]) or k >= 3 or (k == 2 and wild)

    def left_seen(masks, k, reals, wild):
        alive = masks[0] & masks[1] & masks[2] & (reals[0] | reals[1] | reals[2])
        return bool(alive) or k >= 3

    def anywhere(masks, k, reals, wild):
        m1, m2, m3, m4, m5 = masks
        return bool((m1 & m2 & m3) or (m2 & m3 & m4) or (m3 & m4 & m5)) or k >= 3

    def both_ends(masks, k, reals, wild):
        m1, m2, m3, m4, m5 = masks
        return bool((m1 & m2 & m3) or (m3 & m4 & m5)) or k >= 3

    def scatters_only(masks, k, reals, wild):
        return k >= 3

    # Round 1: left-to-right path schemes on the wrap-true windows. All are
    # bounded by the ways union; the bound is why round 2 exists.
    round1 = [
        ("ways L2R | 3+ scatters  [wrap-true union]", cls(base_reels), left),
        ("ways, run needs a real symbol", cls(base_reels), left_seen),
        ("ways | 2+ scatters count as a hit", cls(base_reels), left_scat2),
        ("ways | wilds also count as scatters",
         cls(base_reels, wild_as_scatter=True), left),
        ("ways, wilds do NOT substitute",
         cls(base_reels, wild_subs=False), left),
        ("ways, scatter extends runs like a wild",
         cls(base_reels, scatter_extends=True), left),
        ("runs may start on any reel", cls(base_reels), anywhere),
        ("runs pay both directions (left and right)", cls(base_reels), both_ends),
        ("ways on the FEATURE reels", cls(feature_reels), left),
    ]

    # Round 2a: window-walk bugs (the drum rule broken four ways) and a
    # window-height slip. The reel has N stops; every non-wrap mode changes
    # both the numerator and the denominator.
    round2_walk = [
        ("ways, FLAT walk (wrap windows dropped)",
         cls(base_reels, mode="flat"), left),
        ("ways, PAD1 walk (first symbol appended)",
         cls(base_reels, mode="pad1"), left),
        ("ways, CLAMP walk (end symbols doubled)",
         cls(base_reels, mode="clamp"), left),
        ("ways, 4-row window", cls(base_reels, rows=4), left),
        ("ways runs only, no scatter rule (wrap)", cls(base_reels), runs_only),
        ("ways runs only, no scatter rule (flat)",
         cls(base_reels, mode="flat"), runs_only),
        ("ways FLAT | 2 scatters + a wild visible",
         cls(base_reels, mode="flat"), left_scat2_wild),
        ("ways wrap | 2 scatters + a wild visible",
         cls(base_reels), left_scat2_wild),
        ("ways FLAT on the FEATURE reels",
         cls(feature_reels, mode="flat"), left),
        ("3+ scatters alone (feature trigger, wrap)",
         cls(base_reels), scatters_only),
    ]

    results = []
    for name, classes, fn in round1:
        results.append((name, enumerate_hits(classes, fn), "round 1: path schemes"))
    for name, classes, fn in round2_walk:
        results.append((name, enumerate_hits(classes, fn), "round 2: walk/window"))

    # Line family: the sheet's own Lines=1 claim, four ways. All land near
    # 0.03-0.04, an order of magnitude under the cell.
    for name, reels, mode in (
            ("single line, base reels (wrap)", base_reels, "wrap"),
            ("single line, base reels (flat)", base_reels, "flat"),
            ("single line, FEATURE reels (wrap)", feature_reels, "wrap"),
            ("single line, FEATURE reels (flat)", feature_reels, "flat")):
        results.append((name, line_hit_chance(reels, pays, scatter_pays, mode),
                        "line family"))

    # Round 2b: tallies (expected counts, not probabilities).
    results.append(("E[distinct symbol runs] tally (wrap)",
                    expected_symbol_runs(base_reels, symbols), "round 2: tally"))

    # Round 2c: fold feature spins into the tally. Expected feature spins
    # per base spin come from the trigger weights and the retrigger series
    # E = n1 / (1 - n2 p2) [Muir ch2, free games as a geometric series].
    by_name = {n: v for n, v, _ in results}
    base_wrap = by_name["ways L2R | 3+ scatters  [wrap-true union]"]
    base_flat = by_name["ways, FLAT walk (wrap windows dropped)"]
    feat_wrap = by_name["ways on the FEATURE reels"]
    feat_flat = by_name["ways FLAT on the FEATURE reels"]

    trig = cls(base_reels)
    total = 1
    for c in trig:
        total *= sum(w for _, w in c)
    scat_w = Counter()
    c1, c2, c3, c4, c5 = trig
    for (m1, k1, r1, _), w1 in c1:
        for (m2, k2, r2, _), w2 in c2:
            for (m3, k3, r3, _), w3 in c3:
                for (m4, k4, r4, _), w4 in c4:
                    for (m5, k5, r5, _), w5 in c5:
                        k = k1 + k2 + k3 + k4 + k5
                        if k >= 3:
                            scat_w[min(k, 5)] += w1 * w2 * w3 * w4 * w5
    # retrigger rate per free spin, from the feature strips (wrap-true)
    ftrig = cls(feature_reels)
    ftotal = 1
    for c in ftrig:
        ftotal *= sum(w for _, w in c)
    spins_added = 0
    fc1, fc2, fc3, fc4, fc5 = ftrig
    for (m1, k1, r1, _), w1 in fc1:
        for (m2, k2, r2, _), w2 in fc2:
            for (m3, k3, r3, _), w3 in fc3:
                for (m4, k4, r4, _), w4 in fc4:
                    for (m5, k5, r5, _), w5 in fc5:
                        k = k1 + k2 + k3 + k4 + k5
                        if k >= 3:
                            spins_added += {3: 10, 4: 15, 5: 20}[min(k, 5)] \
                                * w1 * w2 * w3 * w4 * w5
    added_per_spin = spins_added / ftotal
    fs = sum({3: 10, 4: 15, 5: 20}[k] / (1 - added_per_spin) * w
             for k, w in scat_w.items()) / total

    for name, b, f in (("hits folded over ALL spins (wrap)", base_wrap, feat_wrap),
                       ("hits folded over ALL spins (FLAT)", base_flat, feat_flat)):
        results.append((name, (b + fs * f) / (1 + fs), "round 2: fold-in"))
    # expected hits per BASE game (base hit + feature hits it spawns);
    # a tally again, so it may exceed the union
    results.append(("hits in free spins per BASE spin (wrap)",
                    base_wrap + fs * feat_wrap, "round 2: fold-in"))

    print(f"target: Hit Chance = {TARGET}   (Hit Rate 2.407 = 1/{TARGET})\n")
    print(f"{'candidate counting rule':46} {'value':>9} {'diff':>9}")
    print("-" * 68)
    for name, p, _fam in sorted(results, key=lambda t: abs(t[1] - TARGET)):
        tag = "  <-- MATCH" if round(p, 5) == TARGET else ""
        print(f"{name:46} {p:9.5f} {p - TARGET:+9.5f}{tag}")

    print("""
The bound that narrows the search: every payline is a path that picks one
row per reel, so the union of ALL left-to-right path evaluations equals the
ways union above (0.41552 exceeds it) [Muir ch3]. Whatever the sheet
counted, it includes something beyond left-to-right runs on these strips —
or it was measured, not derived: at ~100k simulated spins the standard
deviation of this probability is ~0.0016 [Hannum p.6], which covers the
gap from the closest models.""")

    if json_out:
        json_out.write_text(json.dumps(
            [{"name": n, "value": round(v, 5), "diff": round(v - TARGET, 5),
              "family": f} for n, v, f in results], indent=2))
        print(f"\nwrote {json_out}")


if __name__ == "__main__":
    main()
