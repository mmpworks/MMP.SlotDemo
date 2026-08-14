import React from 'react';
import { AbsoluteFill } from 'remotion';
import { colors } from '../tokens';

/**
 * The radiating brass rays behind the lockup — the era's poster device for
 * "this is the thing you came to see." Rays widen from the center and fade
 * out well before the frame edge, so they never compete with the type.
 *
 * `reveal` grows the rays outward; `spin` rotates the whole fan by a few
 * degrees, which is enough to feel alive without drawing attention.
 */
const RAY_COUNT = 24;

export const Sunburst: React.FC<{ reveal: number; spin: number; opacity?: number }> = ({
  reveal,
  spin,
  opacity = 1,
}) => {
  if (reveal <= 0) return null;

  return (
    <AbsoluteFill style={{ alignItems: 'center', justifyContent: 'center', opacity }}>
      <svg
        width={3600}
        height={3600}
        viewBox="-100 -100 200 200"
        style={{ transform: `rotate(${spin}deg)` }}
      >
        <defs>
          <radialGradient id="rayFade" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor={colors.brass} stopOpacity="0" />
            <stop offset="14%" stopColor={colors.brass} stopOpacity="0.30" />
            <stop offset="46%" stopColor={colors.brass} stopOpacity="0.14" />
            <stop offset="100%" stopColor={colors.brass} stopOpacity="0" />
          </radialGradient>
          <mask id="rayReach">
            {/* Mask channel, not a color — white means "show this much". */}
            <circle cx="0" cy="0" r={100 * reveal} fill="#fff" />
          </mask>
        </defs>
        <g mask="url(#rayReach)">
          {Array.from({ length: RAY_COUNT }, (_, i) => {
            const a = (i * 360) / RAY_COUNT;
            const half = 360 / RAY_COUNT / 2.6;
            return (
              <path
                key={i}
                d={wedge(a - half, a + half, 100)}
                fill="url(#rayFade)"
              />
            );
          })}
        </g>
      </svg>
    </AbsoluteFill>
  );
};

function wedge(fromDeg: number, toDeg: number, r: number): string {
  const a = polar(fromDeg, r);
  const b = polar(toDeg, r);
  return `M 0 0 L ${a.x} ${a.y} A ${r} ${r} 0 0 1 ${b.x} ${b.y} Z`;
}

function polar(deg: number, r: number): { x: number; y: number } {
  const rad = (deg * Math.PI) / 180;
  return { x: r * Math.cos(rad), y: r * Math.sin(rad) };
}
