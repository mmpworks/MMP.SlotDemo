<script setup lang="ts">
defineProps<{ title: string; blurb: string }>()
</script>

<template>
  <section class="placeholder">
    <h2>{{ title }}</h2>
    <p>{{ blurb }}</p>
    <p class="placeholder__note">
      The interactive lab lands with the episode. Every part it names already has its own
      page in the nav above.
    </p>
    <aside class="optimization-note">
      <strong>Optimization notebook:</strong> identify the likely hot paths during design,
      but keep the first implementation clear enough to verify independently. Episode 9
      measures those candidates after the complete system has a trusted baseline.
      <a href="#/ch09">Open the optimization lab →</a>
    </aside>
  </section>
</template>

<style scoped>
.placeholder {
  border: var(--rule-hairline);
  padding: var(--space-lg);
}

.placeholder h2 {
  margin: 0 0 var(--space-sm);
  font-size: 1.3rem;
}

.placeholder p {
  color: var(--color-text-secondary);
  max-width: 62ch;
  line-height: 1.6;
}

.placeholder__note {
  font-family: var(--font-mono);
  font-size: 0.76rem;
  color: var(--color-text-muted);
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.optimization-note { margin-top: 1.5rem; padding: 1rem; border-left: 3px solid #39c6a3; color: var(--color-text-secondary); }
.optimization-note a { display: block; margin-top: .5rem; }
</style>
