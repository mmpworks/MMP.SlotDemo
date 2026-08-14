import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt } from '../components/motion';
import { AnimStage, Edge, Label, Node } from './primitives';

/**
 * Chapter 4 — the game is priced before a spin runs.
 *
 * Marginals and joint row-pair tables come off the strips, feed EV and sigma,
 * and one scalar resizes the canonical paytable until its average return
 * equals the target. The worked factor is article 4's: a 0.75 target over an
 * unscaled EV of 0.50 gives 1.5. The band is z·sigma/sqrt(N) with z = 2.576.
 */
const TARGET = 0.75;
const UNSCALED_EV = 0.5;
const FACTOR = TARGET / UNSCALED_EV; // 1.5, per article 4
const Z = 2.576;

export const Ch04Pricing: React.FC = () => {
  const frame = useCurrentFrame();

  const stage = (i: number) => progressAt(frame, 8 + i * 26, 22, 'out');
  const edge = (i: number) => progressAt(frame, 26 + i * 26, 20, 'out');
  const solve = progressAt(frame, 170, 30, 'out');
  const bandIn = progressAt(frame, 230, 26, 'out');
  // N climbs, and the band closes as 1/sqrt(N).
  const nProgress = progressAt(frame, 262, 150, 'inOut');

  const n = Math.round(1000 * Math.pow(10, nProgress * 4)); // 1e3 → 1e7
  const halfWidth = (Z * 1.8) / Math.sqrt(n); // sigma stands in at 1.8 wagers
  const bandPx = Math.min(120, halfWidth * 3400);

  return (
    <AnimStage viewBox="0 0 900 420">
      <Node x={16} y={40} w={132} h={62} label="strips" sub="counts per reel" reveal={stage(0)} />
      <Edge d="M 148 71 L 196 71" length={52} reveal={edge(0)} />
      <Node x={198} y={22} w={150} h={44} label="marginals" sub="P(symbol, reel)" reveal={stage(1)} fontSize={13} />
      <Node x={198} y={76} w={150} h={44} label="joint tables" sub="row pairs" reveal={stage(1)} fontSize={13} />

      <Edge d="M 348 44 C 380 44, 380 100, 410 100" length={100} reveal={edge(1)} />
      <Edge d="M 348 98 L 410 100" length={64} reveal={edge(1)} />

      <Node x={412} y={62} w={150} h={76} label="AnalyticMath" sub="EV and sigma" reveal={stage(2)} fill={colors.surfaceRaised} />

      {/* The solver. */}
      <Edge d="M 487 138 L 487 186" length={50} reveal={edge(2)} />
      <Node x={392} y={188} w={190} h={78} label="PaytableSolver" sub="one scale factor" reveal={stage(3)} />

      <g opacity={solve}>
        <Label x={487} y={292} size={14} color={colors.brassBright}>
          {`${TARGET.toFixed(2)} ÷ ${UNSCALED_EV.toFixed(2)} = ${FACTOR.toFixed(1)}`}
        </Label>
        <Label x={487} y={314} size={11} color={colors.textMuted}>
          target RTP ÷ unscaled EV — one factor resizes every prize
        </Label>
        <Label x={487} y={336} size={11} color={colors.textMuted}>
          each award rounds half-to-even into whole millicents
        </Label>
      </g>

      {/* The band the dashboard draws. */}
      <Edge d="M 562 100 L 626 100" length={68} reveal={bandIn} />
      <g opacity={bandIn}>
        <rect x={628} y={30} width={250} height={150} fill={colors.surface} stroke={colors.brassDim} strokeWidth={1} />
        <line x1={628} y1={105} x2={878} y2={105} stroke={colors.signalBlue} strokeWidth={1.5} />
        <rect
          x={628}
          y={105 - bandPx / 2}
          width={250}
          height={bandPx}
          fill={colors.signalBlue}
          opacity={0.16}
        />
        <line
          x1={628}
          y1={105 - bandPx / 2}
          x2={878}
          y2={105 - bandPx / 2}
          stroke={colors.signalBlue}
          strokeWidth={1}
          strokeDasharray="4 4"
        />
        <line
          x1={628}
          y1={105 + bandPx / 2}
          x2={878}
          y2={105 + bandPx / 2}
          stroke={colors.signalBlue}
          strokeWidth={1}
          strokeDasharray="4 4"
        />
        <Label x={753} y={196} size={12} color={colors.textSecondary}>
          {`half-width = z · sigma / √N,  z = ${Z}`}
        </Label>
        <Label x={753} y={218} size={13} color={colors.brassBright}>
          {`N = ${n.toLocaleString('en-US')}`}
        </Label>
        <Label x={753} y={244} size={11} color={colors.textMuted}>
          two-sided 99% — a correct game still lands
        </Label>
        <Label x={753} y={260} size={11} color={colors.textMuted}>
          outside it about 1% of the time
        </Label>
      </g>

      <Label x={450} y={400} size={13} color={colors.brassBright} reveal={progressAt(frame, 420, 30, 'out')}>
        Nothing here has run a spin yet. The chart already knows where the curve belongs.
      </Label>
    </AnimStage>
  );
};
