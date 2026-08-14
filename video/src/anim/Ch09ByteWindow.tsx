import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt } from '../components/motion';
import { AnimStage, Bar, Cell, Label } from './primitives';

/**
 * Chapter 9 — extend the strip, delete the remainder.
 *
 * Article 9's worked example: a strip `A B C D E` becomes `A B C D E A B` at
 * construction, and a window that starts at D reads `D E A` as a contiguous
 * slice with no remainder operation per cell. Two to four extra entries per
 * reel buys that, whether the strip has 22 stops or 128.
 *
 * The bars are the measured comparison from the same article: window + rules
 * at 16.07M outcomes/sec, the packed-key dictionary at 2.14M, and progressive
 * narrowing at 20.53M.
 */
const STRIP = ['A', 'B', 'C', 'D', 'E'];
const EXTENDED = [...STRIP, 'A', 'B']; // Rows − 1 = 2 wrapped entries appended

const RATES = [
  { label: 'window + rules', value: 16.07, note: '1.00×', color: colors.signalBlue },
  { label: 'packed-key dictionary', value: 2.14, note: '0.133×', color: colors.signalEmber },
  { label: 'progressive arrays', value: 20.53, note: '1.277×', color: colors.signalGreen },
];
const MAX_RATE = 20.53;

export const Ch09ByteWindow: React.FC = () => {
  const frame = useCurrentFrame();

  const before = progressAt(frame, 6, 22, 'out');
  const modulo = progressAt(frame, 40, 40, 'out');
  const extend = progressAt(frame, 110, 34, 'out');
  const slice = progressAt(frame, 165, 30, 'out');
  const barsIn = (i: number) => progressAt(frame, 250 + i * 26, 34, 'out');
  const verdict = progressAt(frame, 380, 30, 'out');

  const CELL = 52;

  return (
    <AnimStage viewBox="0 0 900 420">
      {/* Before: the wrap costs a remainder per cell. */}
      <Label x={24} y={34} size={12} anchor="start" color={colors.textSecondary}>
        before — a window starting at D wraps, so every cell pays for a remainder
      </Label>
      {STRIP.map((s, i) => (
        <g key={s} opacity={before}>
          <Cell x={24 + i * CELL} y={48} size={CELL - 4} label={s} state={i >= 3 ? 'lit' : 'plain'} />
        </g>
      ))}
      <Label x={310} y={72} size={13} anchor="start" color={colors.signalAmber} reveal={modulo}>
        strip[(stop + row) % strip.Length]
      </Label>
      <Label x={310} y={94} size={11} anchor="start" color={colors.textMuted} reveal={modulo}>
        150,000,000 of them over a 10M-spin, five-reel, three-row run
      </Label>

      {/* After: two wrapped entries appended at construction. */}
      <Label x={24} y={158} size={12} anchor="start" color={colors.textSecondary}>
        after — append Rows − 1 wrapped entries once, at construction
      </Label>
      {EXTENDED.map((s, i) => {
        const wrapped = i >= STRIP.length;
        const inWindow = slice > 0.4 && i >= 3 && i <= 5;
        return (
          <g key={i} opacity={wrapped ? extend : before}>
            <Cell
              x={24 + i * CELL}
              y={172}
              size={CELL - 4}
              label={s}
              state={inWindow ? 'lit' : wrapped ? 'wrapped' : 'plain'}
            />
          </g>
        );
      })}
      <Label x={24 + 5 * CELL + CELL / 2} y={238} size={11} color={colors.signalTeal} reveal={extend}>
        two appended entries
      </Label>

      <g opacity={slice}>
        <rect
          x={24 + 3 * CELL - 4}
          y={168}
          width={CELL * 3}
          height={CELL + 4}
          fill="none"
          stroke={colors.brassBright}
          strokeWidth={2.5}
        />
        <Label x={420} y={186} size={13} anchor="start" color={colors.brassBright}>
          a window at D reads D E A — one contiguous slice
        </Label>
        <Label x={420} y={208} size={11} anchor="start" color={colors.textMuted}>
          two to four extra entries per reel, at 22 stops or at 128
        </Label>
      </g>

      {/* Measured outcomes per second. */}
      <Label x={24} y={278} size={12} anchor="start" color={colors.textSecondary}>
        measured — outcomes per second, loaded-game evaluation
      </Label>
      {RATES.map((rate, i) => {
        const r = barsIn(i);
        return (
          <g key={rate.label}>
            <Label x={230} y={306 + i * 30} size={12} anchor="end" color={colors.textSecondary} reveal={r}>
              {rate.label}
            </Label>
            <Bar
              x={242}
              y={296 + i * 30}
              maxWidth={480}
              height={19}
              fraction={rate.value / MAX_RATE}
              color={rate.color}
              reveal={r}
            />
            <Label
              x={252 + 480 * (rate.value / MAX_RATE)}
              y={306 + i * 30}
              size={12}
              anchor="start"
              color={colors.brassBright}
              reveal={r}
            >
              {`${rate.value.toFixed(2)}M   ${rate.note}`}
            </Label>
          </g>
        );
      })}

      <Label x={24} y={402} size={13} anchor="start" color={colors.brassBright} reveal={verdict}>
        The dictionary lost. Neither number was reported until the checksums matched.
      </Label>
    </AnimStage>
  );
};
