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
        This chapter uses more math than the earlier reel lessons because one window may pay
        several lines and start a bonus. The formulas are standard probability tools. Each
        answers one plain question: What is the average? How swingy are the results? Which
        awards happen together? The article shows the production C# beside each calculation.
      </p>
      <p>
        Orca Dive has 14,781,416 stop combinations. Playing every one would work, but a
        Salmon symbol that appears at several stops on one reel produces the same payline
        symbol each time. The production analyzer groups those repeated Salmon stops.
      </p>
      <p>
        This lab uses a separate, hand-built teaching game containing only Cherry and Bell.
        It is not Orca Dive and it has no bonus. Its three reels have 3, 2, and 4 stops.
        Checking every stop would require
        3 × 2 × 4 = 24 outcomes. Repeated symbols let us do less work without estimating.
      </p>
      <p>
        Think of sorting a jar of coins. You can count each coin separately, or group the
        quarters and multiply their count by 25 cents. This lab groups identical reel symbols.
      </p>
    </section>

    <section class="chapter-brief">
      <h3>The payout table and the analyzer have different jobs</h3>
      <p>
        During game preparation, <code>WinningOutcomeTable</code> checks each complete reel
        window. It records the total line multiplier, every winning payline, and every
        triggered feature. <code>ProgressiveOutcomeTable</code> arranges those answers by
        reel-stop prefix so the spin loop can find one quickly.
      </p>
      <p>
        A spin draws every reel stop first. The lookup then narrows through the prepared
        table using reel 1, reel 2, reel 3, and so on. <code>GameAnalyzer</code> has a different
        job: it totals all possible outcomes to calculate exact RTP and variance before the
        random simulation begins.
      </p>
      <table class="lab-table">
        <thead><tr><th>Component</th><th>Source file</th><th>Produces</th><th>Used for</th></tr></thead>
        <tbody>
          <tr><td>WinningOutcomeTable.Build()</td><td><code>Games/WinningOutcomeTable.cs</code></td><td>Complete window results</td><td>Prepared payout data</td></tr>
          <tr><td>ProgressiveOutcomeTable.Build()</td><td><code>Games/ProgressiveOutcomeTable.cs</code></td><td>Reel-by-reel narrowing arrays</td><td>Fast spin lookup</td></tr>
          <tr><td>GameAnalyzer.Analyze()</td><td><code>Games/GameAnalyzer.cs</code></td><td>RTP, variance, and frequencies</td><td>Checking the simulation</td></tr>
        </tbody>
      </table>
      <p class="lab-note">
        The table builder is the static <code>Build()</code> method inside
        <code>WinningOutcomeTable.cs</code>. It is created lazily by the
        <code>WinningOutcomes</code> property in <code>Games/Definition/GameDefinition.cs</code>.
      </p>
      <p>
        When a run starts, the server stores the analyzer's RTP and sigma with that active
        run. Each simulation checkpoint reports measured RTP. The convergence recorder uses
        the stored sigma to build a 99% band and checks whether measured RTP is inside it.
        Chapter 8 shows this as the referee lab, and the Finale plots the same comparison.
      </p>
      <p class="lab-note">
        Sigma is the standard deviation of one spin's return. In plain language, it is the
        game's swinginess ruler. It does not decide a payout; it tells the validation code
        how much measured RTP may reasonably move around the exact RTP after a given number
        of spins.
      </p>
    </section>

    <section class="chapter-brief">
      <h3>One window can produce two kinds of awards</h3>
      <p>
        A spin chooses one stop for each reel and builds one visible window. Every payline
        reads that window. A bonus rule can inspect the same window for scatter symbols.
        A line win does not block the bonus: the same spin can pay a line award and trigger
        PenguinBonus, and the engine adds both awards.
      </p>
      <table class="lab-table">
        <thead>
          <tr><th>Visible position</th><th>Reel 1</th><th>Reel 2</th><th>Reel 3</th><th>Reel 4</th><th>Reel 5</th></tr>
        </thead>
        <tbody>
          <tr><th>Top</th><td>Green7</td><td>Squid</td><td>Green7</td><td>Mackerel</td><td>Red7</td></tr>
          <tr><th>Center payline</th><td><strong>Blue7</strong></td><td><strong>Blue7</strong></td><td><strong>Blue7</strong></td><td><strong>Seal</strong></td><td>Blue7</td></tr>
          <tr><th>Bottom</th><td><strong>Penguin</strong></td><td>Herring</td><td><strong>Penguin</strong></td><td>Squid</td><td><strong>Penguin</strong></td></tr>
        </tbody>
      </table>
      <p class="lab-note">
        Read across the center: three Blue7 symbols pay before Seal ends the run. Then check
        reels 1, 3, and 5: each shows Penguin, so the bonus triggers on the same spin.
      </p>
      <p>
        Orca Dive has one payline. The next fixture shows how the same engine handles two.
      </p>
    </section>

    <section class="chapter-brief">
      <h3>Two-Line Tide: two paylines and one bonus</h3>
      <p>
        This small loaded game has three reels with four stops each. At stop 0 on every
        reel, one window pays both lines and triggers the bonus:
      </p>
      <table class="lab-table">
        <thead><tr><th>Visible position</th><th>Reel 1</th><th>Reel 2</th><th>Reel 3</th><th>Result</th></tr></thead>
        <tbody>
          <tr><th>Top payline</th><td><strong>Pearl</strong></td><td><strong>Pearl</strong></td><td><strong>Pearl</strong></td><td>5× wager</td></tr>
          <tr><th>Center payline</th><td><strong>Shell</strong></td><td><strong>Shell</strong></td><td><strong>Shell</strong></td><td>3× wager</td></tr>
          <tr><th>Bottom</th><td><strong>Starfish</strong></td><td>Starfish</td><td><strong>Starfish</strong></td><td>Bonus trigger</td></tr>
        </tbody>
      </table>
      <p class="lab-note">
        The lookup returns an 8× combined line multiplier, both payline names, and
        StarfishBonus. The bonus then pays either 0× or 2×, so this spin returns either
        8× or 10× the wager.
      </p>
      <p>
        The exact math checks all <code>4 × 4 × 4 = 64</code> windows. Line awards total
        16 wager units, giving 25% line RTP. StarfishBonus contributes 56.25%, for 81.25%
        total RTP. For variance, the analyzer squares each window's combined line award.
        The window above contributes <code>8² = 64</code>, which keeps the relationship
        between the two line wins.
      </p>
      <p class="lab-note">
        Squaring stops low and high results from canceling and makes unusually large payouts
        count more. Taking the square root at the end produces sigma in ordinary wager units.
        It is like measuring every result's distance from the average, then turning those
        distances into one useful swinginess number.
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
        explanation="Grouping repeated symbols reduces the work while the weights keep every physical outcome in the count."
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
