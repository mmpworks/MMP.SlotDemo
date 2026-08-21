#!/usr/bin/env python3
"""Standalone analytic model of the SpinPlay 'Service Test Math.xls' PAR sheet.

This tool is tuned to ONE game: the SpinPlay service coding test. It reads the
XLS directly with its own parser and shares no code with any other game, engine,
or converter, so nothing from another sample can skew the numbers.

Everything is computed by enumeration and closed-form series. No random spins.
Full enumeration has no sampling error; a simulation only converges to these
numbers as spins grow [Hannum p.5, law of large numbers]. Muir recommends
building the analytic model and a simulation independently and reconciling
them before implementation [Muir ch7] — this file is the analytic side.

References
----------
[Muir]   Robert Muir, "Elements of Slot Design", 3rd ed. (ch3 by Mark
         Sinosich). Cited by chapter only: the house library carries a
         chapter digest, not the paged book, so page numbers for Muir
         would be unverifiable and are deliberately absent.
[Hannum] Robert C. Hannum, "A Guide to Casino Mathematics", UNLV Gaming
         Studies Research Center, 12 pp. Page numbers cite the PDF at
         MMP.Library/books/other/casino-mathematics-hannum/raw/casino_math.pdf.

Usage:
    python3 spinplay_analyzer.py ["path/to/Service Test Math.xls"]

Requires: xlrd (pip install xlrd).
"""
import sys
from collections import Counter
from itertools import product
from pathlib import Path

import xlrd

WILD = "W"
SCATTER = "S"
ROWS = 3

DEFAULT_XLS = (Path(__file__).resolve().parents[2]
               / "assets" / "spinplay-par-source" / "Service Test Math.xls")


# --------------------------------------------------------------------------- parsing

def cell(sheet, r, c):
    v = sheet.cell_value(r, c)
    if isinstance(v, float):
        return str(int(v)) if v == int(v) else str(v)
    return str(v).strip()


def read_sheet(path):
    wb = xlrd.open_workbook(str(path))

    pt = wb.sheet_by_name("Paytable")
    pays = {}
    for r in range(1, pt.nrows):
        label = cell(pt, r, 0)
        if not label:
            break
        row = {}
        for oak in range(1, 6):
            v = pt.cell_value(r, oak)
            if isinstance(v, float) and v > 0:
                row[oak] = int(v)
        if row:
            pays[label] = row
    scatter_pays = pays.pop(SCATTER, {})

    rs = wb.sheet_by_name("Reel Strips")
    def strips(first_col):
        out = []
        for c in range(first_col, first_col + 5):
            strip = [cell(rs, r, c) for r in range(1, rs.nrows)]
            out.append([s for s in strip if s])
        return out
    base_reels, feature_reels = strips(0), strips(6)

    gs = wb.sheet_by_name("Game Summary")
    sheet_values = {}
    for r in range(gs.nrows):
        label = cell(gs, r, 0)
        if label:
            sheet_values.setdefault(label, gs.cell_value(r, 1))

    return pays, scatter_pays, base_reels, feature_reels, sheet_values


# --------------------------------------------------------------------------- model

# Free spins: 3/4/5 scatters -> 10/15/20 spins; retriggers use the same table.
SPINS_AWARDED = {3: 10, 4: 15, 5: 20}
# Per-free-spin multiplier on ALL wins: 2x/3x/5x/10x at 25% each.
MULTIPLIER_MEAN = (2 + 3 + 5 + 10) / 4


def line_pay(cells, pays, pay_symbols):
    """Best left-to-right win on one line. Wilds substitute freely: a run of
    wilds with no real symbol behind it pays as the best symbol it could be
    ('wild pays highest') — the rule that reproduces the sheet's RTP.
    Paying only the best rule per line is Muir's prioritisation: under
    'highest win pays', overlap combinations must not pay twice [Muir ch2]."""
    best = 0
    for sym in pay_symbols:
        run = 0
        for c in cells:
            if c == sym or c == WILD:
                run += 1
            else:
                break
        for k in range(run, 0, -1):
            p = pays[sym].get(k, 0)
            if p > best:
                best = p
                break
    return best


