import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt, pulseAt } from '../components/motion';
import { AnimStage, Edge, Label, Node } from './primitives';

/**
 * Chapter 1 — the two lanes leaving the worker pool.
 *
 * Exact run totals batch 4,096 spins into worker-local longs and publish with
 * four atomic adds; telemetry leaves by a bounded 1,024-slot channel that
 * drops its oldest sample when it is full. One lane may lose data and the
 * other may not, which is the whole point of drawing them apart.
 *
 * Numbers: 4,096 batch, four Interlocked.Add calls, 1,024 slots, ~10 drains
 * per second, one curve point per 50,000 spins — all from article 1.
 */
const WORKERS = 4;
const RING_SLOTS = 12; // A readable stand-in for 1,024; the label carries the real figure.

export const Ch01TwoLanes: React.FC = () => {
  const frame = useCurrentFrame();

  const workersIn = (i: number) => progressAt(frame, 6 + i * 5, 16, 'out');
  const batchFill = progressAt(frame, 40, 70, 'inOut');
  const exactEdge = progressAt(frame, 110, 24, 'out');
  const countersIn = progressAt(frame, 120, 20, 'out');
  const teleEdge = progressAt(frame, 150, 24, 'out');
  const ringIn = progressAt(frame, 160, 20, 'out');
  const dropIn = progressAt(frame, 240, 24, 'out');
  const verdict = progressAt(frame, 320, 30, 'out');

  const addPulse = pulseAt(frame, 130, 26);

  return (
    <AnimStage viewBox="0 0 900 420">
      {/* Workers */}
      {Array.from({ length: WORKERS }, (_, i) => (
        <Node
          key={i}
          x={20}
          y={40 + i * 76}
          w={130}
          h={54}
          label={`worker ${i}`}
          sub="private stream"
          reveal={workersIn(i)}
        />
      ))}

      {/* Every worker feeds the same batch accumulator. */}
      {Array.from({ length: WORKERS }, (_, i) => (
        <Edge
          key={i}
          d={`M 150 ${67 + i * 76} L 220 210`}
          length={190}
          reveal={workersIn(i)}
          color={colors.brassDim}
          width={1}
        />
      ))}

      <Node x={222} y={182} w={150} h={58} label="batch" sub="4,096 spins" reveal={progressAt(frame, 30, 18, 'out')} />

      {/* The batch fills, then empties into the two lanes. */}
      <rect x={224} y={234} width={146 * batchFill} height={5} fill={colors.brass} opacity={0.8} />
      <Label x={297} y={256} size={11} color={colors.textMuted}>
        worker-local longs
      </Label>
      <Label x={297} y={272} size={11} color={colors.textMuted}>
        no synchronization
      </Label>

      {/* Exact lane — up. */}
      <Edge d="M 372 196 L 470 130" length={120} reveal={exactEdge} />
      <Label x={430} y={148} size={12} color={colors.signalGreen} reveal={exactEdge}>
        exact
      </Label>

      <g opacity={countersIn}>
        <rect
          x={474}
          y={46}
          width={200}
          height={140}
          fill={colors.surfaceRaised}
          stroke={colors.signalGreen}
          strokeWidth={1.5}
        />
        <Label x={574} y={64} size={15} color={colors.textPrimary} mono={false}>
          RunTotals
        </Label>
        <Label x={574} y={82} size={11} color={colors.textSecondary}>
          4 × Interlocked.Add
        </Label>
        {['spins', 'wagered', 'returned', 'hits'].map((name, i) => (
          <g key={name}>
            <rect
              x={492}
              y={96 + i * 21}
              width={164}
              height={16}
              fill={colors.surface}
              stroke={colors.signalGreen}
              strokeWidth={0.75}
              opacity={0.5 + addPulse * 0.5}
            />
            <Label x={500} y={104 + i * 21} size={10} anchor="start" color={colors.textSecondary}>
              {name}
            </Label>
          </g>
        ))}
      </g>
      <Label x={574} y={205} size={12} color={colors.signalGreen} reveal={countersIn}>
        every spin counted
      </Label>

      {/* Telemetry lane — down. */}
      <Edge d="M 372 226 L 470 296" length={122} reveal={teleEdge} color={colors.signalBlue} />
      <Label x={428} y={276} size={12} color={colors.signalBlue} reveal={teleEdge}>
        lossy
      </Label>

      <g opacity={ringIn}>
        <rect
          x={474}
          y={252}
          width={200}
          height={110}
          fill={colors.surface}
          stroke={colors.signalBlue}
          strokeWidth={1.5}
        />
        <Label x={574} y={272} size={14} color={colors.textPrimary} mono={false}>
          telemetry channel
        </Label>
        <Label x={574} y={292} size={11} color={colors.textSecondary}>
          1,024 slots, drop-oldest
        </Label>
        {Array.from({ length: RING_SLOTS }, (_, i) => {
          const dropped = i === 0 && dropIn > 0.3;
          return (
            <rect
              key={i}
              x={492 + i * 14}
              y={318}
              width={11}
              height={26}
              fill={dropped ? colors.surfaceDormant : colors.signalBlue}
              opacity={dropped ? 0.35 : 0.55 + (i / RING_SLOTS) * 0.35}
              stroke={dropped ? colors.signalEmber : 'none'}
              strokeWidth={dropped ? 1 : 0}
            />
          );
        })}
      </g>

      <Label x={574} y={378} size={12} color={colors.signalEmber} reveal={dropIn}>
        oldest sample dropped — the queue never blocks a spin
      </Label>

      {/* Reader */}
      <Edge d="M 674 307 L 760 307" length={90} reveal={dropIn} color={colors.signalBlue} />
      <Node
        x={762}
        y={272}
        w={120}
        h={70}
        label="chart"
        sub="~10 / sec"
        reveal={dropIn}
        accent={colors.signalBlue}
      />
      <Edge d="M 674 116 L 760 116" length={90} reveal={countersIn} color={colors.signalGreen} />
      <Node
        x={762}
        y={82}
        w={120}
        h={70}
        label="verdict"
        sub="1 pt / 50,000"
        reveal={countersIn}
        accent={colors.signalGreen}
      />

      <Label x={450} y={410} size={13} color={colors.brassBright} reveal={verdict}>
        A dropped sample costs one chart point. It never costs a counted spin.
      </Label>
    </AnimStage>
  );
};
