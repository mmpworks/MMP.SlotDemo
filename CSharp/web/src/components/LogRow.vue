<script setup lang="ts">
import { computed } from 'vue'
import type { LogEvent } from '../api/types'
import { formatLogTimestamp } from '../format'
import { LEVEL_META, levelColor, normalizeLevelKey } from '../logviewer/levels'
import { messageSegments } from '../logviewer/templateRender'

const props = defineProps<{ event: LogEvent }>()

const levelKey = computed(() => normalizeLevelKey(props.event.level_key))
const levelMeta = computed(() => LEVEL_META[levelKey.value])
const isFatal = computed(() => levelKey.value === 'fatal')

const segments = computed(() =>
  messageSegments(props.event.message_template, props.event.message, props.event.properties),
)
</script>

<template>
  <div class="log-row">
    <span class="log-row__time">{{ formatLogTimestamp(event.time) }}</span>
    <span
      class="log-row__level"
      :class="{ 'log-row__level--fatal': isFatal }"
      :style="{ color: levelColor(levelKey) }"
      :title="levelMeta.origin === 'framework' ? `${levelMeta.label} (framework)` : levelMeta.label"
    >
      {{ levelMeta.label }}
    </span>
    <span class="log-row__category" :title="event.category">{{ event.category }}</span>
    <span class="log-row__message">
      <template v-for="(segment, index) in segments" :key="index">
        <span v-if="segment.kind === 'value'" class="log-row__value">{{ segment.text }}</span>
        <template v-else>{{ segment.text }}</template>
      </template>
    </span>
  </div>
</template>

<style scoped>
.log-row {
  display: flex;
  align-items: baseline;
  gap: 1.1rem;
  height: 22px;
  line-height: 22px;
  padding: 0 var(--space-sm);
  font-family: var(--font-mono);
  font-size: 0.76rem;
  white-space: nowrap;
  overflow: hidden;
}

.log-row:nth-child(odd) {
  background: color-mix(in srgb, var(--color-surface) 60%, transparent);
}

.log-row__time {
  flex: 0 0 auto;
  color: var(--color-text-muted);
}

.log-row__level {
  /* Fixed column: the longest rendered label is INFORMATION (11ch); the extra
     width keeps level clear of category so every category starts on the same x. */
  flex: 0 0 15ch;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: 0.68rem;
  overflow: hidden;
}

.log-row__category {
  /* Fixed column for the same reason — messages all start aligned. */
  flex: 0 0 30ch;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--color-text-secondary);
}

.log-row__message {
  flex: 1 1 auto;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--color-text-primary);
}

.log-row__value {
  color: var(--color-log-value);
}

/* Fatal reads as the deepest/boldest level: bold weight + a faint glow,
   layered on top of the color already bound via levelColor(). */
.log-row__level--fatal {
  font-weight: 700;
  text-shadow: 0 0 6px color-mix(in srgb, var(--color-log-fatal) 55%, transparent);
}
</style>
