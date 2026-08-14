import React from 'react';
import { colors } from '../tokens';
import { fonts } from '../tokens/fonts';

/**
 * Diagram primitives shared by the nine chapter animations.
 *
 * All of them are SVG and take an explicit `reveal` in 0..1 rather than
 * reading the frame themselves, so an animation composes its own timeline in
 * one place and the pieces stay dumb.
 */

export const AnimStage: React.FC<{
  children: React.ReactNode;
  /** The coordinate space the animation is authored in. */
  viewBox: string;
}> = ({ children, viewBox }) => (
  <svg
    viewBox={viewBox}
    width="100%"
    height="100%"
    preserveAspectRatio="xMidYMid meet"
    style={{ display: 'block', overflow: 'visible' }}
  >
    <defs>
      <marker
        id="arrowhead"
        viewBox="0 0 10 10"
        refX="9"
        refY="5"
        markerWidth="6"
        markerHeight="6"
        orient="auto-start-reverse"
      >
        <path d="M 0 0 L 10 5 L 0 10 z" fill={colors.brass} />
      </marker>
    </defs>
    {children}
  </svg>
);

export const Node: React.FC<{
  x: number;
  y: number;
  w: number;
  h: number;
  label: string;
  sub?: string;
  reveal?: number;
  accent?: string;
  fill?: string;
  fontSize?: number;
}> = ({ x, y, w, h, label, sub, reveal = 1, accent = colors.brass, fill = colors.surface, fontSize = 15 }) => (
  <g opacity={reveal} transform={`translate(${x} ${y})`}>
    <rect width={w} height={h} fill={fill} stroke={accent} strokeWidth={1.5} rx={2} />
    <text
      x={w / 2}
      y={sub ? h / 2 - 3 : h / 2 + 1}
      textAnchor="middle"
      dominantBaseline="middle"
      fill={colors.textPrimary}
      fontFamily={fonts.display}
      fontSize={fontSize}
      letterSpacing="0.04em"
    >
      {label}
    </text>
    {sub ? (
      <text
        x={w / 2}
        y={h / 2 + fontSize}
        textAnchor="middle"
        dominantBaseline="middle"
        fill={colors.textSecondary}
        fontFamily={fonts.mono}
        fontSize={fontSize * 0.72}
      >
        {sub}
      </text>
    ) : null}
  </g>
);

/**
 * An edge that draws itself from source to target. `reveal` is the fraction
 * of the path currently drawn; the arrowhead appears only once the line is
 * complete, so a half-drawn edge never reads as a finished connection.
 */
export const Edge: React.FC<{
  d: string;
  reveal?: number;
  color?: string;
  width?: number;
  dashed?: boolean;
  /** Total path length. Approximate is fine — it only sets the dash offset. */
  length: number;
}> = ({ d, reveal = 1, color = colors.brass, width = 1.5, dashed = false, length }) => (
  <path
    d={d}
    fill="none"
    stroke={color}
    strokeWidth={width}
    strokeDasharray={dashed ? '5 5' : `${length} ${length}`}
    strokeDashoffset={dashed ? 0 : length * (1 - reveal)}
    opacity={dashed ? reveal : 1}
    markerEnd={reveal > 0.98 ? 'url(#arrowhead)' : undefined}
  />
);

export const Label: React.FC<{
  x: number;
  y: number;
  children: string;
  size?: number;
  color?: string;
  anchor?: 'start' | 'middle' | 'end';
  mono?: boolean;
  reveal?: number;
  weight?: number;
}> = ({ x, y, children, size = 14, color = colors.textSecondary, anchor = 'middle', mono = true, reveal = 1, weight = 500 }) => (
  <text
    x={x}
    y={y}
    textAnchor={anchor}
    dominantBaseline="middle"
    fill={color}
    fontFamily={mono ? fonts.mono : fonts.display}
    fontSize={size}
    fontWeight={weight}
    opacity={reveal}
  >
    {children}
  </text>
);

/** A reel-strip cell — the recurring unit in chapters 3 and 9. */
export const Cell: React.FC<{
  x: number;
  y: number;
  size: number;
  label: string;
  state?: 'plain' | 'lit' | 'ghost' | 'wrapped';
}> = ({ x, y, size, label, state = 'plain' }) => {
  const palette = {
    plain: { fill: colors.surface, stroke: colors.brassDim, text: colors.textSecondary },
    lit: { fill: colors.surfaceRaised, stroke: colors.brassBright, text: colors.brassBright },
    ghost: { fill: colors.surfaceDormant, stroke: colors.signalAsh, text: colors.textMuted },
    wrapped: { fill: colors.surfaceDormant, stroke: colors.signalTeal, text: colors.signalTeal },
  }[state];

  return (
    <g transform={`translate(${x} ${y})`}>
      <rect width={size} height={size} fill={palette.fill} stroke={palette.stroke} strokeWidth={1.5} />
      <text
        x={size / 2}
        y={size / 2}
        textAnchor="middle"
        dominantBaseline="middle"
        fill={palette.text}
        fontFamily={fonts.mono}
        fontSize={size * 0.42}
      >
        {label}
      </text>
    </g>
  );
};

/** A horizontal measure bar — used wherever two rates are compared. */
export const Bar: React.FC<{
  x: number;
  y: number;
  maxWidth: number;
  height: number;
  fraction: number;
  color: string;
  reveal?: number;
}> = ({ x, y, maxWidth, height, fraction, color, reveal = 1 }) => (
  <rect
    x={x}
    y={y}
    width={maxWidth * fraction * reveal}
    height={height}
    fill={color}
    opacity={0.85}
  />
);
