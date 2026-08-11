<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    /** One row index per reel — the path the line reads. */
    rows: number[]
    /** Window height. */
    windowRows?: number
    active?: boolean
  }>(),
  { windowRows: 3, active: false },
)

const CELL = 10
const GAP = 2

function x(reel: number): number {
  return reel * (CELL + GAP) + CELL / 2
}

function y(row: number): number {
  return row * (CELL + GAP) + CELL / 2
}

const width = () => props.rows.length * (CELL + GAP) - GAP
const height = () => props.windowRows * (CELL + GAP) - GAP

const path = () =>
  props.rows.map((row, reel) => `${x(reel)},${y(row)}`).join(' ')
</script>

<template>
  <svg
    class="payline-pattern"
    :class="{ 'payline-pattern--active': active }"
    :viewBox="`0 0 ${width()} ${height()}`"
    :style="{ width: `${width()}px`, height: `${height()}px` }"
    aria-hidden="true"
  >
    <template v-for="reel in rows.length" :key="reel">
      <rect
        v-for="row in windowRows"
        :key="row"
        :x="(reel - 1) * (CELL + GAP)"
        :y="(row - 1) * (CELL + GAP)"
        :width="CELL"
        :height="CELL"
        class="payline-pattern__cell"
        :class="{ 'payline-pattern__cell--on': rows[reel - 1] === row - 1 }"
      />
    </template>
    <polyline :points="path()" class="payline-pattern__path" />
  </svg>
</template>

<style scoped>
.payline-pattern {
  display: block;
}

.payline-pattern__cell {
  fill: var(--color-surface);
  stroke: color-mix(in srgb, var(--color-text-muted) 40%, transparent);
  stroke-width: 0.5;
}

.payline-pattern__cell--on {
  fill: color-mix(in srgb, var(--color-accent) 30%, transparent);
  stroke: var(--color-accent);
}

.payline-pattern__path {
  fill: none;
  stroke: var(--color-accent);
  stroke-width: 1.4;
  opacity: 0.55;
}

.payline-pattern--active .payline-pattern__cell--on {
  fill: color-mix(in srgb, var(--color-accent) 55%, transparent);
}

.payline-pattern--active .payline-pattern__path {
  opacity: 1;
  stroke: var(--color-accent-bright);
}
</style>
