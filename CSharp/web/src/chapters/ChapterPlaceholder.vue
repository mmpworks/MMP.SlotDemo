<script setup lang="ts">
defineProps<{ title: string; blurb: string }>()
</script>

<template>
  <section class="placeholder">
    <h2>{{ title }}</h2>
    <p>{{ blurb }}</p>
    <p class="placeholder__note">This lab ships with the episode.</p>
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
</style>
