import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt } from '../components/motion';
import { AnimStage, Cell, Edge, Label, Node } from './primitives';

/**
 * Chapter 7 — a validated file becomes a running game.
 *
 * The loader validates the whole file and reports every problem at once, then
 * compiles the rules into neutral pay categories: two flat boolean arrays,
 * `Continues` and `IsRequired`, one index per symbol. The evaluator walks a
 * line with two lookups per cell and no game-specific branches.
 *
 * The walked line demonstrates the wild rule article 7 states: WildOrca
 * continues a Mackerel run but never satisfies the Mackerel category, so the
 * run needs a real Mackerel in it to pay.
 */
const LINE = ['Mackerel', 'WildOrca', 'Mackerel', 'Squid', 'Salmon'];

/** The Mackerel pay category, as article 7 describes it. */
const CONTINUES = new Set(['Mackerel', 'WildOrca']);
const REQUIRED = new Set(['Mackerel']);

export const Ch07GameAsData: React.FC = () => {
  const frame = useCurrentFrame();

  const stage = (i: number) => progressAt(frame, 8 + i * 30, 22, 'out');
  const errorsIn = progressAt(frame, 120, 26, 'out');
  const lineIn = progressAt(frame, 200, 24, 'out');
  // The walk steps one cell at a time and stops on the first mismatch.
  const walk = progressAt(frame, 240, 120, 'inOut');
  const cursor = Math.min(LINE.length, Math.floor(walk * (LINE.length + 1)));
  const verdict = progressAt(frame, 390, 30, 'out');

  const run = runLength();

  return (
    <AnimStage viewBox="0 0 900 420">
      {/* The pipeline. */}
      <Node x={16} y={30} w={150} h={58} label="orca-dive.json" sub="26/29/26/29/26" reveal={stage(0)} fontSize={13} />
      <Edge d="M 166 59 L 212 59" length={50} reveal={stage(1)} />
      <Node x={214} y={30} w={150} h={58} label="loader" sub="validate whole file" reveal={stage(1)} fontSize={13} />
      <Edge d="M 364 59 L 410 59" length={50} reveal={stage(2)} />
      <Node x={412} y={30} w={160} h={58} label="pay categories" sub="two bool[]" reveal={stage(2)} fontSize={13} fill={colors.surfaceRaised} />
      <Edge d="M 572 59 L 618 59" length={50} reveal={stage(3)} />
      <Node x={620} y={30} w={150} h={58} label="WinEvaluator" sub="no game branches" reveal={stage(3)} fontSize={13} />

      {/* Both problems reported at once. */}
      <g opacity={errorsIn}>
        <rect x={214} y={108} width={430} height={62} fill={colors.surface} stroke={colors.signalEmber} strokeWidth={1} />
        <Label x={230} y={128} size={11} anchor="start" color={colors.signalEmber}>
          Reel 1 declares 22 stops but contains 21
        </Label>
        <Label x={230} y={150} size={11} anchor="start" color={colors.signalEmber}>
          Payline &apos;Center&apos; refers to unknown symbol &apos;Whale&apos;
        </Label>
        <Label x={660} y={140} size={11} anchor="start" color={colors.textMuted}>
          both, in one pass
        </Label>
      </g>

      {/* The category walk. */}
      <Label x={24} y={214} size={12} anchor="start" color={colors.textSecondary}>
        Mackerel category — Continues: Mackerel, WildOrca · IsRequired: Mackerel
      </Label>

      {LINE.map((symbol, i) => {
        const reached = cursor > i;
        const continues = CONTINUES.has(symbol);
        const inRun = reached && i < run;
        return (
          <g key={i} opacity={lineIn}>
            <Cell
              x={24 + i * 96}
              y={234}
              size={72}
              label={symbol.slice(0, 4)}
              state={inRun ? 'lit' : reached ? 'ghost' : 'plain'}
            />
            <Label
              x={60 + i * 96}
              y={324}
              size={11}
              color={reached ? (continues ? colors.signalGreen : colors.signalEmber) : colors.textMuted}
            >
              {reached ? (continues ? 'continues' : 'stops') : 'reel ' + (i + 1)}
            </Label>
            <Label
              x={60 + i * 96}
              y={342}
              size={11}
              color={reached && REQUIRED.has(symbol) ? colors.brassBright : colors.textMuted}
            >
              {reached ? (REQUIRED.has(symbol) ? 'required ✓' : '—') : ''}
            </Label>
          </g>
        );
      })}

      <g opacity={verdict}>
        <Label x={520} y={252} size={13} anchor="start" color={colors.brassBright}>
          {`run of ${run}`}
        </Label>
        <Label x={520} y={276} size={12} anchor="start" color={colors.textSecondary}>
          the wild continued it
        </Label>
        <Label x={520} y={296} size={12} anchor="start" color={colors.textSecondary}>
          a real Mackerel satisfied it
        </Label>
        <Label x={24} y={382} size={13} anchor="start" color={colors.brassBright}>
          Two array lookups per cell. A new rule is a new category, not a new branch.
        </Label>
        <Label x={24} y={406} size={12} anchor="start" color={colors.textMuted}>
          Best pay wins; ties go to the longer run.
        </Label>
      </g>
    </AnimStage>
  );
};

/** Leading run under the Mackerel category — the same walk the evaluator does. */
function runLength(): number {
  let run = 0;
  while (run < LINE.length && CONTINUES.has(LINE[run]!)) run++;
  return run;
}
