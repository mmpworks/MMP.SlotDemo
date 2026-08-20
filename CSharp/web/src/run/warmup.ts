/**
 * Whether a finished run's throughput reads as a warm-up rather than the engine's real
 * speed.
 *
 * The first runs after the server starts are slower because .NET compiles the spin loop on
 * first use and the tiered compiler re-optimizes it once it has been hit enough times. On
 * this machine a first run reads around 12M spins/s and settles above 130M by the third,
 * with identical results each time: same spins, same measured RTP, same verdict. Only the
 * clock differs, so a warm-up run is honest about the math and misleading about the engine.
 */

/**
 * Below this, a completed run is treated as still warming up. Set from the observed
 * settled range on developer hardware, where warm runs land at 130M and above and cold
 * ones an order of magnitude lower, so the gap is wide enough that one threshold separates
 * them without tuning.
 */
export const WARMED_SPINS_PER_SECOND = 100_000_000

/**
 * A run is only judged once it has finished; a run still in flight has not had the chance
 * to reach its rate yet, and a rate of zero means nothing has been measured at all.
 */
export function isWarmupRun(status: string | undefined, spinsPerSecond: number): boolean {
  if (status !== 'completed') return false
  if (!Number.isFinite(spinsPerSecond) || spinsPerSecond <= 0) return false
  return spinsPerSecond < WARMED_SPINS_PER_SECOND
}
