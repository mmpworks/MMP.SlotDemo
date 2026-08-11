<script setup lang="ts">
import MoneyLab from './ch02/MoneyLab.vue'
import RngLab from './ch02/RngLab.vue'
import BiasLab from './ch02/BiasLab.vue'

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
        Two small types carry the guarantees the rest of the simulator depends on.
        <code>Millicents</code> keeps money as a count of integers so totals stay exact and
        order-independent. <code>SpinRng</code> gives every worker its own seeded stream so a
        run can be replayed exactly. Everything downstream gets to be ordinary because these
        two are strict.
      </p>
      <dl class="invariants">
        <div>
          <dt>M1</dt>
          <dd>No floating point in money paths. There is no conversion to <code>double</code> to reach for, so the compiler enforces it on every build.</dd>
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
        those exact files, served from
        <code>CSharp/src/SlotDemo.Server/Chapters/</code>.
      </p>
    </section>

    <MoneyLab />
    <RngLab />
    <BiasLab />

    <section class="chapter__next">
      <h3>Carried into episode 3</h3>
      <p>
        The stop index at the end of lab 2 is the handoff. Episode 3 takes those numbers and
        asks what a reel actually is — and why a strip of positions behaves differently from a
        weighted die, even when the two look identical on a spec sheet.
      </p>
    </section>
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