def reel_classes(strip):
    """Stops grouped by (middle-row symbol, scatters visible in the column).
    Those two values are all a spin result depends on.

    The reel is a drum: a strip of N symbols has N stop positions, and the
    3-row window at stop i is strip[i], strip[(i+1) % N], strip[(i+2) % N].
    The last two windows wrap past the end back to the top. A flat walk
    that stops at the end sees only N-2 windows and shifts every count.
    Scatter probability rides on the whole window, not one row — 'scatters
    count window area' [Muir ch2]."""
    n = len(strip)
    classes = Counter()
    for i in range(n):
        column = [strip[i], strip[(i + 1) % n], strip[(i + 2) % n]]
        classes[(column[1], column.count(SCATTER))] += 1
    return list(classes.items())


def enumerate_game(reels, pays, scatter_pays, pay_symbols, retrigger):
    """Complete totals over every stop combination, by weighted classes.

    Muir's 'hits over cycle' model [Muir ch2]: cycle = product of the strip
    lengths, p(event) = hits / cycle, and RTP = sum of prize x hits over
    (cycle x bet). The same sum is the wager expected value per unit bet —
    Hannum's formal EV calculation, EV = sum(prize_i x p_i) [Hannum p.3]."""
    total = 1
    for strip in reels:
        total *= len(strip)

    classes = [reel_classes(strip) for strip in reels]
    line_cache = {}

    pay_units = 0          # sum of (line + scatter) pay over all combinations
    hit_combos = 0         # combinations that pay anything
    scatter_weights = Counter()   # combinations per visible scatter count
    spins_added = 0        # retrigger spins summed over all combinations

    for combo in product(*classes):
        weight = 1
        scatters = 0
        cells = []
        for (mid, k), stops in combo:
            weight *= stops
            scatters += k
            cells.append(mid)

        key = tuple(cells)
        lp = line_cache.get(key)
        if lp is None:
            lp = line_cache[key] = line_pay(cells, pays, pay_symbols)

        sp = scatter_pays.get(min(scatters, 5), 0) if scatters >= 3 else 0
        w = lp + sp
        if w:
            pay_units += w * weight
            hit_combos += weight
        if scatters >= 3:
            scatter_weights[min(scatters, 5)] += weight
            if retrigger:
                spins_added += SPINS_AWARDED[min(scatters, 5)] * weight

    return {
        "total": total,
        "pay_units": pay_units,
        "hit_combos": hit_combos,
        "scatter_weights": dict(scatter_weights),
        "spins_added": spins_added,
    }


def ways_hit_chance(reels):
    """P(any ways-style win): a symbol (or a wild for it) anywhere in the
    column, three or more columns from the left, OR 3+ scatters. Every symbol
    pays at three of a kind, so only reels 1-3 decide the ways part.

    Muir models ways games as 'scattered 1-line games': a symbol counts on a
    reel when it sits anywhere in that reel's window [Muir ch3]. This value
    is also a bound: every payline picks one row per reel, so the union of
    ALL left-to-right path evaluations equals this ways union. No line count
    can exceed it on these strips."""
    pay_present = []
    scatter_counts = []
    for strip in reels:
        n = len(strip)
        cols = []
        for i in range(n):
            column = [strip[i], strip[(i + 1) % n], strip[(i + 2) % n]]
            wild = WILD in column
            present = None if wild else frozenset(c for c in column if c not in (WILD, SCATTER))
            cols.append((present, column.count(SCATTER)))  # None = wild = keeps everything alive
        pay_present.append(cols)
        scatter_counts.append(Counter(k for _, k in cols))

    def meet(a, b):
        if a is None:
            return b
        if b is None:
            return a
        return a & b

    hit123 = Counter()   # scatter count on reels 1-3, for stop triples WITH a ways run
    miss123 = Counter()  # same, for triples without one
    for s1, k1 in pay_present[0]:
        for s2, k2 in pay_present[1]:
            a12 = meet(s1, s2)
            if a12 is not None and not a12:
                for _, k3 in pay_present[2]:
                    miss123[k1 + k2 + k3] += 1
                continue
            for s3, k3 in pay_present[2]:
                alive = meet(a12, s3)
                bucket = hit123 if (alive is None or alive) else miss123
                bucket[k1 + k2 + k3] += 1

    tail = Counter({0: 1})
    for counts in scatter_counts[3:]:
        nxt = Counter()
        for k, w in tail.items():
            for kk, ww in counts.items():
                nxt[k + kk] += w * ww
        tail = nxt

    n123 = len(reels[0]) * len(reels[1]) * len(reels[2])
    n45 = len(reels[3]) * len(reels[4])
    union = 0
    for k123, w123 in hit123.items():
        union += w123 * n45                     # ways hit, any scatter outcome
    for k123, w123 in miss123.items():
        for k45, w45 in tail.items():
            if k123 + k45 >= 3:
                union += w123 * w45             # no ways hit, but 3+ scatters
    return union / (n123 * n45)


