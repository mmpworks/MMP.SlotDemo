import React from 'react';
import { colors, tracking } from '../tokens';
import { fonts } from '../tokens/fonts';

/**
 * The three type roles used across decks and title cards. Sizes are passed in
 * by the caller because a 4-second card and a slide heading are the same role
 * at different scales — the role fixes family, tracking, and color, not size.
 */

export const Kicker: React.FC<{
  children: React.ReactNode;
  size?: number;
  color?: string;
  style?: React.CSSProperties;
}> = ({ children, size = 26, color = colors.brass, style }) => (
  <div
    style={{
      fontFamily: fonts.display,
      fontWeight: 500,
      fontSize: size,
      letterSpacing: tracking.kicker,
      textTransform: 'uppercase',
      color,
      whiteSpace: 'nowrap',
      ...style,
    }}
  >
    {children}
  </div>
);

export const Display: React.FC<{
  children: React.ReactNode;
  size: number;
  color?: string;
  style?: React.CSSProperties;
}> = ({ children, size, color = colors.textPrimary, style }) => (
  <div
    style={{
      fontFamily: fonts.display,
      fontWeight: 700,
      fontSize: size,
      lineHeight: 1.02,
      letterSpacing: tracking.display,
      color,
      ...style,
    }}
  >
    {children}
  </div>
);

export const Body: React.FC<{
  children: React.ReactNode;
  size?: number;
  color?: string;
  style?: React.CSSProperties;
}> = ({ children, size = 34, color = colors.textPrimary, style }) => (
  <div
    style={{
      fontFamily: fonts.body,
      fontWeight: 400,
      fontSize: size,
      lineHeight: 1.42,
      color,
      ...style,
    }}
  >
    {children}
  </div>
);

export const Mono: React.FC<{
  children: React.ReactNode;
  size?: number;
  color?: string;
  style?: React.CSSProperties;
}> = ({ children, size = 30, color = colors.signalTeal, style }) => (
  <span
    style={{
      fontFamily: fonts.mono,
      fontWeight: 500,
      fontSize: size,
      color,
      ...style,
    }}
  >
    {children}
  </span>
);

/**
 * A brass hairline that draws from its left edge. `reveal` is the fraction of
 * its full width currently drawn, so a rule can lead a heading in.
 */
export const Rule: React.FC<{ width: number; reveal?: number; color?: string; height?: number }> = ({
  width,
  reveal = 1,
  color = colors.brass,
  height = 2,
}) => (
  <div
    style={{
      width: width * reveal,
      height,
      backgroundColor: color,
      flexShrink: 0,
    }}
  />
);
