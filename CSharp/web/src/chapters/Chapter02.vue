<script setup lang="ts">
import MoneyLab from './ch02/MoneyLab.vue'
import RngLab from './ch02/RngLab.vue'
import DieLab from './ch02/DieLab.vue'
import BiasLab from './ch02/BiasLab.vue'
import ComprehensionCheck from '../components/ComprehensionCheck.vue'
import OptimizationPreview from '../components/OptimizationPreview.vue'

defineProps<{ title: string; blurb: string }>()
</script>

<template>
  <article class="chapter">
    <header class="chapter__head">
      <h2>{{ title }}</h2>
      <p class="chapter__blurb">{{ blurb }}</p>
    </header>

    <section class="chapter__brief">
      <h3>What the episode builds</h3>
      <p>
        Type <code>0.1 + 0.2 == 0.3</code> into any C# REPL and it returns <code>false</code>.
        A slot simulator that counts millions of spins cannot afford that kind of drift, so
        this episode fixes it with a unit change instead of a rounding trick. Two small types
        carry the guarantees the rest of the simulator depends on.
        <code>Millicents</code> keeps money as a count of integers so totals stay exact and
        order-independent. <code>SpinRng</code> gives every worker its own seeded stream so a
        run can be replayed. Because these two types are strict, the code that uses them can
        be ordinary.
      </p>
      <dl class="invariants">
        <div>
          <dt>M1</dt>
          <dd>Money stays integer end to end. <code>Millicents</code> offers no conversion to <code>double</code>, so the rule holds at compile time rather than by discipline.</dd>
        </div>
        <div>
          <dt>M2</dt>
          <dd>Integer addition is order-independent, which is what lets an N-worker total match a 1-worker total bit for bit.</dd>
        </div>
        <div>
          <dt>R3</dt>
          <dd>Randomness travels as a <code>ref</code> parameter. Nothing reaches for an ambient generator, so every consumer of randomness declares it in its signature.</dd>
        </div>
      </dl>
      <p class="chapter__source">
        Source:
        <code>src/MMP.SlotGame.Core/Money/Millicents.cs</code> and
        <code>src/MMP.SlotGame.Core/Simulation/SpinRng.cs</code>. The labs below run copies of
        those files, served from
        <code>CSharp/src/SlotDemo.Server/Chapters/</code>.
      </p>
    </section>

    <MoneyLab />
    <RngLab />
    <DieLab />
    <BiasLab />

    <section class="chapter__next">
      <h3>Carried into episode 3</h3>
      <p>
        The stop index at the end of lab 2 is the handoff. Episode 3 takes those numbers and
        asks what a reel is, and why a strip of positions behaves differently from a weighted
        die even when the two look identical on a spec sheet.
      </p>
    </section>
    <ComprehensionCheck
      question="Why does the engine store money as whole millicents?"
      :choices="['To make payouts look larger.', 'So repeated addition stays exact.', 'Because random numbers require integers.']"
      :answer="1"
      explanation="Whole-number addition does not collect the small rounding errors produced by binary floating point."
    />
    <OptimizationPreview
      question="Can bounded random selection move constant work out of the spin loop?"
      later="Reel lengths become stable construction-time data. Finish and test the RNG first; episode 9 measures precomputed Lemire ranges and rejection thresholds."
    />
  </article>
</template>

<style scoped>
.chapter__head h2 {
  margin: 0 0 var(--space-xs);
  font-size: 1.5rem;
}

.chapter__blurb {
  color: var(--color-text-secondary);
  max-width: 70ch;
  line-height: 1.6;
  margin-bottom: var(--space-lg);
}

.chapter__brief,
.chapter__next {
  margin-bottom: var(--space-lg);
}

.chapter__brief h3,
.chapter__next h3 {
  font-size: 1rem;
  margin: 0 0 var(--space-xs);
}

.chapter__brief p,
.chapter__next p {
  color: var(--color-text-secondary);
  max-width: 70ch;
  line-height: 1.65;
  font-size: 0.92rem;
}

.invariants {
  display: grid;
  gap: var(--space-sm);
  margin: var(--space-md) 0;
}

.invariants > div {
  display: grid;
  grid-template-columns: 3rem 1fr;
  gap: var(--space-sm);
  align-items: baseline;
}

.invariants dt {
  font-family: var(--font-display);
  letter-spacing: 0.16em;
  color: var(--color-accent);
  font-size: 0.85rem;
}

.invariants dd {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: 0.88rem;
  line-height: 1.55;
  max-width: 66ch;
}

.chapter__source {
  font-size: 0.82rem !important;
  color: var(--color-text-muted) !important;
}
</style>
