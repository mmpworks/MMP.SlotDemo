<script setup lang="ts">
import { chapters } from '../chapters/registry'

defineProps<{ current: string }>()
const emit = defineEmits<{ (e: 'navigate', id: string): void }>()
</script>

<template>
  <nav class="chapter-nav" aria-label="Chapters">
    <button
      v-for="chapter in chapters"
      :key="chapter.id"
      class="chapter-nav__tab"
      :class="{
        'chapter-nav__tab--active': chapter.id === current,
        'chapter-nav__tab--pending': !chapter.ready,
      }"
      type="button"
      :aria-current="chapter.id === current ? 'page' : undefined"
      :title="chapter.title"
      @click="emit('navigate', chapter.id)"
    >
      {{ chapter.label }}
    </button>
  </nav>
</template>

<style scoped>
.chapter-nav {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-xs);
  margin-bottom: var(--space-lg);
  padding-bottom: var(--space-sm);
  border-bottom: var(--rule-hairline);
}

.chapter-nav__tab {
  font-family: var(--font-display);
  font-size: 0.82rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  background: transparent;
  color: var(--color-text-secondary);
  border: var(--rule-hairline);
  padding: 0.5rem 1.1rem;
  cursor: pointer;
  transition:
    color var(--duration-micro) var(--ease-out),
    border-color var(--duration-micro) var(--ease-out);
}

.chapter-nav__tab:hover {
  color: var(--color-accent-bright);
  border-color: var(--color-accent);
}

.chapter-nav__tab--active {
  color: var(--color-accent);
  border: var(--rule-brass);
}

.chapter-nav__tab--pending {
  color: var(--color-text-muted);
}
</style>
