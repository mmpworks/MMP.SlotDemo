<script setup lang="ts">
// Live SSE log viewer. Virtualizes a 25k-row ring buffer at a fixed row
// height (LogRow.vue is 22px) using the top/bottom-spacer technique so only
// the rows in view ever touch the DOM. Filtering is view-side (hidden levels
// stay in the buffer), and Follow re-engages on scroll-to-bottom, disengaging
// the moment the operator scrolls up to read history.
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef, watch } from 'vue'
import { useLogStream } from '../composables/useLogStream'
import { LEVEL_FAMILIES, LEVEL_META, LEVEL_ORDER, levelColor, normalizeLevelKey, type LevelKey } from '../logviewer/levels'
import { bottomScrollTop, computeSpacerHeights, computeVisibleRange, isNearBottom } from '../logviewer/virtualization'
import LogRow from './LogRow.vue'
import type { LogEvent } from '../api/types'

const ROW_HEIGHT_PX = 22
const OVERSCAN_ROWS = 8
const FOLLOW_THRESHOLD_PX = ROW_HEIGHT_PX * 2

const stream = useLogStream('/api/logs/stream')

const paused = ref(false)
const pausedSnapshot = ref<LogEvent[] | null>(null)
const follow = ref(true)
const visibleLevels = ref<Set<LevelKey>>(new Set(LEVEL_ORDER))

const panelEl = useTemplateRef<HTMLElement>('panelEl')
const viewportEl = useTemplateRef<HTMLElement>('viewportEl')
const scrollTop = ref(0)
const viewportHeight = ref(0)
let resizeObserver: ResizeObserver | null = null
let suppressFollowCheck = false

const sourceEvents = computed<LogEvent[]>(() =>
  paused.value && pausedSnapshot.value ? pausedSnapshot.value : stream.events.value,
)

const allLevelsVisible = computed(() => visibleLevels.value.size === LEVEL_ORDER.length)

// The unfiltered path returns the source array as-is (no copy) so the hot
// live-tail case — every level shown, which is the default — never pays
// for an allocation on every animation-frame flush.
const filteredEvents = computed<LogEvent[]>(() =>
  allLevelsVisible.value
    ? sourceEvents.value
    : sourceEvents.value.filter((evt) => visibleLevels.value.has(normalizeLevelKey(evt.level_key))),
)

const totalRows = computed(() => filteredEvents.value.length)
const bufferedCount = computed(() => stream.events.value.length)

const range = computed(() => computeVisibleRange(scrollTop.value, viewportHeight.value, ROW_HEIGHT_PX, totalRows.value, OVERSCAN_ROWS))
const spacers = computed(() => computeSpacerHeights(range.value, totalRows.value, ROW_HEIGHT_PX))
const visibleEvents = computed(() => filteredEvents.value.slice(range.value.start, range.value.end))

const connectionLabel = computed(() => {
  switch (stream.connectionState.value) {
    case 'live':
      return 'Live'
    case 'reconnecting':
      return 'Reconnecting…'
    default:
      return 'Connecting…'
  }
})

function scrollToBottom(): void {
  suppressFollowCheck = true
  void nextTick(() => {
    const el = viewportEl.value
    if (el) {
      const target = bottomScrollTop(totalRows.value, ROW_HEIGHT_PX, viewportHeight.value)
      el.scrollTop = target
      scrollTop.value = target
    }
    suppressFollowCheck = false
  })
}

function onScroll(): void {
  const el = viewportEl.value
  if (!el) {
    return
  }
  scrollTop.value = el.scrollTop
  if (suppressFollowCheck || !follow.value) {
    return
  }
  if (!isNearBottom(scrollTop.value, totalRows.value, ROW_HEIGHT_PX, viewportHeight.value, FOLLOW_THRESHOLD_PX)) {
    follow.value = false
  }
}

function maybeFollowScroll(): void {
  if (follow.value && !paused.value) {
    scrollToBottom()
  }
}

function jumpToLive(): void {
  follow.value = true
  scrollToBottom()
}

function togglePause(): void {
  if (paused.value) {
    paused.value = false
    pausedSnapshot.value = null
    follow.value = true
    scrollToBottom()
  } else {
    pausedSnapshot.value = stream.events.value
    paused.value = true
  }
}

function clearBuffer(): void {
  stream.clear()
  pausedSnapshot.value = null
  scrollTop.value = 0
}

function toggleLevel(key: LevelKey): void {
  const next = new Set(visibleLevels.value)
  if (next.has(key)) {
    next.delete(key)
  } else {
    next.add(key)
  }
  visibleLevels.value = next
}

function isLevelVisible(key: LevelKey | null): boolean {
  return key !== null && visibleLevels.value.has(key)
}

// bufferedCount changes exactly once per stream flush (see useLogStream's
// rAF batching), so watching it is the cheapest correct signal for "new
// rows landed" — no separate polling loop needed.
watch(bufferedCount, maybeFollowScroll)

onMounted(() => {
  if (panelEl.value && typeof ResizeObserver !== 'undefined') {
    resizeObserver = new ResizeObserver(() => {
      viewportHeight.value = viewportEl.value?.clientHeight ?? 0
      maybeFollowScroll()
    })
    resizeObserver.observe(panelEl.value)
  }
  viewportHeight.value = viewportEl.value?.clientHeight ?? 0
})

onUnmounted(() => {
  resizeObserver?.disconnect()
})
</script>

