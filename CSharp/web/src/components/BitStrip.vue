<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    /** 64 characters of '0'/'1', most significant bit first. */
    bits: string
    /** Bits below this index are the fractional part of a credit — shaded. */
    fractionBits?: number
    label?: string
  }>(),
  { fractionBits: 0, label: '' },
)

// Grouped in bytes so a viewer can count position without a ruler.
const groups = computed(() => {
  const padded = props.bits.padStart(64, '0')
  const out: { bits: string; startIndex: number }[] = []
  for (let i = 0; i < 64; i += 8) {
    out.push({ bits: padded.slice(i, i + 8), startIndex: i })
  }
  return out
})

function isFraction(index: number): boolean {
  return props.fractionBits > 0 && index >= 64 - props.fractionBits
}
</script>

<template>
  <div class="bit-strip">
    <span v-if="label" class="bit-strip__label">{{ label }}</span>
    <div class="bit-strip__row">
      <span v-for="group in groups" :key="group.startIndex" class="bit-strip__group">
        <span
          v-for="(bit, offset) in group.bits.split('')"
          :key="offset"
          class="bit-strip__bit"
          :class="{
            'bit-strip__bit--set': bit === '1',
            'bit-strip__bit--fraction': isFraction(group.startIndex + offset),
          }"
        >{{ bit }}</span>
      </span>
    </div>
  </div>
</template>

<style scoped>
.bit-strip {
  display: grid;
  gap: 0.25rem;
}

.bit-strip__label {
  font-family: var(--font-mono);
  font-size: 0.7rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.bit-strip__row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.bit-strip__group {
  display: flex;
  gap: 1px;
}

.bit-strip__bit {
  font-family: var(--font-mono);
  font-size: 0.7rem;
  line-height: 1;
  padding: 0.2rem 0.15rem;
  color: var(--color-text-muted);
  background: var(--color-surface);
}

.bit-strip__bit--set {
  color: var(--color-ground);
  background: var(--color-accent);
}

.bit-strip__bit--fraction {
  outline: 1px solid color-mix(in srgb, var(--color-accent) 35%, transparent);
}
</style>
