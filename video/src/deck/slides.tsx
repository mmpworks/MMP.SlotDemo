import React from 'react';
import { interpolate, useCurrentFrame, useVideoConfig } from 'remotion';
import { colors, tracking } from '../tokens';
import { fonts } from '../tokens/fonts';
import { progressAt } from '../components/motion';
import { fitMonoBlock } from '../components/fitText';
import { slideMetrics } from './metrics';
import { Body, Display, Kicker } from '../components/Type';
import type { Chapter, Slide, Stat } from '../data/chapters';
import { ChapterAnimation } from '../anim';

/** Frames between one revealed item and the next. */
const STAGGER = 9;
const ITEM_IN = 16;

/**
 * Slide bodies. Each one reveals its items in reading order on a fixed
 * stagger, so a narrator can pace to the slide instead of the other way
 * round. None of them animate after they have settled.
 */

export const PointsBody: React.FC<{ points: string[] }> = ({ points }) => {
  const frame = useCurrentFrame();
  const { width } = useVideoConfig();

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        gap: Math.round(width * 0.024),
        width: '100%',
      }}
    >
      {points.map((point, i) => {
        const p = progressAt(frame, 14 + i * STAGGER, ITEM_IN, 'out');
        return (
          <div
            key={point}
            style={{
              display: 'flex',
              alignItems: 'baseline',
              gap: Math.round(width * 0.017),
              opacity: p,
              transform: `translateX(${interpolate(p, [0, 1], [-26, 0])}px)`,
            }}
          >
            <span
              style={{
                width: Math.round(width * 0.014),
                height: Math.round(width * 0.014),
                flexShrink: 0,
                transform: 'rotate(45deg)',
                border: `2px solid ${colors.brass}`,
                alignSelf: 'center',
              }}
            />
            <Body size={Math.round(width * 0.0225)}>{point}</Body>
          </div>
        );
      })}
    </div>
  );
};

export const StatBody: React.FC<{ stats: Stat[]; note?: string }> = ({ stats, note }) => {
  const frame = useCurrentFrame();
  const { width } = useVideoConfig();
  const noteAt = 14 + stats.length * STAGGER + 6;
  const notep = progressAt(frame, noteAt, ITEM_IN, 'out');

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        gap: Math.round(width * 0.035),
        width: '100%',
        height: '100%',
      }}
    >
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: Math.round(width * 0.045),
          rowGap: Math.round(width * 0.03),
          alignItems: 'flex-end',
          // Takes the room the note does not; `minHeight: 0` lets it give room
          // back rather than pushing the note past the bottom of the body.
          flex: 1,
          minHeight: 0,
          alignContent: 'center',
        }}
      >
        {stats.map((stat, i) => {
          const p = progressAt(frame, 14 + i * STAGGER, ITEM_IN, 'out');
          return (
            <div
              key={stat.label}
              style={{
                opacity: p,
                transform: `translateY(${interpolate(p, [0, 1], [22, 0])}px)`,
                borderLeft: `2px solid ${colors.brass}`,
                paddingLeft: Math.round(width * 0.016),
              }}
            >
              <div
                style={{
                  fontFamily: fonts.mono,
                  fontWeight: 500,
                  fontSize: Math.round(width * 0.046),
                  color: colors.brassBright,
                  lineHeight: 1.05,
                }}
              >
                {stat.value}
              </div>
              <div
                style={{
                  fontFamily: fonts.body,
                  fontSize: Math.round(width * 0.0165),
                  color: colors.textSecondary,
                  marginTop: Math.round(width * 0.006),
                  maxWidth: Math.round(width * 0.26),
                }}
              >
                {stat.label}
              </div>
            </div>
          );
        })}
      </div>

      {note ? (
        // Fixed two-line reserve. A note that runs longer is a copy problem to
        // fix in the data, not a slide that quietly grows into the footer.
        <div
          style={{
            opacity: notep,
            maxWidth: Math.round(width * 0.86),
            flexShrink: 0,
            height: Math.ceil(Math.round(width * 0.0155) * 1.42 * 2),
            overflow: 'hidden',
          }}
        >
          <Body size={Math.round(width * 0.0155)} color={colors.textMuted}>
            {note}
          </Body>
        </div>
      ) : null}
    </div>
  );
};

/** Vertical rhythm inside the code panel. */
const CODE_LINE_HEIGHT = 1.55;

