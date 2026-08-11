<script setup lang="ts">
type Status = 'running' | 'installed' | 'dormant'

const props = defineProps<{ status: Status }>()

const labels: Record<Status, string> = {
  running: 'Running',
  installed: 'Installed',
  dormant: 'Not installed',
}
</script>

<template>
  <span class="badge" :class="`badge--${props.status}`" role="status" :aria-label="labels[props.status]">
    <span class="badge__dot" aria-hidden="true" />
    {{ labels[props.status] }}
  </span>
</template>

<style scoped>
.badge {
  display: inline-flex;
  align-items: center;
  gap: var(--space-xs);
  font-family: var(--font-display);
  font-size: 0.7rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
  border: 1px solid transparent;
}

.badge__dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}

.badge--running {
  color: var(--color-status-running);
  border-color: color-mix(in srgb, var(--color-status-running) 40%, transparent);
}
.badge--running .badge__dot {
  background: var(--color-status-running);
  box-shadow: 0 0 6px var(--color-status-running);
  animation: pulse 2.4s var(--ease-in-out) infinite;
}

.badge--installed {
  color: var(--color-status-installed);
  border-color: color-mix(in srgb, var(--color-status-installed) 40%, transparent);
}
.badge--installed .badge__dot {
  background: var(--color-status-installed);
}

.badge--dormant {
  color: var(--color-text-muted);
  border-color: transparent;
}
.badge--dormant .badge__dot {
  background: var(--color-text-muted);
}

@keyframes pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.4;
  }
}

@media (prefers-reduced-motion: reduce) {
  .badge__dot {
    animation: none !important;
  }
}
</style>
