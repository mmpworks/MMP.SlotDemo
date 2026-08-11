<script setup lang="ts">
import type { MachineStats } from '../api/types'
import { formatMemoryMb, formatUptimeHours } from '../format'

defineProps<{ machine: MachineStats }>()
</script>

<template>
  <section
    class="strip deco-frame"
    role="group"
    aria-label="Machine summary: host, operating system, processor count, memory, and uptime"
  >
    <div class="strip__item">
      <div class="deco-label">Host</div>
      <div class="strip__value">{{ machine.host }}</div>
    </div>
    <div class="strip__item">
      <div class="deco-label">OS</div>
      <div class="strip__value">{{ machine.os }}</div>
    </div>
    <div class="strip__item">
      <div class="deco-label">CPUs</div>
      <div class="strip__value">{{ machine.processorCount }}</div>
    </div>
    <div class="strip__item">
      <div class="deco-label">Memory</div>
      <div class="strip__value">{{ formatMemoryMb(machine.totalMemoryMb) }}</div>
    </div>
    <div class="strip__item">
      <div class="deco-label">Uptime</div>
      <div class="strip__value">{{ formatUptimeHours(machine.uptimeHours) }}</div>
    </div>
  </section>
</template>

<style scoped>
.strip {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: var(--space-md);
  background: var(--color-surface);
  padding: var(--space-md) var(--space-lg);
  margin-bottom: var(--space-lg);
}

.strip__item {
  display: flex;
  flex-direction: column;
  gap: var(--space-xs);
  border-left: var(--rule-hairline);
  padding-left: var(--space-md);
}

.strip__item:first-child {
  border-left: none;
  padding-left: 0;
}

.strip__value {
  font-family: var(--font-display);
  font-size: 1.05rem;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

@media (max-width: 860px) {
  .strip {
    grid-template-columns: repeat(2, 1fr);
  }
  .strip__item {
    border-left: none;
    padding-left: 0;
  }
}
</style>
