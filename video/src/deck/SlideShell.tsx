import React from 'react';
import { AbsoluteFill, useCurrentFrame, useVideoConfig } from 'remotion';
import { colors, tracking } from '../tokens';
import { fonts } from '../tokens/fonts';
import { Frame } from '../components/Frame';
import { progressAt } from '../components/motion';
import { Display, Kicker } from '../components/Type';
import { slideMetrics } from './metrics';

/**
 * The chrome every content slide sits in: the deco frame, a heading that
 * settles in under a drawing rule, and a footer marking the chapter and the
 * slide's place in the deck. Slide bodies supply only their own content, so
 * the chrome cannot drift between slide kinds.
 */
export const SlideShell: React.FC<{
  heading: string;
  chapterNumber: number;
  chapterTitle: string;
  index: number;
  total: number;
  children: React.ReactNode;
}> = ({ heading, chapterNumber, chapterTitle, index, total, children }) => {
  const frame = useCurrentFrame();
  const { width, height } = useVideoConfig();

  const rule = progressAt(frame, 2, 14, 'out');
  const head = progressAt(frame, 5, 16, 'out');

  const m = slideMetrics(width, height);

  return (
    <Frame>
      <AbsoluteFill
        style={{
          paddingLeft: m.padX,
          paddingRight: m.padX,
          paddingTop: m.padTop,
          paddingBottom: m.padBottom,
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        {/* Fixed height, whether the heading runs to one line or two — the
            body's room must not depend on how long a heading happens to be. */}
        <div style={{ opacity: head, height: m.headingBlock, flexShrink: 0, overflow: 'hidden' }}>
          <div
            style={{
              width: Math.round(width * 0.13) * rule,
              height: 2,
              backgroundColor: colors.brass,
              marginBottom: m.ruleGap,
            }}
          />
          <Display size={m.headingSize} style={{ textTransform: 'uppercase' }}>
            {heading}
          </Display>
        </div>

        {/* Fixed height and clipped: a body that miscalculates is a body that
            gets cut off, never one that prints over the footer. */}
        <div
          style={{
            height: m.bodyHeight,
            flexShrink: 0,
            display: 'flex',
            marginTop: m.bodyGap,
            overflow: 'hidden',
          }}
        >
          {children}
        </div>

        <div
          style={{
            marginTop: 'auto',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'baseline',
            borderTop: `1px solid ${colors.brassDim}`,
            paddingTop: Math.round(height * 0.018),
            opacity: 0.85,
          }}
        >
          <Kicker size={Math.round(width * 0.0105)} color={colors.textMuted}>
            {`Chapter ${chapterNumber} — ${chapterTitle}`}
          </Kicker>
          <span
            style={{
              fontFamily: fonts.mono,
              fontSize: Math.round(width * 0.0105),
              letterSpacing: tracking.smallCaps,
              color: colors.textMuted,
            }}
          >
            {`${index} / ${total}`}
          </span>
        </div>
      </AbsoluteFill>
    </Frame>
  );
};
