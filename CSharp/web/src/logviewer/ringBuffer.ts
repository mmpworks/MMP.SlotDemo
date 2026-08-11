// Ring-buffer trim: appends new items and drops the oldest past `cap`.
// A batched array copy rather than a true circular index: the caller replaces the
// whole reactive array on each flush regardless, so one concat plus at most one
// slice per flush is cheap even at the 25k cap. Callers always get a fresh array
// reference (immutable-update convention), never a mutated one.

export function appendWithCap<T>(existing: readonly T[], incoming: readonly T[], cap: number): T[] {
  if (incoming.length === 0) {
    return existing as T[]
  }
  const combined = incoming.length > 0 ? (existing as T[]).concat(incoming) : (existing as T[])
  return combined.length > cap ? combined.slice(combined.length - cap) : combined
}
