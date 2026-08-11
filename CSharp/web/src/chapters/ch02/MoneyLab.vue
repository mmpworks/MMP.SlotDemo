<script setup lang="ts">
import { ref } from 'vue'
import { runMoneyLab, type MoneyResponse } from '../../api/chapters'
import BitStrip from '../../components/BitStrip.vue'

const wagerCredits = ref(1)
// 1.10× by default: 1.1 has no exact binary form, so the double column drifts on camera.
// 2.25× is the counter-example worth trying live — it is exact in binary and drift stays 0.
const scaledMultiplier = ref(110)
const repeats = ref(1_000_000)
// 0 means "use the credit wager above"; any other value goes in raw.
const wagerMillicents = ref(0)

const result = ref<MoneyResponse | null>(null)
const error = ref('')
const busy = ref(false)

async function run(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    result.value = await runMoneyLab(
      wagerCredits.value,
      scaledMultiplier.value,
      repeats.value,
      wagerMillicents.value,
    )
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Money lab failed.'
  } finally {
    busy.value = false
  }
}

// 100,000 millicents to the credit, so the low 17 bits are all sub-credit resolution.
const FRACTION_BITS = 17

function formatCredits(value: number): string {
  return value.toLocaleString(undefined, { maximumFractionDigits: 8 })
}
</script>

<template>
  <section class="lab">
    <h3>Lab 1 — Money as an integer</h3>
    <p class="lab__lede">
      A wager goes in as credits, comes back as a count of millicents, and the same total is
      then computed a second way in <code>double</code>. Push the repeat count up and watch
      only one of the two answers stay put.
    </p>

    <div class="controls">
      <label>
        Wager (credits)
        <input v-model.number="wagerCredits" type="number" min="1" step="1" />
      </label>
      <label>
        Pay multiplier ×100
        <input v-model.number="scaledMultiplier" type="number" min="0" step="5" />
        <small>225 means 2.25×</small>
      </label>
      <label>
        Raw wager (millicents)
        <input v-model.number="wagerMillicents" type="number" min="0" step="1" />
        <small>0 uses the credits above; try 12345</small>
      </label>
      <label>
        Repeats
        <input v-model.number="repeats" type="number" min="1" step="100000" />
      </label>
      <button type="button" :disabled="busy" @click="run">
        {{ busy ? 'Running…' : 'Run' }}
      </button>
    </div>

    <p v-if="error" class="lab__error">{{ error }}</p>

    <div v-if="result" class="results">
      <div v-if="result.refusal" class="refusal">
        <strong>The type refused the conversion.</strong>
        <p>{{ result.refusal }}</p>
      </div>

      <table class="amounts">
        <thead>
          <tr>
            <th>Amount</th>
            <th>Raw millicents</th>
            <th>Displayed</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>Wager</td>
            <td class="mono">{{ result.wager.millicents.toLocaleString() }}</td>
            <td class="mono">{{ result.wager.display }}</td>
          </tr>
          <tr>
            <td>Pay</td>
            <td class="mono">{{ result.pay.millicents.toLocaleString() }}</td>
            <td class="mono">{{ result.pay.display }}</td>
          </tr>
          <tr>
            <td>Total over {{ repeats.toLocaleString() }}</td>
            <td class="mono">{{ result.exactTotal.millicents.toLocaleString() }}</td>
            <td class="mono">{{ result.exactTotal.display }}</td>
          </tr>
        </tbody>
      </table>

      <div class="bits">
        <BitStrip :bits="result.wager.bits" :fraction-bits="FRACTION_BITS" label="wager, 64 bits" />
        <BitStrip :bits="result.pay.bits" :fraction-bits="FRACTION_BITS" label="pay, 64 bits" />
        <p class="bits__note">
          Outlined bits sit below one credit. Nothing here is a mantissa and an exponent —
          it is one number, and every bit of it is money.
        </p>
      </div>

      <div class="verdict" :class="result.floatAgrees ? 'verdict--clean' : 'verdict--drift'">
        <div>
          <span class="verdict__label">Integer total</span>
          <span class="mono">{{ formatCredits(result.exactTotal.credits) }} cr</span>
        </div>
        <div>
          <span class="verdict__label">Double total</span>
          <span class="mono">{{ formatCredits(result.floatTotal) }} cr</span>
        </div>
        <div>
          <span class="verdict__label">Drift</span>
          <span class="mono">{{ result.driftCredits.toExponential(4) }} cr</span>
        </div>
      </div>

      <div class="assoc">
        <span class="verdict__label">Grouping changes a double sum</span>
        <p class="mono">(0.1 + 0.2) + 0.3 = {{ result.associativeLeft }}</p>
        <p class="mono">0.1 + (0.2 + 0.3) = {{ result.associativeRight }}</p>
        <p class="assoc__note">
          Same three numbers, two groupings, two answers. Workers finish in whatever order
          the scheduler hands out, so a double-based total carries a race condition inside
          its arithmetic. Integer addition has no such opinion, which is what makes an
          N-worker run match a 1-worker run bit for bit.
        </p>
      </div>
    </div>
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
  width: 11rem;
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

.lab__error {
  color: var(--color-status-error);
  font-family: var(--font-mono);
  font-size: 0.8rem;
}

.results {
  display: grid;
  gap: var(--space-md);
}

.refusal {
  border-left: 3px solid var(--color-log-warning);
  padding-left: var(--space-sm);
}

.refusal p {
  color: var(--color-text-secondary);
  font-size: 0.85rem;
  line-height: 1.55;
}

.amounts {
  border-collapse: collapse;
  width: 100%;
  font-size: 0.85rem;
}

.amounts th {
  text-align: left;
  font-family: var(--font-display);
  font-size: 0.72rem;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: var(--color-text-muted);
  padding-bottom: 0.35rem;
  border-bottom: var(--rule-hairline);
}

.amounts td {
  padding: 0.35rem 0.75rem 0.35rem 0;
  border-bottom: var(--rule-hairline);
}

.mono {
  font-family: var(--font-mono);
}

.bits {
  display: grid;
  gap: var(--space-sm);
}

.bits__note,
.assoc__note {
  color: var(--color-text-secondary);
  font-size: 0.82rem;
  line-height: 1.55;
  max-width: 68ch;
}

.verdict {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-lg);
  padding: var(--space-sm) var(--space-md);
  background: var(--color-surface);
  border-left: 3px solid var(--color-status-installed);
}

.verdict--drift {
  border-left-color: var(--color-log-warning);
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

.assoc {
  display: grid;
  gap: 0.2rem;
}

.assoc p.mono {
  font-size: 0.85rem;
  margin: 0;
}
</style>
