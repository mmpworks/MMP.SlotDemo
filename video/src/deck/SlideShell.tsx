import React from 'react';
import { AbsoluteFill, useCurrentFrame, useVideoConfig } from 'remotion';
import { colors, tracking } from '../tokens';
import { fonts } from '../tokens/fonts';
import { Frame } from '../components/Frame';
import { progressAt } from '../components/motion';
import { Display, Kicker } from '../components/Type';

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

  const marginX = Math.round(width * 0.085);

  return (
    <Frame>
      <AbsoluteFill
        style={{
          paddingLeft: marginX,
          paddingRight: marginX,
          paddingTop: Math.round(height * 0.11),
          paddingBottom: Math.round(height * 0.1),
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <div style={{ opacity: head }}>
          <div
            style={{
              width: Math.round(width * 0.13) * rule,
              height: 2,
              backgroundColor: colors.brass,
              marginBottom: Math.round(height * 0.026),
            }}
          />
          <Display size={Math.round(width * 0.037)} style={{ textTransform: 'uppercase' }}>
            {heading}
          </Display>
        </div>

        <div style={{ flex: 1, display: 'flex', minHeight: 0, paddingTop: Math.round(height * 0.05) }}>
          {children}
        </div>

        <div
          style={{
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
