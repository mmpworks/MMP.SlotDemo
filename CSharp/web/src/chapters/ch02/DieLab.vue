<script setup lang="ts">
// The die-first rejection demo: enumerate EVERY raw value of a tiny source into
// B faces via the multiply trick, so the trim is visible case by case rather
// than sampled. Pure client-side math — the point is that the rejected set is a
// fixed function of (source size, face count), decided before any randomness.
import { computed, ref } from 'vue'

const faces = ref(6)
const bits = ref(5)

interface Row {
  raw: number
  product: number
  bin: number
  low: number
  rejected: boolean
  boundary: boolean
}

const space = computed(() => 2 ** bits.value)
const threshold = computed(() => space.value % faces.value)

const rows = computed<Row[]>(() => {
  const s = space.value
  const b = faces.value
  const t = threshold.value
  const out: Row[] = []
  for (let raw = 0; raw < s; raw++) {
    const product = raw * b
    const low = product % s
    out.push({
      raw,
      product,
      bin: Math.floor(product / s),
      low,
      rejected: low < t,
      boundary: low === t,
    })
  }
  return out
})

const beforeCounts = computed(() => {
  const c = Array<number>(faces.value).fill(0)
  for (const r of rows.value) c[r.bin]++
  return c
})

const afterCounts = computed(() => {
  const c = Array<number>(faces.value).fill(0)
  for (const r of rows.value) if (!r.rejected) c[r.bin]++
  return c
})

const rejectedRows = computed(() => rows.value.filter((r) => r.rejected))

// Split the enumeration into columns so 32-64 rows stay scannable.
const columns = computed(() => {
  const all = rows.value
  const perColumn = Math.ceil(all.length / Math.min(4, Math.ceil(all.length / 16)))
  const out: Row[][] = []
  for (let i = 0; i < all.length; i += perColumn) out.push(all.slice(i, i + perColumn))
  return out
})

// ---- the real thing: one 64-bit draw through Lemire's method, exact BigInt math ----

const TWO64 = 1n << 64n

const rawText = ref('13846071019375029842')
const bound64 = ref(26)

function randomRaw(): void {
  const a = new BigUint64Array(1)
  crypto.getRandomValues(a)
  rawText.value = a[0].toString()
}

const parsed = computed<bigint | null>(() => {
  try {
    const v = BigInt(rawText.value.trim())
    return v >= 0n && v < TWO64 ? v : null
  } catch {
    return null
  }
})

const lemire = computed(() => {
  const raw = parsed.value
  const b = bound64.value
  if (raw === null || !Number.isInteger(b) || b < 2) return null
  const bound = BigInt(b)
  const product = raw * bound
  const high = product >> 64n
  const low = product & (TWO64 - 1n)
  const threshold = TWO64 % bound
  return {
    raw,
    hex: '0x' + raw.toString(16).padStart(16, '0'),
    product,
    high,
    low,
    threshold,
    rejected: low < threshold,
  }
})
</script>

