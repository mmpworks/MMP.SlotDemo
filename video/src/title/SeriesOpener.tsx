import React from 'react';
import { AbsoluteFill, interpolate, useCurrentFrame, useVideoConfig } from 'remotion';
import { colors, tracking } from '../tokens';
import { fonts } from '../tokens/fonts';
import { Frame } from '../components/Frame';
import { progressAt } from '../components/motion';
import { fitDisplaySize } from '../components/fitText';
import { ReelDrum } from './ReelDrum';
import { Sunburst } from './Sunburst';
import { SERIES } from '../data/chapters';
import { OPENER } from './timing';

/**
 * The six-second series opener, used at the head of every video.
 *
 * Shape: the machine takes power, three drums spin up and land on a row, the
 * rays bloom out from behind that row, and the two-line lockup settles into
 * the frame the drums vacate. Motion is braked, never sprung — the register
 * calls for confidence, not exuberance.
 */
export const SeriesOpener: React.FC = () => {
  const frame = useCurrentFrame();
  const { width, height } = useVideoConfig();

  const horizon = progressAt(frame, OPENER.horizonIn, OPENER.horizonDuration, 'out');
  const drumsIn = progressAt(frame, OPENER.drumsIn, OPENER.drumsInDuration, 'out');
  const drumsOut = progressAt(frame, OPENER.drumsOut, OPENER.drumsOutDuration, 'inOut');
  const burst = progressAt(frame, OPENER.sunburstIn, OPENER.sunburstDuration, 'out');
  const line1 = progressAt(frame, OPENER.line1In, OPENER.line1Duration, 'out');
  const rule = progressAt(frame, OPENER.ruleIn, OPENER.ruleDuration, 'out');
  const line2 = progressAt(frame, OPENER.line2In, OPENER.line2Duration, 'out');
  const frameReveal = progressAt(frame, OPENER.frameIn, OPENER.frameDuration, 'out');

  const drumWidth = Math.round(width * 0.135);
  const drumHeight = Math.round(height * 0.34);

  // Both lines are solved against the same zone so the lockup reads as one
  // block: line 2 is the anchor and carries the larger size.
  const zone = width * 0.62;
  const line1Size = fitDisplaySize(SERIES.line1, zone, 0.16, { min: 40, max: 96 });
  const line2Size = fitDisplaySize(SERIES.line2, zone * 1.02, 0.08, { min: 72, max: 168 });

  return (
    <Frame reveal={frameReveal}>
      <Sunburst reveal={burst} spin={interpolate(burst, [0, 1], [-6, 0])} opacity={burst} />

      {/* The horizon line: the first thing that happens is power. */}
      <AbsoluteFill style={{ alignItems: 'center', justifyContent: 'center' }}>
        <div
          style={{
            width: width * 0.86 * horizon,
            height: 1,
            backgroundColor: colors.brass,
            opacity: (1 - line1) * 0.7 + 0.08,
          }}
        />
      </AbsoluteFill>

      {/* Three drums, staggered in and staggered to rest. */}
      <AbsoluteFill
        style={{
          // AbsoluteFill lays out as a column by default; the drums sit in a row.
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'center',
          gap: Math.round(width * 0.022),
          opacity: (1 - drumsOut) * drumsIn,
          transform: `translateY(${interpolate(drumsIn, [0, 1], [-90, 0]) + drumsOut * -40}px) scale(${interpolate(drumsOut, [0, 1], [1, 0.92])})`,
        }}
      >
        {DRUMS.map((drum, i) => (
          <ReelDrum
            key={drum.landsOn}
            frame={frame - i * 4}
            spinStart={OPENER.spinStart}
            landFrame={OPENER.landFrames[i]!}
            revolutions={drum.revolutions}
            landsOn={drum.landsOn}
            width={drumWidth}
            height={drumHeight}
          />
        ))}
      </AbsoluteFill>

      {/* The lockup. Tracking closes as the line settles — the type arrives
          spread out and gathers, which reads as weight coming to rest. */}
      <AbsoluteFill style={{ alignItems: 'center', justifyContent: 'center' }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
          <div
            style={{
              fontFamily: fonts.display,
              fontWeight: 500,
              fontSize: line1Size,
              textTransform: 'uppercase',
              color: colors.brass,
              opacity: line1,
              letterSpacing: `${interpolate(line1, [0, 1], [0.42, 0.16])}em`,
              // Letterspacing pads the right edge; nudge back to optical center.
              marginRight: `-${interpolate(line1, [0, 1], [0.42, 0.16])}em`,
              whiteSpace: 'nowrap',
            }}
          >
            {SERIES.line1}
          </div>

          <div
            style={{
              width: Math.round(width * 0.4) * rule,
              height: 2,
              backgroundColor: colors.brassBright,
              marginTop: Math.round(height * 0.032),
              marginBottom: Math.round(height * 0.03),
            }}
          />

          <div
            style={{
              fontFamily: fonts.display,
              fontWeight: 700,
              fontSize: line2Size,
              textTransform: 'uppercase',
              color: colors.textPrimary,
              letterSpacing: tracking.display,
              marginRight: tracking.display,
              opacity: line2,
              transform: `translateY(${interpolate(line2, [0, 1], [34, 0])}px)`,
              whiteSpace: 'nowrap',
            }}
          >
            {SERIES.line2}
          </div>
        </div>
      </AbsoluteFill>
    </Frame>
  );
};

/**
 * Which glyph each column lands on, and how far it turns to get there.
 * Different revolution counts are what keep the three landings from reading
 * as one mechanism moving.
 */
const DRUMS = [
  { landsOn: 'gem', revolutions: 5 },
  { landsOn: 'gem', revolutions: 6 },
  { landsOn: 'gem', revolutions: 7 },
] as const;
