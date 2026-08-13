<script setup lang="ts">
import { computed, ref } from 'vue'

const props = defineProps<{
  question: string
  choices: string[]
  answer: number
  explanation: string
}>()

const selected = ref<number | null>(null)
const checked = ref(false)
const correct = computed(() => selected.value === props.answer)

function check(): void {
  if (selected.value !== null) checked.value = true
}
</script>

<template>
  <aside class="check">
    <h4>Check your understanding</h4>
    <p>{{ question }}</p>
    <label v-for="(choice, index) in choices" :key="choice" class="check__choice">
      <input v-model="selected" type="radio" :value="index" @change="checked = false" />
      <span>{{ choice }}</span>
    </label>
    <button type="button" :disabled="selected === null" @click="check">Check my answer</button>
    <p v-if="checked" class="check__result" :class="correct ? 'check__result--correct' : 'check__result--retry'">
      <strong>{{ correct ? 'Correct.' : 'Try again.' }}</strong> {{ explanation }}
    </p>
  </aside>
</template>

<style scoped>
.check {
  margin-top: 1.25rem;
  padding: 1rem;
  border: 1px solid var(--color-border);
  border-left: 4px solid var(--color-accent);
  background: var(--color-surface-raised);
}

.check h4 { margin: 0 0 0.5rem; }
.check__choice { display: flex; gap: 0.55rem; margin: 0.45rem 0; }
.check button { margin-top: 0.65rem; }
.check__result { margin-bottom: 0; }
.check__result--correct { color: var(--color-success); }
.check__result--retry { color: var(--color-warning); }
</style>
