import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt } from '../components/motion';
import { AnimStage, Cell, Label } from './primitives';
import { GREEN7_STOPS, REEL_ONE, SYMBOL_MARK } from '../data/orcaDive';

/**
 * Chapter 3 — a reel is a strip, and the window reads neighbors.
 *
 * The strip is Orca Dive's reel 1, all 26 stops in order, straight out of
 * `CSharp/games/orca-dive.json`. A three-cell window slides along it, and
 * because Green7 sits at stops 3, 16 and 24, the gaps between copies are 13,
 * 8 and 5 — every one of them larger than the window, so no single stop can
 * ever show two Green7s on this reel. That is a fact about neighbors on a
 * wire, and it is the fact a weighted-die model cannot produce.
 */
const CELL = 30;
const STRIP_X = 30;
const STRIP_Y = 150;
const WINDOW_ROWS = 3;

/**
 * The window as one or two contiguous runs over a strip laid out flat. A
 * window that crosses the end of the strip is genuinely two runs, and drawing
 * it that way is the illustration, not a compromise.
 */
function windowRuns(stop: number, n: number): Array<{ from: number; count: number }> {
  const head = Math.min(WINDOW_ROWS, n - stop);
  const runs = [{ from: stop, count: head }];
  if (head < WINDOW_ROWS) runs.push({ from: 0, count: WINDOW_ROWS - head });
  return runs;
}

export const Ch03StripWindow: React.FC = () => {
  const frame = useCurrentFrame();
  const n = REEL_ONE.length;

  const stripIn = (i: number) => progressAt(frame, 4 + i * 2, 12, 'out');
  const windowIn = progressAt(frame, 70, 20, 'out');
  // The window walks the strip, pausing on each Green7 copy in turn.
  const walk = progressAt(frame, 95, 200, 'inOut');
  const stop = Math.floor(walk * (n - 1));
  const gapsIn = progressAt(frame, 320, 26, 'out');
  const verdict = progressAt(frame, 370, 30, 'out');

  const visible = [stop, (stop + 1) % n, (stop + 2) % n];

  return (
    <AnimStage viewBox="0 0 900 420">
      <Label x={30} y={40} size={14} anchor="start" color={colors.textSecondary}>
        Orca Dive, reel 1 — 26 stops, in strip order
      </Label>

      {/* The strip, laid out flat. */}
      {REEL_ONE.map((symbol, i) => {
        const inWindow = visible.includes(i) && windowIn > 0.5;
        const isGreen7 = symbol === 'Green7';
        return (
          <g key={i} opacity={stripIn(i)}>
            <Cell
              x={STRIP_X + i * CELL}
              y={STRIP_Y}
              size={CELL}
              label={SYMBOL_MARK[symbol] ?? '?'}
              state={inWindow ? 'lit' : isGreen7 ? 'wrapped' : 'plain'}
            />
            <Label x={STRIP_X + i * CELL + CELL / 2} y={STRIP_Y + CELL + 14} size={9} color={colors.textMuted}>
              {String(i)}
            </Label>
          </g>
        );
      })}

      {/* The strip is cyclic: stop 25 is next to stop 0. */}
      <path
        d={`M ${STRIP_X + n * CELL} ${STRIP_Y + CELL / 2} C ${STRIP_X + n * CELL + 40} ${STRIP_Y + CELL / 2}, ${STRIP_X + n * CELL + 40} ${STRIP_Y - 60}, ${STRIP_X + n * CELL / 2} ${STRIP_Y - 60} L ${STRIP_X} ${STRIP_Y - 60} C ${STRIP_X - 22} ${STRIP_Y - 60}, ${STRIP_X - 22} ${STRIP_Y + CELL / 2}, ${STRIP_X} ${STRIP_Y + CELL / 2}`}
        fill="none"
        stroke={colors.brassDim}
        strokeWidth={1}
        strokeDasharray="4 4"
        opacity={stripIn(n - 1) * 0.8}
      />
      <Label x={450} y={STRIP_Y - 72} size={11} color={colors.textMuted} reveal={stripIn(n - 1)}>
        the strip is a loop — stop 25 is next to stop 0
      </Label>

      {/* The three-cell window. Near the end of the strip it wraps, so it is
          drawn as two boxes — which is the point being made, not a defect. */}
      <g opacity={windowIn}>
        {windowRuns(stop, n).map((run) => (
          <rect
            key={run.from}
            x={STRIP_X + run.from * CELL - 3}
            y={STRIP_Y - 3}
            width={CELL * run.count + 6}
            height={CELL + 6}
            fill="none"
            stroke={colors.brassBright}
            strokeWidth={2.5}
          />
        ))}
        <Label
          x={Math.min(STRIP_X + stop * CELL + CELL * 1.5, STRIP_X + (n - 3) * CELL)}
          y={STRIP_Y - 22}
          size={12}
          color={colors.brassBright}
        >
          {`stop ${stop} → reads ${stop}, ${(stop + 1) % n}, ${(stop + 2) % n}`}
        </Label>
      </g>

      {/* The three Green7 copies and the gaps between them. */}
      <g opacity={gapsIn}>
        {GREEN7_STOPS.map((s, i) => {
          const next = GREEN7_STOPS[(i + 1) % GREEN7_STOPS.length]!;
          const gap = (next - s + n) % n;
          const wraps = next < s;
          const midX = wraps
            ? STRIP_X + n * CELL + 16
            : STRIP_X + ((s + next) / 2) * CELL + CELL / 2;
          return (
            <g key={s}>
              <line
                x1={STRIP_X + s * CELL + CELL / 2}
                y1={STRIP_Y + CELL + 30}
                x2={STRIP_X + s * CELL + CELL / 2}
                y2={STRIP_Y + CELL + 46}
                stroke={colors.signalTeal}
                strokeWidth={1.5}
              />
              <Label x={midX} y={STRIP_Y + CELL + 60} size={12} color={colors.signalTeal}>
                {`gap ${gap}`}
              </Label>
            </g>
          );
        })}
        <Label x={200} y={STRIP_Y + CELL + 88} size={12} anchor="start" color={colors.textSecondary}>
          Green7 sits at stops 3, 16 and 24. Gaps of 13, 8 and 5.
        </Label>
      </g>

      <g opacity={verdict}>
        <Label x={30} y={370} size={14} anchor="start" color={colors.brassBright}>
          Every gap is wider than the window, so no stop shows two Green7s.
        </Label>
        <Label x={30} y={394} size={13} anchor="start" color={colors.textMuted}>
          A weighted die would price that pair at P(Green7)² and be wrong.
        </Label>
      </g>
    </AnimStage>
  );
};
