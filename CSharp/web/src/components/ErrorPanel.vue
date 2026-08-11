<script setup lang="ts">
defineProps<{ message: string }>()
const emit = defineEmits<{ retry: [] }>()
</script>

<template>
  <div class="error deco-frame" role="alert">
    <div class="deco-label" style="color: var(--color-status-error)">Stat probe failed</div>
    <p class="error__message">{{ message }}</p>
    <button class="error__retry" type="button" @click="emit('retry')">Try again</button>
  </div>
</template>

<style scoped>
.error {
  background: var(--color-surface);
  border-color: color-mix(in srgb, var(--color-status-error) 45%, transparent);
  padding: var(--space-lg);
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
  align-items: flex-start;
}

.error__message {
  margin: 0;
  color: var(--color-text-primary);
}

.error__retry {
  font-family: var(--font-display);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  font-size: 0.78rem;
  background: transparent;
  border: var(--rule-hairline);
  color: var(--color-accent);
  padding: 0.45rem 1rem;
  cursor: pointer;
  transition: border-color var(--duration-micro) var(--ease-out), color var(--duration-micro) var(--ease-out);
}

.error__retry:hover {
  border-color: var(--color-accent);
  color: var(--color-accent-bright);
}
</style>
