import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt } from '../components/motion';
import { AnimStage, Cell, Edge, Label } from './primitives';

/**
 * Chapter 5 — twenty-four physical outcomes, eight weighted combinations.
 *
 * The teaching game from article 5: reels of 3, 2 and 4 stops. Reel 1 holds
 * Cherry twice and Bell once, reel 2 holds one of each, reel 3 holds Cherry
 * three times and Bell once. Descend fills one box per reel, multiplying the
 * running weight by the count as it goes, so eight leaves stand in for all
 * 3 × 2 × 4 = 24 physical combinations — and the eight weights still total 24.
 */

/** Stop counts per reel, from article 5's table. */
const COUNTS: Record<'C' | 'B', number>[] = [
  { C: 2, B: 1 }, // reel 1: Cherry, Cherry, Bell
  { C: 1, B: 1 }, // reel 2: Cherry, Bell
  { C: 3, B: 1 }, // reel 3: Cherry, Cherry, Cherry, Bell
];

const LEAVES = buildLeaves();

export const Ch05WeightTree: React.FC = () => {
  const frame = useCurrentFrame();

  const stripsIn = progressAt(frame, 6, 20, 'out');
  const level = (i: number) => progressAt(frame, 60 + i * 55, 30, 'out');
  const weightsIn = progressAt(frame, 250, 30, 'out');
  const sumIn = progressAt(frame, 320, 30, 'out');
  const verdict = progressAt(frame, 380, 30, 'out');

  return (
    <AnimStage viewBox="0 0 900 420">
      {/* The three reels as literal stop lists. */}
      <g opacity={stripsIn}>
        {[
          ['C', 'C', 'B'],
          ['C', 'B'],
          ['C', 'C', 'C', 'B'],
        ].map((strip, reel) => (
          <g key={reel}>
            <Label x={22} y={38 + reel * 42} size={11} anchor="start" color={colors.textMuted}>
              {`reel ${reel + 1}`}
            </Label>
            {strip.map((s, i) => (
              <Cell key={i} x={74 + i * 26} y={26 + reel * 42} size={24} label={s} />
            ))}
            <Label x={200} y={38 + reel * 42} size={11} anchor="start" color={colors.textSecondary}>
              {`C × ${COUNTS[reel]!.C}   B × ${COUNTS[reel]!.B}`}
            </Label>
          </g>
        ))}
        <Label x={22} y={160} size={12} anchor="start" color={colors.brassBright}>
          3 × 2 × 4 = 24 physical outcomes
        </Label>
      </g>

      {/* The descent. One box per reel, branching only on distinct symbols. */}
      <Label x={470} y={20} size={12} color={colors.textSecondary}>
        Descend — one box per reel, weight multiplies on the way down
      </Label>

      {LEAVES.map((leaf, i) => {
        const y = 44 + i * 44;
        const r = level(2);
        return (
          <g key={leaf.path}>
            {leaf.path.split('').map((s, reel) => (
              <g key={reel} opacity={level(reel)}>
                <Cell x={360 + reel * 40} y={y} size={30} label={s} state={s === 'C' ? 'lit' : 'plain'} />
              </g>
            ))}
            <Label x={506} y={y + 15} size={12} anchor="start" color={colors.textMuted} reveal={weightsIn}>
              {`${COUNTS[0]![leaf.symbols[0]!]} × ${COUNTS[1]![leaf.symbols[1]!]} × ${COUNTS[2]![leaf.symbols[2]!]}  =`}
            </Label>
            <Label x={640} y={y + 15} size={15} anchor="start" color={colors.brassBright} reveal={weightsIn}>
              {String(leaf.weight)}
            </Label>
            <Edge
              d={`M 350 ${y + 15} L 358 ${y + 15}`}
              length={10}
              reveal={r}
              color={colors.brassDim}
              width={1}
            />
          </g>
        );
      })}

      {/* The branch trunk. */}
      <Edge d="M 296 210 L 348 210" length={54} reveal={level(0)} />
      <Label x={252} y={210} size={11} color={colors.textMuted} reveal={level(0)}>
        8 combinations
      </Label>

      <g opacity={sumIn}>
        <line x1={630} y1={396} x2={676} y2={396} stroke={colors.brass} strokeWidth={1.5} />
        <Label x={640} y={410} size={15} anchor="start" color={colors.brassBright}>
          24
        </Label>
        <Label x={690} y={410} size={11} anchor="start" color={colors.textSecondary}>
          the weights add back
        </Label>
      </g>

      <Label x={22} y={330} size={13} anchor="start" color={colors.brassBright} reveal={verdict}>
        Eight evaluations, not twenty-four.
      </Label>
      <Label x={22} y={354} size={12} anchor="start" color={colors.textMuted} reveal={verdict}>
        Nothing estimated. Nothing thrown away.
      </Label>
      <Label x={22} y={378} size={12} anchor="start" color={colors.textMuted} reveal={verdict}>
        Repeated stops add weight, never branches.
      </Label>
    </AnimStage>
  );
};

type Leaf = { path: string; symbols: Array<'C' | 'B'>; weight: number };

/** All eight symbol combinations, in descent order, with their weights. */
function buildLeaves(): Leaf[] {
  const options: Array<'C' | 'B'> = ['C', 'B'];
  const leaves: Leaf[] = [];
  for (const a of options)
    for (const b of options)
      for (const c of options)
        leaves.push({
          path: `${a}${b}${c}`,
          symbols: [a, b, c],
          weight: COUNTS[0]![a] * COUNTS[1]![b] * COUNTS[2]![c],
        });
  return leaves;
}
