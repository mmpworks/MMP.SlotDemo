<script setup lang="ts">
import { computed } from 'vue'
import { articleForChapter } from '../teach/pairing'

/**
 * The link from a lab back to its written counterpart. Every chapter page carries one, so
 * a reader who wants the reasoning behind what they are running is one click away rather
 * than hunting for it in the reading section.
 */
const props = defineProps<{ chapter: string }>()

const article = computed(() => articleForChapter(props.chapter))

function open() {
  // The teach page reads this deep link on mount, so the reader lands on the article
  // itself rather than on the contents list.
  window.location.hash = `#/teach/${article.value}`
}
</script>

<template>
  <p v-if="article" class="read-article">
    <button type="button" class="read-article__link" @click="open">
      Read the article for this chapter →
    </button>
  </p>
</template>

<style scoped>
.read-article {
  margin: 0 0 var(--space-lg);
}

.read-article__link {
  background: transparent;
  border: var(--rule-hairline);
  color: var(--color-text-secondary);
  font-family: var(--font-display);
  font-size: 0.72rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  padding: 0.4rem 0.9rem;
  cursor: pointer;
}

.read-article__link:hover {
  color: var(--color-accent);
  border-color: var(--color-accent);
}
</style>
