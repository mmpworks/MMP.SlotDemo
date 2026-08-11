<script setup lang="ts">
import { ref } from 'vue'
import { fetchStats } from '../api/client'
import type { StatsResponse } from '../api/types'
import { formatClockTime } from '../format'
import { chapters } from './registry'
import MachineStrip from '../components/MachineStrip.vue'
import SystemCard from '../components/SystemCard.vue'
import LoadingState from '../components/LoadingState.vue'
import ErrorPanel from '../components/ErrorPanel.vue'

type StatsPhase = 'idle' | 'loading' | 'loaded' | 'error'

const emit = defineEmits<{ (e: 'navigate', id: string): void }>()

const stats = ref<StatsResponse | null>(null)
const phase = ref<StatsPhase>('idle')
const errorMessage = ref('')

const episodes = chapters.filter((c) => c.id !== 'home')

async function loadStats(): Promise<void> {
  phase.value = 'loading'
  try {
    stats.value = await fetchStats()
    phase.value = 'loaded'
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : 'Unknown error probing stats.'
    phase.value = 'error'
  }
}
</script>

<template>
  <section class="intro">
    <p>
      Each episode of the build has a page here. The controls run the episode's own types
      on the server rather than a JavaScript lookalike, and every step narrates itself
      through Herald, so the log stream at the bottom of the page is the same code path
      talking back.
    </p>
  </section>

  <section class="episode-grid" aria-label="Episode labs">
    <button
      v-for="episode in episodes"
      :key="episode.id"
      class="episode-card"
      :class="{ 'episode-card--pending': !episode.ready }"
      type="button"
      @click="emit('navigate', episode.id)"
    >
      <span class="episode-card__number">{{ episode.label }}</span>
      <span class="episode-card__title">{{ episode.title.replace(/^Episode \d+ — /, '') }}</span>
      <span class="episode-card__blurb">{{ episode.blurb }}</span>
      <span v-if="!episode.ready" class="episode-card__flag">lab pending</span>
    </button>
  </section>

  <section class="probe">
    <h2>Machine probe</h2>
    <p class="probe__lede">
      The harness feature this demo site was forked from: a scan for installed AI coding
      tools. It stays because it exercises the server, the SSE log relay, and the viewer in
      one click — a quick way to confirm the plumbing before recording.
    </p>

    <MachineStrip v-if="stats" :machine="stats.machine" />

    <div class="stat-launch">
      <button class="stat-button" type="button" :disabled="phase === 'loading'" @click="loadStats">
        {{ phase === 'loading' ? 'Probing…' : 'STAT' }}
      </button>
      <p v-if="stats" class="stat-launch__meta">
        Last probe {{ formatClockTime(stats.generatedAtUtc) }}
      </p>
    </div>

    <LoadingState v-if="phase === 'loading'" />
    <ErrorPanel v-else-if="phase === 'error'" :message="errorMessage" @retry="loadStats" />
    <div
      v-else-if="phase === 'loaded' && stats"
      class="grid"
      role="list"
      aria-label="AI system status cards"
    >
      <SystemCard
        v-for="(system, index) in stats.systems"
        :key="system.id"
        :system="system"
        :stagger-index="index"
        role="listitem"
      />
    </div>
  </section>
</template>

<style scoped>
.intro p {
  color: var(--color-text-secondary);
  max-width: 68ch;
  line-height: 1.65;
  margin: 0 0 var(--space-lg);
}

.episode-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: var(--space-md);
  margin-bottom: var(--space-xl);
}

.episode-card {
  display: grid;
  gap: var(--space-xs);
  text-align: left;
  background: var(--color-surface);
  border: var(--rule-hairline);
  padding: var(--space-md);
  color: inherit;
  font: inherit;
  cursor: pointer;
  transition:
    border-color var(--duration-micro) var(--ease-out),
    background var(--duration-micro) var(--ease-out);
}

.episode-card:hover {
  border-color: var(--color-accent);
  background: var(--color-surface-raised);
}

.episode-card--pending {
  background: var(--color-surface-dormant);
}

.episode-card__number {
  font-family: var(--font-display);
  font-size: 0.8rem;
  letter-spacing: 0.28em;
  color: var(--color-accent);
}

.episode-card__title {
  font-size: 1rem;
}

.episode-card__blurb {
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.episode-card__flag {
  font-family: var(--font-mono);
  font-size: 0.68rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.probe h2 {
  font-size: 1.15rem;
  margin: 0 0 var(--space-xs);
}

.probe__lede {
  color: var(--color-text-secondary);
  max-width: 66ch;
  line-height: 1.6;
  font-size: 0.9rem;
}

.stat-launch {
  display: flex;
  align-items: baseline;
  gap: var(--space-md);
  margin: var(--space-md) 0 var(--space-lg);
}

.stat-button {
  font-family: var(--font-display);
  font-size: 1rem;
  letter-spacing: 0.28em;
  text-transform: uppercase;
  background: transparent;
  color: var(--color-accent);
  border: var(--rule-brass);
  padding: 0.85rem 2.4rem;
  cursor: pointer;
  transition:
    color var(--duration-micro) var(--ease-out),
    border-color var(--duration-micro) var(--ease-out),
    background var(--duration-micro) var(--ease-out);
}

.stat-button:hover:not(:disabled) {
  color: var(--color-accent-bright);
  border-color: var(--color-accent-bright);
  background: color-mix(in srgb, var(--color-accent) 8%, transparent);
}

.stat-button:disabled {
  color: var(--color-accent-dim);
  border-color: var(--color-accent-dim);
  cursor: progress;
}

.stat-launch__meta {
  font-family: var(--font-mono);
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--space-md);
}
</style>
