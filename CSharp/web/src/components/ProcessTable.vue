<script setup lang="ts">
import type { ProcessStats } from '../api/types'
import { formatMemoryMb, formatClockTime } from '../format'

defineProps<{ processes: ProcessStats[] }>()
</script>

<template>
  <table class="proc-table" :aria-label="`Process list, ${processes.length} running`">
    <thead>
      <tr>
        <th scope="col">PID</th>
        <th scope="col">Name</th>
        <th scope="col">Memory</th>
        <th scope="col">Started</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="proc in processes" :key="proc.pid">
        <td class="mono">{{ proc.pid }}</td>
        <td>{{ proc.name }}</td>
        <td class="mono">{{ formatMemoryMb(proc.memoryMb) }}</td>
        <td class="mono">{{ proc.startedUtc ? formatClockTime(proc.startedUtc) : '—' }}</td>
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.proc-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.82rem;
  margin-top: var(--space-sm);
}

.proc-table th {
  text-align: left;
  font-family: var(--font-display);
  letter-spacing: 0.08em;
  text-transform: uppercase;
  font-size: 0.66rem;
  color: var(--color-text-secondary);
  border-bottom: var(--rule-hairline);
  padding: var(--space-xs) var(--space-sm);
}

.proc-table td {
  padding: var(--space-xs) var(--space-sm);
  border-bottom: 1px solid color-mix(in srgb, var(--color-accent-dim) 35%, transparent);
  color: var(--color-text-primary);
}

.mono {
  font-family: var(--font-mono);
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}
</style>
