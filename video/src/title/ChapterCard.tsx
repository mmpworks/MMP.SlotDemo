import React from 'react';
import { AbsoluteFill, interpolate, useCurrentFrame, useVideoConfig } from 'remotion';
import { colors, tracking } from '../tokens';
import { fonts } from '../tokens/fonts';
import { Frame } from '../components/Frame';
import { progressAt } from '../components/motion';
import { fitDisplaySize } from '../components/fitText';
import { Sunburst } from './Sunburst';
import { CHAPTER_CARD } from './timing';
import type { Chapter } from '../data/chapters';

/**
 * The four-second chapter card that follows the opener.
 *
 * One template, rendered once per chapter with the chapter's own row from
 * `data/chapters.ts` — the numeral, kicker, and title are props, so there is
 * no per-chapter copy of this file to keep in step.
 */
export const ChapterCard: React.FC<{ chapter: Chapter }> = ({ chapter }) => {
  const frame = useCurrentFrame();
  const { width, height } = useVideoConfig();

  const frameReveal = progressAt(frame, CHAPTER_CARD.frameIn, CHAPTER_CARD.frameDuration, 'out');
  const numeral = progressAt(frame, CHAPTER_CARD.numeralIn, CHAPTER_CARD.numeralDuration, 'out');
  const kicker = progressAt(frame, CHAPTER_CARD.kickerIn, CHAPTER_CARD.kickerDuration, 'out');
  const rule = progressAt(frame, CHAPTER_CARD.ruleIn, CHAPTER_CARD.ruleDuration, 'out');
  const title = progressAt(frame, CHAPTER_CARD.titleIn, CHAPTER_CARD.titleDuration, 'out');

  const titleSize = fitDisplaySize(chapter.title, width * 0.74, 0.08, { min: 54, max: 128 });

  return (
    <Frame reveal={frameReveal}>
      <Sunburst reveal={numeral} spin={interpolate(numeral, [0, 1], [-4, 0])} opacity={0.5 * numeral} />

      {/* The chapter numeral sits behind the type as a ghosted plate. */}
      <AbsoluteFill style={{ alignItems: 'center', justifyContent: 'center' }}>
        <div
          style={{
            fontFamily: fonts.display,
            fontWeight: 700,
            fontSize: Math.round(height * 0.86),
            lineHeight: 1,
            color: colors.brass,
            opacity: numeral * 0.09,
            transform: `translateY(${interpolate(numeral, [0, 1], [70, 0])}px)`,
          }}
        >
          {chapter.number}
        </div>
      </AbsoluteFill>

      <AbsoluteFill style={{ alignItems: 'center', justifyContent: 'center' }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
          <div
            style={{
              fontFamily: fonts.display,
              fontWeight: 500,
              fontSize: Math.round(width * 0.019),
              letterSpacing: tracking.kicker,
              marginRight: tracking.kicker,
              textTransform: 'uppercase',
              color: colors.brass,
              opacity: kicker,
              whiteSpace: 'nowrap',
            }}
          >
            {`Chapter ${chapter.number} · ${chapter.kicker}`}
          </div>

          <div
            style={{
              width: Math.round(width * 0.34) * rule,
              height: 2,
              backgroundColor: colors.brassBright,
              marginTop: Math.round(height * 0.028),
              marginBottom: Math.round(height * 0.028),
            }}
          />

          <div
            style={{
              fontFamily: fonts.display,
              fontWeight: 700,
              fontSize: titleSize,
              letterSpacing: tracking.display,
              marginRight: tracking.display,
              textTransform: 'uppercase',
              color: colors.textPrimary,
              opacity: title,
              transform: `translateY(${interpolate(title, [0, 1], [28, 0])}px)`,
              whiteSpace: 'nowrap',
              textAlign: 'center',
            }}
          >
            {chapter.title}
          </div>
        </div>
      </AbsoluteFill>
    </Frame>
  );
};
