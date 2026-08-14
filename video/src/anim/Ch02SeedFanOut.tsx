import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt } from '../components/motion';
import { AnimStage, Edge, Label, Node } from './primitives';

/**
 * Chapter 2 — one master seed becomes eight replayable streams, each welded
 * to a quota decided before the run starts.
 *
 * Numbers from article 2: 10,000,000 spins over 8 workers is 1,250,000 each,
 * with worker 0 absorbing any remainder; SplitMix64 expands the seed into the
 * four xoshiro256** state words.
 */
const WORKERS = 8;
const TOTAL_SPINS = 10_000_000;
const QUOTA = TOTAL_SPINS / WORKERS;

export const Ch02SeedFanOut: React.FC = () => {
  const frame = useCurrentFrame();

  const seedIn = progressAt(frame, 6, 20, 'out');
  const mixIn = progressAt(frame, 34, 20, 'out');
  const wordsIn = (i: number) => progressAt(frame, 60 + i * 7, 16, 'out');
  const fanIn = (i: number) => progressAt(frame, 110 + i * 8, 20, 'out');
  const quotaIn = progressAt(frame, 200, 26, 'out');
  const replayIn = progressAt(frame, 300, 30, 'out');

  return (
    <AnimStage viewBox="0 0 900 420">
      <Node x={20} y={176} w={160} h={64} label="master seed" sub="+ worker id" reveal={seedIn} />

      <Edge d="M 180 208 L 246 208" length={70} reveal={mixIn} />
      <Node
        x={248}
        y={168}
        w={150}
        h={80}
        label="SplitMix64"
        sub="called 4 ×"
        reveal={mixIn}
        fill={colors.surfaceRaised}
      />

      {/* The four state words. */}
      {['_s0', '_s1', '_s2', '_s3'].map((word, i) => (
        <g key={word} opacity={wordsIn(i)}>
          <rect
            x={410}
            y={150 + i * 30}
            width={64}
            height={22}
            fill={colors.surface}
            stroke={colors.brassDim}
            strokeWidth={1}
          />
          <Label x={442} y={161 + i * 30} size={12} color={colors.signalTeal}>
            {word}
          </Label>
        </g>
      ))}
      <Label x={442} y={278} size={11} color={colors.textMuted} reveal={wordsIn(3)}>
        xoshiro256** state
      </Label>

      {/* Eight streams, each with its own quota. */}
      {Array.from({ length: WORKERS }, (_, i) => {
        const y = 26 + i * 44;
        const r = fanIn(i);
        return (
          <g key={i}>
            <Edge
              d={`M 474 ${161 + Math.min(i, 3) * 30} C 540 ${161 + Math.min(i, 3) * 30}, 560 ${y + 16}, 610 ${y + 16}`}
              length={190}
              reveal={r}
              color={colors.brassDim}
              width={1}
            />
            <rect
              x={612}
              y={y}
              width={150}
              height={32}
              fill={colors.surface}
              stroke={colors.brass}
              strokeWidth={1}
              opacity={r}
            />
            <Label x={624} y={y + 16} size={12} anchor="start" color={colors.textPrimary} reveal={r}>
              {`stream ${i}`}
            </Label>
            <Label
              x={752}
              y={y + 16}
              size={12}
              anchor="end"
              color={colors.brassBright}
              reveal={quotaIn}
            >
              {QUOTA.toLocaleString('en-US')}
            </Label>
          </g>
        );
      })}

      <Label x={686} y={396} size={12} color={colors.textSecondary} reveal={quotaIn}>
        {`${TOTAL_SPINS.toLocaleString('en-US')} spins ÷ ${WORKERS} workers, assigned before the run`}
      </Label>

      <Label x={200} y={330} size={13} color={colors.brassBright} reveal={replayIn} anchor="start">
        Same seed, same worker count, same spin target — the run replays bit for bit.
      </Label>
      <Label x={200} y={356} size={12} color={colors.textMuted} reveal={replayIn} anchor="start">
        Change the worker count and the partition changes with it.
      </Label>
    </AnimStage>
  );
};