<template>
  <section ref="panelEl" class="log-viewer deco-frame" aria-label="Live log stream">
    <div class="log-controls" role="toolbar" aria-label="Log viewer controls">
      <div class="log-controls__group">
        <button type="button" class="log-btn" @click="clearBuffer">Clear</button>
        <button type="button" class="log-btn" @click="togglePause">{{ paused ? 'Resume' : 'Pause' }}</button>
        <button
          type="button"
          class="log-btn"
          :class="{ 'log-btn--active': follow }"
          :aria-pressed="follow"
          @click="follow ? (follow = false) : jumpToLive()"
        >
          Follow
        </button>
        <button v-if="!follow" type="button" class="log-btn log-btn--accent" @click="jumpToLive">Jump to live</button>
      </div>

      <div class="log-filters" role="group" aria-label="Log level filters">
        <div v-for="family in LEVEL_FAMILIES" :key="family.label" class="log-filter-family">
          <span class="deco-label log-filter-family__label">{{ family.label }}</span>
          <button
            v-if="family.sysKey"
            type="button"
            class="log-filter-chip"
            :class="{ 'log-filter-chip--off': !isLevelVisible(family.sysKey) }"
            :style="{ color: levelColor(family.sysKey) }"
            :aria-pressed="isLevelVisible(family.sysKey)"
            :title="`${LEVEL_META[family.sysKey].label} (framework)`"
            @click="toggleLevel(family.sysKey)"
          >
            sys
          </button>
          <button
            type="button"
            class="log-filter-chip"
            :class="{ 'log-filter-chip--off': !isLevelVisible(family.appKey) }"
            :style="{ color: levelColor(family.appKey) }"
            :aria-pressed="isLevelVisible(family.appKey)"
            :title="LEVEL_META[family.appKey].label"
            @click="toggleLevel(family.appKey)"
          >
            {{ family.sysKey ? 'app' : family.label.slice(0, 3).toLowerCase() }}
          </button>
        </div>
      </div>

      <div class="log-status">
        <span
          class="log-status__dot"
          :class="`log-status__dot--${stream.connectionState.value}`"
          role="status"
          :aria-label="connectionLabel"
          :title="connectionLabel"
        />
        <span class="log-status__count mono">{{ totalRows.toLocaleString() }} / {{ bufferedCount.toLocaleString() }} shown</span>
      </div>
    </div>

    <div ref="viewportEl" class="log-viewport" role="log" :aria-label="`${totalRows} log rows`" @scroll="onScroll">
      <div class="log-spacer" :style="{ height: `${spacers.topPx}px` }" />
      <LogRow v-for="(evt, i) in visibleEvents" :key="range.start + i" :event="evt" />
      <div class="log-spacer" :style="{ height: `${spacers.bottomPx}px` }" />
      <p v-if="totalRows === 0" class="log-empty">No log events yet.</p>
    </div>
  </section>
</template>

<style scoped>
.log-viewer {
  display: flex;
  flex-direction: column;
  resize: both;
  overflow: hidden;
  min-width: 50%;
  max-width: 100%;
  min-height: 220px;
  height: 440px;
  margin-top: var(--space-lg);
  background: var(--color-surface);
}

.log-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-md);
  padding: var(--space-sm) var(--space-md);
  border-bottom: var(--rule-hairline);
  flex: 0 0 auto;
}

.log-controls__group {
  display: flex;
  gap: var(--space-xs);
}

.log-btn {
  font-family: var(--font-display);
  font-size: 0.68rem;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  background: transparent;
  border: var(--rule-hairline);
  color: var(--color-text-secondary);
  padding: 0.3rem 0.7rem;
  cursor: pointer;
  transition: border-color var(--duration-micro) var(--ease-out), color var(--duration-micro) var(--ease-out);
}

.log-btn:hover {
  border-color: var(--color-accent);
  color: var(--color-accent-bright);
}

.log-btn--active {
  border-color: var(--color-accent);
  color: var(--color-accent);
}

.log-btn--accent {
  border-color: var(--color-accent);
  color: var(--color-accent-bright);
}

.log-filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-sm);
  flex: 1 1 auto;
}

.log-filter-family {
  display: flex;
  align-items: center;
  gap: 0.3rem;
}

.log-filter-family__label {
  font-size: 0.6rem;
  color: var(--color-text-muted);
}

.log-filter-chip {
  font-family: var(--font-mono);
  font-size: 0.6rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  background: transparent;
  border: 1px solid currentColor;
  border-radius: 3px;
  padding: 0.05rem 0.35rem;
  cursor: pointer;
  transition: opacity var(--duration-micro) var(--ease-out);
}

.log-filter-chip--off {
  opacity: 0.32;
}

.log-status {
  display: flex;
  align-items: center;
  gap: var(--space-xs);
  margin-left: auto;
}

.log-status__dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--color-text-muted);
}

.log-status__dot--live {
  background: var(--color-status-running);
  box-shadow: 0 0 6px var(--color-status-running);
  animation: log-status-pulse 2.4s var(--ease-in-out) infinite;
}

.log-status__dot--reconnecting {
  background: var(--color-log-warning);
  animation: log-status-pulse 1s var(--ease-in-out) infinite;
}

.log-status__dot--connecting {
  background: var(--color-text-muted);
}

@keyframes log-status-pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.35;
  }
}

@media (prefers-reduced-motion: reduce) {
  .log-status__dot--live,
  .log-status__dot--reconnecting {
    animation: none !important;
  }
}

.log-status__count {
  font-size: 0.7rem;
  color: var(--color-text-secondary);
}

.log-viewport {
  flex: 1 1 auto;
  overflow-y: auto;
  overflow-x: hidden;
}

.log-spacer {
  width: 100%;
}

.log-empty {
  padding: var(--space-md);
  color: var(--color-text-muted);
  font-size: 0.82rem;
}

.mono {
  font-family: var(--font-mono);
}
</style>
