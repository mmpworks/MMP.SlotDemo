import React from 'react';
import { colors } from '../tokens';

/**
 * Reel glyphs for the title sequence, drawn as SVG so a clean checkout renders
 * identical frames with no image assets to fetch. Flat brass shapes with a
 * single darker facet each — the era's poster art read at a distance, which is
 * what a reel symbol has to do while it is moving.
 *
 * These are title-sequence ornament. They are deliberately not the Orca Dive
 * symbol set: the opener fronts the whole series, not one game.
 */

export type GlyphName = 'gem' | 'seven' | 'bar' | 'bell' | 'star' | 'coin';

export const GLYPH_ORDER: GlyphName[] = ['gem', 'seven', 'bar', 'bell', 'star', 'coin'];

export const Glyph: React.FC<{ name: GlyphName; size: number; dim?: boolean }> = ({
  name,
  size,
  dim = false,
}) => {
  const fill = dim ? colors.brassDim : colors.brass;
  const facet = dim ? '#4d3f24' : colors.brassBright;

  return (
    <svg width={size} height={size} viewBox="0 0 100 100" style={{ display: 'block' }}>
      {SHAPES[name](fill, facet)}
    </svg>
  );
};

const SHAPES: Record<GlyphName, (fill: string, facet: string) => React.ReactNode> = {
  gem: (fill, facet) => (
    <g>
      <polygon points="50,12 82,38 50,88 18,38" fill={fill} />
      <polygon points="50,12 82,38 50,38" fill={facet} />
      <polygon points="18,38 82,38 50,88" fill={fill} opacity={0.72} />
      <line x1="18" y1="38" x2="82" y2="38" stroke={facet} strokeWidth="2" />
    </g>
  ),
  seven: (fill, facet) => (
    <g>
      <polygon points="22,18 78,18 78,30 50,86 34,86 60,32 22,32" fill={fill} />
      <polygon points="22,18 78,18 78,26 22,26" fill={facet} />
    </g>
  ),
  bar: (fill, facet) => (
    <g>
      <rect x="16" y="30" width="68" height="16" rx="3" fill={fill} />
      <rect x="16" y="54" width="68" height="16" rx="3" fill={fill} opacity={0.78} />
      <rect x="16" y="30" width="68" height="5" rx="2" fill={facet} />
    </g>
  ),
  bell: (fill, facet) => (
    <g>
      <path d="M50 16 C34 16 28 30 28 46 C28 60 22 66 20 72 L80 72 C78 66 72 60 72 46 C72 30 66 16 50 16 Z" fill={fill} />
      <ellipse cx="50" cy="80" rx="9" ry="7" fill={fill} />
      <path d="M50 16 C40 16 34 24 32 34 C38 26 44 22 50 22 Z" fill={facet} />
    </g>
  ),
  star: (fill, facet) => (
    <g>
      <polygon
        points="50,10 60,38 90,38 66,56 75,86 50,68 25,86 34,56 10,38 40,38"
        fill={fill}
      />
      <polygon points="50,10 60,38 50,38" fill={facet} />
    </g>
  ),
  coin: (fill, facet) => (
    <g>
      <circle cx="50" cy="50" r="34" fill={fill} />
      <circle cx="50" cy="50" r="24" fill="none" stroke={facet} strokeWidth="3" />
      <path d="M50 16 A34 34 0 0 1 84 50 L74 50 A24 24 0 0 0 50 26 Z" fill={facet} opacity={0.85} />
    </g>
  ),
};
