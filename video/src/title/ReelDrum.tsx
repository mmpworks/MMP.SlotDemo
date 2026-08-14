import React from 'react';
import { interpolate } from 'remotion';
import { colors } from '../tokens';
import { Glyph, GLYPH_ORDER, type GlyphName } from './Glyphs';
import { progressAt } from '../components/motion';

/**
 * One spinning reel column for the opener.
 *
 * The drum turns fast, then decelerates into a landing on `landsOn`. Speed
 * drives a vertical blur, so the column reads as a physical drum with mass
 * rather than a list scrolling. Nothing overshoots — a real drum is braked,
 * it does not bounce back.
 *
 * Geometry: `position` counts cells traveled. The cell at `position % cycle`
 * is the one centered in the window, so landing on a chosen glyph is a matter
 * of ending `position` at a whole number of revolutions plus that glyph's
 * index. Three cycles are rendered and the strip is offset back by one, so
 * the window never runs off either end of the list.
 */

const CELL = 150;
const CYCLES_RENDERED = 3;

export const ReelDrum: React.FC<{
  frame: number;
  /** Frame the drum starts turning. */
  spinStart: number;
  /** Frame the drum has come to rest. */
  landFrame: number;
  /** Full turns before the landing — stagger this per column. */
  revolutions: number;
  landsOn: GlyphName;
  height: number;
  width: number;
}> = ({ frame, spinStart, landFrame, revolutions, landsOn, height, width }) => {
  const cycle = GLYPH_ORDER.length;
  const landIndex = GLYPH_ORDER.indexOf(landsOn);
  const travel = revolutions * cycle + landIndex;

  const spin = progressAt(frame, spinStart, landFrame - spinStart, 'out');
  const position = spin * travel;

  // Blur tracks the per-frame delta, which is what the eye reads as speed.
  const previous = progressAt(frame - 1, spinStart, landFrame - spinStart, 'out') * travel;
  const blur = Math.min(24, Math.max(0, position - previous) * 5);

  const phase = position % cycle;
  const offset = height / 2 - CELL / 2 - phase * CELL - cycle * CELL;

  const cells: GlyphName[] = [];
  for (let i = 0; i < cycle * CYCLES_RENDERED; i++) cells.push(GLYPH_ORDER[i % cycle]!);

  const settle = progressAt(frame, landFrame - 6, 10, 'out');

  return (
    <div
      style={{
        width,
        height,
        position: 'relative',
        overflow: 'hidden',
        backgroundColor: colors.surfaceDormant,
        border: `1px solid ${colors.brassDim}`,
        // The drum's curvature, as an inset shadow top and bottom. Shaded to
        // groundDeep rather than pure black — the palette has no pure black.
        boxShadow: `inset 0 ${Math.round(height * 0.3)}px ${Math.round(height * 0.26)}px -${Math.round(height * 0.18)}px ${colors.groundDeep}, inset 0 -${Math.round(height * 0.3)}px ${Math.round(height * 0.26)}px -${Math.round(height * 0.18)}px ${colors.groundDeep}`,
      }}
    >
      {/* The payline band sits BEHIND the glyphs and lights the landed cell,
          so the row the drums stopped on is the brightest thing in the frame
          rather than a hairline over it. */}
      <div
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          top: `calc(50% - ${CELL / 2}px)`,
          height: CELL,
          background: `linear-gradient(180deg, transparent 0%, ${colors.brassDim} 18%, ${colors.brass} 50%, ${colors.brassDim} 82%, transparent 100%)`,
          opacity: settle * 0.22,
          transform: `scaleX(${interpolate(settle, [0, 1], [0.3, 1])})`,
        }}
      />

      <div style={{ position: 'absolute', left: 0, right: 0, top: offset }}>
        {cells.map((name, i) => {
          // Distance of this cell's center from the payline, in cells. The
          // neighbor rows recede so the landed row owns the frame; while the
          // drum is still turning nothing is dimmed, because there is no
          // landed row yet.
          const centerY = offset + i * CELL + CELL / 2;
          const distance = Math.abs(centerY - height / 2) / CELL;
          const recede = settle * Math.min(1, distance);

          return (
            <div
              key={`${name}-${i}`}
              style={{
                height: CELL,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                opacity: interpolate(recede, [0, 1], [1, 0.28]),
                filter:
                  blur > 0.4
                    ? `blur(${blur}px)`
                    : recede > 0.05
                      ? `blur(${(recede * 2.2).toFixed(2)}px) brightness(${interpolate(recede, [0, 1], [1, 0.6]).toFixed(2)})`
                      : undefined,
              }}
            >
              <Glyph name={name} size={CELL * (0.62 + settle * (1 - Math.min(1, distance)) * 0.14)} />
            </div>
          );
        })}
      </div>

      {/* The payline rule itself, struck across the lit band. */}
      <div
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          top: '50%',
          height: 2,
          backgroundColor: colors.brassBright,
          opacity: settle * 0.5,
          transform: `scaleX(${interpolate(settle, [0, 1], [0.2, 1])})`,
        }}
      />
    </div>
  );
};
