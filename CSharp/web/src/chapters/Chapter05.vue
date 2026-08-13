<script setup lang="ts">
import { computed, ref } from 'vue'
import ComprehensionCheck from '../components/ComprehensionCheck.vue'
import OptimizationPreview from '../components/OptimizationPreview.vue'

defineProps<{ title: string; blurb: string }>()

const reel1 = [
  { symbol: 'Cherry', count: 2 },
  { symbol: 'Bell', count: 1 },
]
const reel2 = [
  { symbol: 'Cherry', count: 1 },
  { symbol: 'Bell', count: 1 },
]
const reel3 = [
  { symbol: 'Cherry', count: 3 },
  { symbol: 'Bell', count: 1 },
]

const pick1 = ref(0)
const pick2 = ref(0)
const pick3 = ref(0)
const weight = computed(() => reel1[pick1.value].count * reel2[pick2.value].count * reel3[pick3.value].count)
const combination = computed(() => [reel1[pick1.value], reel2[pick2.value], reel3[pick3.value]])
const isThreeCherries = computed(() => combination.value.every((item) => item.symbol === 'Cherry'))

const allRows = computed(() => {
  const rows: Array<{ symbols: string; weight: number }> = []
  for (const a of reel1)
    for (const b of reel2)
      for (const c of reel3)
        rows.push({ symbols: `${a.symbol} / ${b.symbol} / ${c.symbol}`, weight: a.count * b.count * c.count })
  return rows
})
const totalWeight = computed(() => allRows.value.reduce((sum, row) => sum + row.weight, 0))
</script>

<template>
  <article>
    <header class="chapter-head">
      <h2>{{ title }}</h2>
      <p class="chapter-blurb">{{ blurb }}</p>
    </header>

    <section class="chapter-brief">
      <h3>Start with a smaller problem</h3>
      <p>
        These three reels have 3, 2, and 4 stops. Checking every stop would require
        3 × 2 × 4 = 24 outcomes. Repeated symbols let us do less work without estimating.
      </p>
      <p>
        Think of sorting a jar of coins. You can count each coin separately, or group the
        quarters and multiply their count by 25 cents. This lab groups identical reel symbols.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — Build one weighted outcome</h3>
      <p class="lab__lede">
        Choose one symbol from each reel. The number beside it says how many stops show that
        symbol. Multiply the three counts to find how many physical outcomes your choice represents.
      </p>

      <div class="weight-picker">
        <label>Reel 1
          <select v-model.number="pick1"><option v-for="(x, i) in reel1" :key="x.symbol" :value="i">{{ x.symbol }} ({{ x.count }})</option></select>
        </label>
        <span>×</span>
        <label>Reel 2
          <select v-model.number="pick2"><option v-for="(x, i) in reel2" :key="x.symbol" :value="i">{{ x.symbol }} ({{ x.count }})</option></select>
        </label>
        <span>×</span>
        <label>Reel 3
          <select v-model.number="pick3"><option v-for="(x, i) in reel3" :key="x.symbol" :value="i">{{ x.symbol }} ({{ x.count }})</option></select>
        </label>
      </div>

      <div class="verdict verdict--info">
        <div><span class="verdict__label">Combination</span><span>{{ combination.map(x => x.symbol).join(' / ') }}</span></div>
        <div><span class="verdict__label">Weight</span><span class="mono">{{ combination.map(x => x.count).join(' × ') }} = {{ weight }}</span></div>
        <div><span class="verdict__label">Result</span><span>{{ isThreeCherries ? 'Three-cherry win' : 'No three-cherry win' }}</span></div>
      </div>

      <ComprehensionCheck
        question="Why does Cherry / Cherry / Cherry have a weight of 6?"
        :choices="['It pays six credits.', 'Six physical stop combinations show those three symbols.', 'The analyzer ran six random spins.']"
        :answer="1"
        explanation="The cherries appear 2, 1, and 3 times. Multiplying 2 × 1 × 3 gives six physical stop combinations."
      />
    </section>

    <section class="lab">
      <h3>Lab 2 — Prove that no outcomes disappeared</h3>
      <p class="lab__lede">
        There are eight distinct symbol combinations. Their weights should add back to the
        original 24 physical stop combinations.
      </p>
      <table class="lab-table">
        <thead><tr><th>Symbol combination</th><th>Physical outcomes represented</th></tr></thead>
        <tbody><tr v-for="row in allRows" :key="row.symbols"><td>{{ row.symbols }}</td><td>{{ row.weight }}</td></tr></tbody>
        <tfoot><tr><th>Total</th><th>{{ totalWeight }}</th></tr></tfoot>
      </table>

      <ComprehensionCheck
        question="The eight weights add to 24. What does that prove?"
        :choices="['Every physical stop outcome is still counted.', 'The game has a 24% RTP.', 'Every symbol is equally likely.']"
        :answer="0"
        explanation="Grouping repeated symbols changed the amount of work, not the outcomes being counted."
      />
    </section>

    <section class="chapter-brief">
      <h3>How this becomes `GameAnalyzer.Descend`</h3>
      <p>
        The method fills one reel position, multiplies the running weight, and calls itself for
        the next reel. When every reel has a symbol, it evaluates the completed payline once.
        The production analyzer uses the same steps as this table, with more reels and symbols.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Games/GameAnalyzer.cs</code>.
      </p>
    </section>
    <OptimizationPreview
      question="Where does weighted enumeration spend its time after branch count shrinks?"
      later="The grouping algorithm is the first optimization. Keep the independent exhaustive check, then profile recursion, evaluation, and accumulation separately."
    />
  </article>
</template>

<style scoped>
.weight-picker { display: flex; align-items: end; gap: 0.75rem; flex-wrap: wrap; margin: 1rem 0; }
.weight-picker label { display: grid; gap: 0.3rem; }
tfoot { border-top: 2px solid var(--color-border); }
</style>
