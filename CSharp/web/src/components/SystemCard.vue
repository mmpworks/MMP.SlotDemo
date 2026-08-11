<script setup lang="ts">
import { computed } from 'vue'
import type { AiSystemStats } from '../api/types'
import { formatMemoryMb } from '../format'
import StatusBadge from './StatusBadge.vue'
import ProcessTable from './ProcessTable.vue'

const props = defineProps<{ system: AiSystemStats; staggerIndex: number }>()

const status = computed<'running' | 'installed' | 'dormant'>(() => {
  if (!props.system.installed) return 'dormant'
  return props.system.running ? 'running' : 'installed'
})

const cardStyle = computed(() => ({ '--stagger-index': String(props.staggerIndex) }))
</script>

<template>
  <article
    class="card deco-frame"
    :class="{ 'card--dormant': status === 'dormant' }"
    :style="cardStyle"
  >
    <header class="card__head">
      <h3 class="card__name">{{ system.name }}</h3>
      <StatusBadge :status="status" />
    </header>

    <div class="deco-rule" />

    <dl class="card__stats">
      <div>
        <dt class="deco-label">Version</dt>
        <dd>{{ system.cliVersion ?? '—' }}</dd>
      </div>
      <div>
        <dt class="deco-label">Processes</dt>
        <dd>{{ system.processCount }}</dd>
      </div>
      <div>
        <dt class="deco-label">Memory</dt>
        <dd>{{ system.processCount > 0 ? formatMemoryMb(system.totalMemoryMb) : '—' }}</dd>
      </div>
    </dl>

    <details v-if="system.processes.length > 0" class="card__detail">
      <summary>see running processes &rarr;</summary>
      <ProcessTable :processes="system.processes" />
    </details>
  </article>
</template>

<style scoped>
.card {
  background: var(--color-surface);
  padding: var(--space-md);
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
  opacity: 0;
  transform: translateY(6px);
  animation: card-in var(--duration-major) var(--ease-out) forwards;
  animation-delay: calc(var(--stagger-index, 0) * 28ms);
}

.card--dormant {
  background: var(--color-surface-dormant);
  border-color: color-mix(in srgb, var(--color-accent-dim) 45%, transparent);
}

.card--dormant .card__name,
.card--dormant dd {
  color: var(--color-text-muted);
}

.card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-sm);
}

.card__name {
  font-size: 1.1rem;
  letter-spacing: 0.03em;
}

.card__stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-sm);
  margin: 0;
}

.card__stats dt {
  margin-bottom: 2px;
}

.card__stats dd {
  margin: 0;
  font-family: var(--font-mono);
  font-size: 0.92rem;
  color: var(--color-text-primary);
}

.card__detail summary {
  cursor: pointer;
  font-size: 0.78rem;
  color: var(--color-accent);
  letter-spacing: 0.03em;
}

.card__detail summary:hover {
  color: var(--color-accent-bright);
}

@keyframes card-in {
  from {
    opacity: 0;
    transform: translateY(6px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media (prefers-reduced-motion: reduce) {
  .card {
    animation: none !important;
    opacity: 1;
    transform: none;
  }
}
</style>
