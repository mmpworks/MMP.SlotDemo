import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt } from '../components/motion';
import { AnimStage, Label } from './primitives';

/**
 * Chapter 6 — determinism is a scheduling property.
 *
 * Article 6's worked example: twelve spins over four workers, three each,
 * assigned before the run starts. The scheduler is then allowed to run them
 * in any order it likes — worker 3 may go first — and the totals do not move,
 * because which stream plays which spin was never the scheduler's decision.
 */
const SPINS = 12;
const WORKERS = 4;
const PER_WORKER = SPINS / WORKERS;

/** The order the OS happens to start the workers in. Any order is legal. */
const START_ORDER = [3, 0, 2, 1];

export const Ch06Quotas: React.FC = () => {
  const frame = useCurrentFrame();

  const spinsIn = (i: number) => progressAt(frame, 6 + i * 4, 14, 'out');
  const assign = progressAt(frame, 70, 60, 'inOut');
  const scheduleIn = (rank: number) => progressAt(frame, 170 + rank * 22, 22, 'out');
  const totalsIn = progressAt(frame, 300, 26, 'out');
  const verdict = progressAt(frame, 360, 30, 'out');

  return (
    <AnimStage viewBox="0 0 900 420">
      <Label x={24} y={28} size={12} anchor="start" color={colors.textSecondary}>
        twelve spins, decided before the run starts
      </Label>

      {/* The spin queue, before assignment. */}
      {Array.from({ length: SPINS }, (_, i) => {
        const worker = Math.floor(i / PER_WORKER);
        const assigned = assign * SPINS > i;
        const targetY = 110 + worker * 62;
        const targetX = 250 + (i % PER_WORKER) * 46;
        const x = assigned ? targetX : 24 + i * 46;
        const y = assigned ? targetY : 48;
        return (
          <g key={i} opacity={spinsIn(i)}>
            <rect
              x={x}
              y={y}
              width={38}
              height={30}
              fill={assigned ? colors.surfaceRaised : colors.surface}
              stroke={assigned ? colors.brass : colors.brassDim}
              strokeWidth={1.25}
            />
            <text
              x={x + 19}
              y={y + 15}
              textAnchor="middle"
              dominantBaseline="middle"
              fill={assigned ? colors.brassBright : colors.textSecondary}
              fontSize={13}
              fontFamily="monospace"
            >
              {i + 1}
            </text>
          </g>
        );
      })}

      {/* The four workers with their fixed quotas. */}
      {Array.from({ length: WORKERS }, (_, w) => {
        const rank = START_ORDER.indexOf(w);
        const started = scheduleIn(rank);
        return (
          <g key={w}>
            <rect
              x={80}
              y={104 + w * 62}
              width={150}
              height={42}
              fill={colors.surface}
              stroke={started > 0.5 ? colors.signalGreen : colors.brassDim}
              strokeWidth={1.5}
              opacity={assign}
            />
            <Label
              x={94}
              y={118 + w * 62}
              size={13}
              anchor="start"
              color={colors.textPrimary}
              reveal={assign}
            >
              {`worker ${w}`}
            </Label>
            <Label
              x={94}
              y={135 + w * 62}
              size={10}
              anchor="start"
              color={started > 0.5 ? colors.signalGreen : colors.textMuted}
              reveal={assign}
            >
              {started > 0.5 ? `started ${rank + 1} of 4` : 'waiting'}
            </Label>
            <Label x={430} y={125 + w * 62} size={11} anchor="start" color={colors.textMuted} reveal={assign}>
              {`${PER_WORKER} spins · private stream`}
            </Label>
          </g>
        );
      })}

      <Label x={330} y={370} size={12} color={colors.textSecondary} reveal={scheduleIn(3)}>
        {`the OS started them ${START_ORDER.join(', ')} — and it was free to`}
      </Label>

      {/* The totals, unmoved. */}
      <g opacity={totalsIn}>
        <rect x={620} y={104} width={256} height={168} fill={colors.surfaceRaised} stroke={colors.signalGreen} strokeWidth={1.5} />
        <Label x={748} y={128} size={13} color={colors.signalGreen} mono={false}>
          RunTotals
        </Label>
        {['spins', 'wagered', 'returned', 'hits'].map((name, i) => (
          <g key={name}>
            <Label x={642} y={162 + i * 26} size={12} anchor="start" color={colors.textSecondary}>
              {name}
            </Label>
            <Label x={856} y={162 + i * 26} size={12} anchor="end" color={colors.brassBright}>
              identical
            </Label>
          </g>
        ))}
      </g>

      <Label x={24} y={396} size={13} anchor="start" color={colors.brassBright} reveal={verdict}>
        A shared queue would let timing decide which stream plays which spin. Fixed quotas remove the choice.
      </Label>
    </AnimStage>
  );
};
