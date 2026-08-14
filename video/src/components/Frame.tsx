import React from 'react';
import { AbsoluteFill } from 'remotion';
import { colors } from '../tokens';
import { WaitForFonts } from './WaitForFonts';

/**
 * The deco frame every slide and card sits inside: near-black ground, a thin
 * brass rule inset from the edge, and stepped corner marks. The register is
 * the companion site's — art-deco framing over a cinematic ground — so a
 * viewer moving between the site and the video sees one house.
 */
const INSET = 44;
const CORNER = 34;

export const Frame: React.FC<{
  children: React.ReactNode;
  /** 0 hides the frame, 1 draws it whole. Corners lead, rules follow. */
  reveal?: number;
  background?: string;
}> = ({ children, reveal = 1, background = colors.ground }) => {
  const ruleReveal = Math.max(0, (reveal - 0.25) / 0.75);

  return (
    <AbsoluteFill style={{ backgroundColor: background, overflow: 'hidden' }}>
      <Vignette />
      <div
        style={{
          position: 'absolute',
          inset: INSET,
          border: `1px solid ${colors.brassDim}`,
          opacity: ruleReveal * 0.8,
        }}
      />
      {CORNERS.map(([x, y, sx, sy]) => (
        <Corner key={`${x}-${y}`} x={x} y={y} scaleX={sx} scaleY={sy} opacity={reveal} />
      ))}
      {/* Every composition sits inside a Frame, so the font gate lives here
          rather than as a rule each composition has to remember. */}
      <WaitForFonts>{children}</WaitForFonts>
    </AbsoluteFill>
  );
};

/** Corner marks, as [left|right, top|bottom, x-direction, y-direction]. */
const CORNERS: Array<[string, string, number, number]> = [
  ['left', 'top', 1, 1],
  ['right', 'top', -1, 1],
  ['left', 'bottom', 1, -1],
  ['right', 'bottom', -1, -1],
];

const Corner: React.FC<{
  x: string;
  y: string;
  scaleX: number;
  scaleY: number;
  opacity: number;
}> = ({ x, y, scaleX, scaleY, opacity }) => (
  <div
    style={{
      position: 'absolute',
      [x]: INSET - 1,
      [y]: INSET - 1,
      width: CORNER,
      height: CORNER,
      borderTop: `2px solid ${colors.brass}`,
      borderLeft: `2px solid ${colors.brass}`,
      transform: `scale(${scaleX}, ${scaleY})`,
      transformOrigin: `${x} ${y}`,
      opacity,
    }}
  />
);

/**
 * A soft radial fall-off toward the corners. The era's printing left bloom and
 * grain; a perfectly flat digital field reads as the wrong decade.
 */
const Vignette: React.FC = () => (
  <AbsoluteFill
    style={{
      background: `radial-gradient(ellipse at 50% 45%, ${colors.surface} 0%, ${colors.ground} 62%, ${colors.groundDeep} 100%)`,
    }}
  />
);
