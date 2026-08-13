import { Easing, interpolate, useCurrentFrame } from 'remotion';
import { easing } from '../tokens';

const CURVES = {
  out: Easing.bezier(...easing.out),
  inOut: Easing.bezier(...easing.inOut),
  in: Easing.bezier(...easing.in),
} as const;

export type Curve = keyof typeof CURVES;

/**
 * Progress from 0 to 1 across `durationInFrames`, starting at `delay`, eased.
 * Clamped at both ends so a value read before the start or after the end is a
 * held state rather than an extrapolation.
 */
export function useProgress(delay: number, durationInFrames: number, curve: Curve = 'out'): number {
  const frame = useCurrentFrame();
  return interpolate(frame, [delay, delay + durationInFrames], [0, 1], {
    easing: CURVES[curve],
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
}

/** Eased progress that is not tied to the current frame — for nested timelines. */
export function progressAt(
  frame: number,
  delay: number,
  durationInFrames: number,
  curve: Curve = 'out',
): number {
  return interpolate(frame, [delay, delay + durationInFrames], [0, 1], {
    easing: CURVES[curve],
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
}

/**
 * Rises to 1 and returns to 0 — for a beat that flashes and settles, such as a
 * counter accepting a batch. Symmetric, so the emphasis reads as one motion.
 */
export function pulseAt(frame: number, at: number, durationInFrames: number): number {
  const half = durationInFrames / 2;
  if (frame < at || frame > at + durationInFrames) return 0;
  return frame < at + half
    ? progressAt(frame, at, half, 'out')
    : 1 - progressAt(frame, at + half, half, 'inOut');
}

/** Seconds to frames at the project rate. Keeps timing tables readable. */
export function sec(seconds: number, fps: number): number {
  return Math.round(seconds * fps);
}
