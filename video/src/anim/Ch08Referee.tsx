import React from 'react';
import { useCurrentFrame } from 'remotion';
import { colors } from '../tokens';
import { progressAt } from '../components/motion';
import { AnimStage, Edge, Label, Node } from './primitives';

/**
 * Chapter 8 — three paths to one number, and a referee that shares no code.
 *
 * The closed form and the simulation both come out of the engine. The
 * exhaustive referee writes its own window builder and its own evaluation
 * loop over all 22³ = 10,648 Classic3 outcomes, sharing data with the other
 * two and code with neither. That duplication is the point: it is what makes
 * the agreement mean something.
 */
const OUTCOMES = 22 ** 3; // 10,648, per article 8

export const Ch08Referee: React.FC = () => {
  const frame = useCurrentFrame();

  const analytic = progressAt(frame, 8, 24, 'out');
  const sim = progressAt(frame, 36, 24, 'out');
  const ref = progressAt(frame, 72, 26, 'out');
  const enumerate = progressAt(frame, 110, 130, 'inOut');
  const edgeA = progressAt(frame, 250, 26, 'out');
  const edgeB = progressAt(frame, 280, 26, 'out');
  const dataEdge = progressAt(frame, 310, 26, 'out');
  const verdict = progressAt(frame, 360, 30, 'out');

  const walked = Math.round(enumerate * OUTCOMES);

  return (
    <AnimStage viewBox="0 0 900 420">
      <Node x={40} y={40} w={200} h={80} label="AnalyticMath" sub="closed form" reveal={analytic} fill={colors.surfaceRaised} />
      <Node x={40} y={250} w={200} h={80} label="SimulationEngine" sub="sampled, 10M spins" reveal={sim} />

      <Node
        x={620}
        y={140}
        w={230}
        h={100}
        label="exhaustive referee"
        sub="its own loops"
        reveal={ref}
        accent={colors.signalGreen}
        fill={colors.surface}
      />

      {/* The referee's counter, walking every outcome. */}
      <g opacity={ref}>
        <Label x={735} y={264} size={16} color={colors.signalGreen}>
          {walked.toLocaleString('en-US')}
        </Label>
        <Label x={735} y={286} size={11} color={colors.textMuted}>
          {`of ${OUTCOMES.toLocaleString('en-US')} outcomes — 22³`}
        </Label>
        <rect x={620} y={298} width={230} height={5} fill={colors.surfaceDormant} />
        <rect x={620} y={298} width={230 * enumerate} height={5} fill={colors.signalGreen} opacity={0.8} />
        <Label x={735} y={320} size={11} color={colors.textMuted}>
          total pay ÷ total wager is the RTP, not an estimate of it
        </Label>
      </g>

      {/* Agreement edges. */}
      <Edge d="M 240 80 C 420 80, 460 170, 618 174" length={400} reveal={edgeA} color={colors.signalGreen} />
      <Label x={430} y={112} size={11} color={colors.signalGreen} reveal={edgeA}>
        integer equality on the counts
      </Label>

      <Edge d="M 240 290 C 420 290, 460 216, 618 208" length={400} reveal={edgeB} color={colors.signalBlue} />
      <Label x={430} y={286} size={11} color={colors.signalBlue} reveal={edgeB}>
        inside z · sigma / √N
      </Label>

      <Edge d="M 140 120 L 140 248" length={130} reveal={dataEdge} color={colors.brassDim} dashed />
      <Label x={200} y={186} size={11} anchor="start" color={colors.textMuted} reveal={dataEdge}>
        one paytable, one set of strips
      </Label>

      <g opacity={verdict}>
        <Label x={40} y={370} size={13} anchor="start" color={colors.brassBright}>
          The referee shares data with both and code with neither.
        </Label>
        <Label x={40} y={396} size={12} anchor="start" color={colors.textMuted}>
          A simulator that checks itself is a circular argument. This one is not.
        </Label>
      </g>
    </AnimStage>
  );
};