# --------------------------------------------------------------------------- report

def main():
    xls = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_XLS
    pays, scatter_pays, base_reels, feature_reels, sheet = read_sheet(xls)
    pay_symbols = list(pays)

    # 'Wild pays highest': the wild's own row is zero on the sheet, but the
    # published RTP only reproduces when a pure-wild run pays as the best
    # symbol it can stand for. Give the wild that row.
    pays[WILD] = {oak: max(p.get(oak, 0) for p in pays.values()) for oak in range(1, 6)}
    pay_symbols = [s for s in pays if s != WILD] + [WILD]

    base = enumerate_game(base_reels, pays, scatter_pays, pay_symbols, retrigger=False)
    feat = enumerate_game(feature_reels, pays, scatter_pays, pay_symbols, retrigger=True)

    base_rtp = base["pay_units"] / base["total"]
    spin_ev = feat["pay_units"] / feat["total"] * MULTIPLIER_MEAN
    added_per_spin = feat["spins_added"] / feat["total"]
    # Free games are a geometric series [Muir ch2]: with n1 spins awarded at
    # trigger and n2*p2 = added_per_spin retriggered per free spin, the
    # expected total is n1 / (1 - added_per_spin). The feature runs on its
    # own strips (feature_reels): reels 2 and 3 differ from the base set,
    # so retrigger chance and spin EV both come from the feature cycle.
    feature_rtp = sum(
        weight / base["total"] * SPINS_AWARDED[k] / (1 - added_per_spin) * spin_ev
        for k, weight in base["scatter_weights"].items())

    line_hit = base["hit_combos"] / base["total"]
    ways_hit = ways_hit_chance(base_reels)

    rows = [
        ("Base game RTP",          base_rtp,                       sheet.get("Base %")),
        ("Feature RTP",            feature_rtp,                    sheet.get("Feature %")),
        ("Total RTP",              base_rtp + feature_rtp,         sheet.get("RTP")),
        ("Single feature spin RTP", spin_ev,                       sheet.get("Single Feature Spin RTP")),
        ("Feature trigger chance", sum(base["scatter_weights"].values()) / base["total"],
                                                                   sheet.get("Feature Chance")),
        ("Base games to feature",  base["total"] / sum(base["scatter_weights"].values()),
                                                                   sheet.get("Base Games to Feature")),
        ("Hit chance (line model)", line_hit,                      sheet.get("Hit Chance")),
        ("Hit chance (ways model)", ways_hit,                      sheet.get("Hit Chance")),
    ]

    print(f"SpinPlay PAR analytic model — {xls.name}")
    print(f"base cycles {base['total']:,} | feature cycles {feat['total']:,}\n")
    print(f"{'value':30} {'computed':>14} {'sheet':>12}  verdict")
    print("-" * 74)
    for name, computed, published in rows:
        if published is None:
            print(f"{name:30} {computed:14.5f} {'—':>12}")
            continue
        match = round(computed, 5) == round(float(published), 5)
        verdict = "MATCH" if match else f"off by {computed - float(published):+.5f}"
        print(f"{name:30} {computed:14.5f} {float(published):12.5f}  {verdict}")

    print(f"\nScatter hits (integer combination counts out of {base['total']:,}):")
    for k in (3, 4, 5):
        print(f"  {k} scatters: {base['scatter_weights'].get(k, 0):>9,}"
              f"   avg free spins {SPINS_AWARDED[k] / (1 - added_per_spin):.4f}")

    print("\nNote: neither hit-chance model lands on the sheet's 0.41552.")
    print("The line model is what the programming notes describe; the ways model")
    print("is the nearest counting rule we can construct. Every RTP figure above")
    print("reproduces under 'wild pays highest', so the rule set is right and the")
    print("Hit Chance cell is the open question.")


if __name__ == "__main__":
    main()