<template>
  <section class="lab">
    <h3>Lab 3 — Roll a fair die from raw bits</h3>
    <p class="lab__lede">
      Before sampling anything, watch the whole mechanism at once. A tiny source —
      every raw value from 0 to {{ space - 1 }} — is turned into {{ faces }} die faces by one
      multiply: the high part of <code>raw × {{ faces }}</code> is the face, the low part is
      how deep into that face's slice the value landed. {{ space }} values cannot split into
      {{ faces }} equal slices when {{ space }} mod {{ faces }} = {{ threshold }}, so
      {{ threshold }} slices hold one extra value. The fix: reject any row whose low part is
      below {{ threshold }} — the shallowest seats of the fat slices — and redraw. The
      rejected set is fixed before any randomness exists, and it cannot see outcomes.
    </p>

    <div class="controls">
      <label>
        Faces (die sides)
        <input v-model.number="faces" type="number" min="2" max="12" />
      </label>
      <label>
        Source bits
        <input v-model.number="bits" type="number" min="3" max="6" />
        <small>{{ space }} raw values</small>
      </label>
    </div>

    <div class="verdict">
      <div>
        <span class="verdict__label">Threshold</span>
        <span class="mono">{{ space }} mod {{ faces }} = {{ threshold }}</span>
      </div>
      <div>
        <span class="verdict__label">Faces before trim</span>
        <span class="mono warn">{{ beforeCounts.join(' / ') }}</span>
      </div>
      <div>
        <span class="verdict__label">Faces after trim</span>
        <span class="mono good">{{ afterCounts.join(' / ') }}</span>
      </div>
      <div>
        <span class="verdict__label">Rejected raws</span>
        <span class="mono">{{ threshold === 0 ? 'none — even split' : rejectedRows.map((r) => r.raw).join(', ') }}</span>
      </div>
    </div>

    <div class="tables">
      <table v-for="(column, c) in columns" :key="c" class="mono">
        <thead>
          <tr><th>raw</th><th>×{{ faces }}</th><th>face</th><th>low</th><th></th></tr>
        </thead>
        <tbody>
          <tr
            v-for="row in column"
            :key="row.raw"
            :class="{ 'row--rejected': row.rejected, 'row--boundary': row.boundary }"
          >
            <td>{{ row.raw }}</td>
            <td>{{ row.product }}</td>
            <td>{{ row.bin + 1 }}</td>
            <td>{{ row.low }}</td>
            <td>{{ row.rejected ? '✗ reject' : row.boundary ? 'first fair seat' : '' }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p class="note">
      The low column climbs by {{ faces }} each row and wraps at every face boundary. A wrap
      landing below {{ threshold }} marks a fat slice's extra value — those rows are the
      whole rejected set. The engine's <code>SpinRng.NextInt</code> does exactly this with
      2⁶⁴ raw values: for a 26-stop reel the threshold is 16, so the reject zone is 16
      positions out of 2⁶⁴ — about one redraw per 10¹⁸ draws.
    </p>

    <h4>The real thing: one 64-bit draw</h4>
    <p class="lab__lede">
      Now the production sizes, with exact arithmetic. Type any 64-bit number (or take a
      random one), pick a stop count, and watch the same multiply at full width: the
      128-bit product's high 64 bits are the stop, its low 64 bits face the threshold
      2⁶⁴ mod {{ bound64 }}.
    </p>

    <div class="controls">
      <label class="wide">
        Raw 64-bit value
        <input v-model="rawText" type="text" spellcheck="false" />
        <small v-if="lemire">{{ lemire.hex }}</small>
        <small v-else class="bad">enter a whole number from 0 to 2⁶⁴−1</small>
      </label>
      <label>
        Stops (bound)
        <input v-model.number="bound64" type="number" min="2" max="512" />
      </label>
      <button type="button" @click="randomRaw">Random</button>
    </div>

    <div v-if="lemire" class="walk mono">
      <div class="walk__row">
        <span class="walk__label">raw × {{ bound64 }}</span>
        <span>{{ lemire.product.toLocaleString() }}</span>
        <span class="walk__aside">a 128-bit product</span>
      </div>
      <div class="walk__row">
        <span class="walk__label">high 64 bits</span>
        <span class="good">{{ lemire.high.toLocaleString() }}</span>
        <span class="walk__aside">product ÷ 2⁶⁴ → the stop</span>
      </div>
      <div class="walk__row">
        <span class="walk__label">low 64 bits</span>
        <span>{{ lemire.low.toLocaleString() }}</span>
        <span class="walk__aside">position inside the stop's slice</span>
      </div>
      <div class="walk__row">
        <span class="walk__label">threshold</span>
        <span>{{ lemire.threshold.toLocaleString() }}</span>
        <span class="walk__aside">2⁶⁴ mod {{ bound64 }} — the leftover</span>
      </div>
      <div class="walk__row">
        <span class="walk__label">verdict</span>
        <span :class="lemire.rejected ? 'bad' : 'good'">
          {{ lemire.rejected
            ? `low < threshold → reject, redraw`
            : `low ≥ threshold → accept stop ${lemire.high.toLocaleString()}` }}
        </span>
        <span class="walk__aside">
          reject odds: {{ lemire.threshold.toLocaleString() }} in 2⁶⁴
        </span>
      </div>
    </div>

    <p class="note">
      Hunting for a rejection? With 26 stops only the raw values whose low half lands below
      16 redraw — 16 patterns out of 18,446,744,073,709,551,616. Clicking Random until one
      appears would take, on average, longer than the age of the universe. That is the
      whole trade: exact fairness, at a cost you cannot measure. The sampling lab below
      shrinks the draw space so the same arithmetic becomes visible again.
    </p>
  </section>
</template>

<style scoped>
.lab {
  border: var(--rule-hairline);
  padding: var(--space-lg);
  margin-bottom: var(--space-lg);
}

.lab h3 {
  margin: 0 0 var(--space-xs);
  font-size: 1.1rem;
}

.lab__lede {
  color: var(--color-text-secondary);
  max-width: 68ch;
  line-height: 1.6;
  font-size: 0.9rem;
}

.controls {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: var(--space-md);
  margin: var(--space-md) 0;
}

.controls label {
  display: grid;
  gap: 0.25rem;
  font-size: 0.78rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.controls small {
  text-transform: none;
  letter-spacing: 0;
  font-size: 0.7rem;
}

.controls input {
  font-family: var(--font-mono);
  font-size: 0.9rem;
  background: var(--color-surface);
  border: var(--rule-hairline);
  color: var(--color-text-primary);
  padding: 0.4rem 0.5rem;
  width: 9rem;
}

.controls .wide input {
  width: 22rem;
  max-width: 70vw;
}

.controls button {
  font-family: var(--font-display);
  letter-spacing: 0.2em;
  text-transform: uppercase;
  background: transparent;
  color: var(--color-accent);
  border: var(--rule-brass);
  padding: 0.55rem 1.6rem;
  cursor: pointer;
}

h4 {
  margin: var(--space-lg) 0 var(--space-xs);
  font-size: 0.95rem;
}

.walk {
  display: grid;
  gap: 0.35rem;
  padding: var(--space-sm) var(--space-md);
  background: var(--color-surface);
  border-left: 3px solid var(--color-accent);
  font-size: 0.82rem;
}

.walk__row {
  display: grid;
  grid-template-columns: 9rem minmax(0, auto) 1fr;
  gap: var(--space-md);
  align-items: baseline;
}

.walk__row > span:nth-child(2) {
  overflow-wrap: anywhere;
}

.walk__label {
  font-family: var(--font-display);
  font-size: 0.68rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.walk__aside {
  color: var(--color-text-muted);
  font-size: 0.72rem;
}

.bad {
  color: var(--color-status-error);
}

.verdict {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-lg);
  padding: var(--space-sm) var(--space-md);
  background: var(--color-surface);
  border-left: 3px solid var(--color-accent);
  margin-bottom: var(--space-md);
}

.verdict div {
  display: grid;
  gap: 0.2rem;
}

.verdict__label {
  font-family: var(--font-display);
  font-size: 0.68rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.mono {
  font-family: var(--font-mono);
}

.warn {
  color: var(--color-log-warning);
}

.good {
  color: var(--color-status-installed);
}

.tables {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-lg);
  align-items: flex-start;
}

.tables table {
  border-collapse: collapse;
  font-size: 0.78rem;
}

.tables th {
  text-align: left;
  color: var(--color-text-muted);
  font-weight: normal;
  padding: 0.15rem 0.6rem 0.25rem 0;
  border-bottom: var(--rule-hairline);
}

.tables td {
  padding: 0.12rem 0.6rem 0.12rem 0;
  color: var(--color-text-secondary);
}

.row--rejected td {
  color: var(--color-log-warning);
}

.row--boundary td {
  color: var(--color-status-installed);
}

.note {
  color: var(--color-text-secondary);
  font-size: 0.82rem;
  line-height: 1.55;
  max-width: 70ch;
  margin-top: var(--space-md);
}
</style>