export const CodeBody: React.FC<{ lines: string[]; source: string; caption?: string }> = ({
  lines,
  source,
  caption,
}) => {
  const frame = useCurrentFrame();
  const { width, height } = useVideoConfig();
  const panel = progressAt(frame, 12, 18, 'out');

  const m = slideMetrics(width, height);
  const gap = Math.round(width * 0.014);
  const panelPadY = Math.round(width * 0.018);
  const panelPadX = Math.round(width * 0.022);

  // The source line and, when present, the caption sit under the panel and
  // take their room first. Whatever is left is what the code has to fit in.
  const sourceSize = Math.round(width * 0.0115);
  const captionSize = Math.round(width * 0.0155);
  const attributionHeight =
    Math.ceil(sourceSize * 1.5) + (caption ? Math.ceil(captionSize * 1.42) + 6 : 0);

  const panelHeight = m.bodyHeight - attributionHeight - gap;
  const codeSize = fitMonoBlock(
    lines,
    { width: m.bodyWidth - panelPadX * 2, height: panelHeight - panelPadY * 2 },
    CODE_LINE_HEIGHT,
    { min: 15, max: Math.round(width * 0.0175) },
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', width: '100%', gap }}>
      <div
        style={{
          opacity: panel,
          transform: `translateY(${interpolate(panel, [0, 1], [16, 0])}px)`,
          backgroundColor: colors.surface,
          border: `1px solid ${colors.brassDim}`,
          padding: `${panelPadY}px ${panelPadX}px`,
          height: panelHeight,
          boxSizing: 'border-box',
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'center',
        }}
      >
        {lines.map((line, i) => {
          const p = progressAt(frame, 18 + i * 4, 12, 'out');
          return (
            <div
              key={`${i}-${line}`}
              style={{
                fontFamily: fonts.mono,
                fontSize: codeSize,
                lineHeight: CODE_LINE_HEIGHT,
                color: colors.textPrimary,
                opacity: p,
                whiteSpace: 'pre',
              }}
            >
              {line === '' ? ' ' : line}
            </div>
          );
        })}
      </div>

      <div style={{ opacity: panel, height: attributionHeight, flexShrink: 0 }}>
        {/* A file path keeps its own casing — it is a path, not a label. */}
        <div
          style={{
            fontFamily: fonts.mono,
            fontSize: sourceSize,
            lineHeight: 1.5,
            letterSpacing: tracking.smallCaps,
            color: colors.textMuted,
          }}
        >
          {source}
        </div>
        {caption ? (
          <Body size={captionSize} color={colors.textSecondary} style={{ marginTop: 6 }}>
            {caption}
          </Body>
        ) : null}
      </div>
    </div>
  );
};

export const AnimBody: React.FC<{ chapterId: string; caption: string }> = ({
  chapterId,
  caption,
}) => {
  const frame = useCurrentFrame();
  const { width } = useVideoConfig();
  const cap = progressAt(frame, 10, 18, 'out');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', width: '100%', minHeight: 0, gap: Math.round(width * 0.012) }}>
      <div style={{ flex: 1, minHeight: 0, position: 'relative' }}>
        <ChapterAnimation chapterId={chapterId} />
      </div>
      <div style={{ opacity: cap, maxWidth: Math.round(width * 0.8) }}>
        <Body size={Math.round(width * 0.0155)} color={colors.textSecondary}>
          {caption}
        </Body>
      </div>
    </div>
  );
};

/**
 * The deck's own opening slide: the chapter title and its thesis, set larger
 * than any body slide. It is the beat where the narrator states the claim the
 * rest of the deck defends.
 */
export const DeckOpener: React.FC<{ chapter: Chapter }> = ({ chapter }) => {
  const frame = useCurrentFrame();
  const { width } = useVideoConfig();

  const kicker = progressAt(frame, 4, 16, 'out');
  const title = progressAt(frame, 12, 20, 'out');
  const rule = progressAt(frame, 22, 18, 'out');
  const thesis = progressAt(frame, 30, 22, 'out');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center', width: '100%' }}>
      <div style={{ opacity: kicker }}>
        <Kicker size={Math.round(width * 0.0145)}>{`Chapter ${chapter.number} · ${chapter.kicker}`}</Kicker>
      </div>

      <Display
        size={Math.round(width * 0.058)}
        style={{
          textTransform: 'uppercase',
          marginTop: Math.round(width * 0.014),
          opacity: title,
          transform: `translateY(${interpolate(title, [0, 1], [24, 0])}px)`,
        }}
      >
        {chapter.title}
      </Display>

      <div
        style={{
          width: Math.round(width * 0.28) * rule,
          height: 2,
          backgroundColor: colors.brassBright,
          marginTop: Math.round(width * 0.02),
          marginBottom: Math.round(width * 0.02),
        }}
      />

      <Body
        size={Math.round(width * 0.0215)}
        color={colors.textSecondary}
        style={{ maxWidth: Math.round(width * 0.68), opacity: thesis }}
      >
        {chapter.thesis}
      </Body>

      <div style={{ marginTop: Math.round(width * 0.026), opacity: thesis }}>
        <span
          style={{
            fontFamily: fonts.mono,
            fontSize: Math.round(width * 0.0115),
            letterSpacing: tracking.smallCaps,
            color: colors.textMuted,
          }}
        >
          {chapter.article}
        </span>
      </div>
    </div>
  );
};

export function slideHeading(slide: Slide): string {
  return slide.heading;
}
