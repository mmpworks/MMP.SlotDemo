<script setup lang="ts">
import { computed, ref } from 'vue'
import { fetchHello } from './api/client'
import type { HelloResponse } from './api/types'
import { useHashRoute } from './composables/useHashRoute'
import { chapterById } from './chapters/registry'
import ChapterNav from './components/ChapterNav.vue'
import LogViewer from './components/LogViewer.vue'

const hello = ref<HelloResponse | null>(null)
const { current, go } = useHashRoute('home')
const chapter = computed(() => chapterById(current.value))

async function loadHello(): Promise<void> {
  try {
    hello.value = await fetchHello()
  } catch {
    // A failed hello probe just leaves the footer blank; nothing else depends on it.
    hello.value = null
  }
}

void loadHello()
</script>

<template>
  <header class="hero">
    <div class="deco-label">MMP.SlotDemo</div>
    <h1 class="hero__title">Building a Slot Machine RTP Simulator</h1>
    <div class="deco-rule" />
  </header>

  <ChapterNav :current="chapter.id" @navigate="go" />

  <!--
    The log viewer sits outside the chapter slot on purpose: every lab logs through
    Herald, and keeping one stream mounted across navigation means the record of what
    you just did survives the jump to the next chapter.
  -->
  <main class="chapter-slot">
    <component
      :is="chapter.component"
      :key="chapter.id"
      :title="chapter.title"
      :blurb="chapter.blurb"
      @navigate="go"
    />
  </main>

  <LogViewer />

  <footer class="footer">
    <span v-if="hello">{{ hello.message }} &middot; {{ hello.harness }}</span>
  </footer>
</template>

<style scoped>
.hero {
  margin-bottom: var(--space-md);
}

.hero__title {
  font-size: 2.1rem;
  letter-spacing: 0.04em;
  margin: var(--space-xs) 0 var(--space-md);
}

.chapter-slot {
  margin-bottom: var(--space-xl);
}

.footer {
  margin-top: var(--space-xl);
  padding-top: var(--space-md);
  border-top: var(--rule-hairline);
  font-family: var(--font-mono);
  font-size: 0.72rem;
  color: var(--color-text-muted);
}
</style>
