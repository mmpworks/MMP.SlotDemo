<script setup lang="ts">
defineProps<{ question: string; later: string }>()
</script>

<template>
  <aside class="optimization-preview">
    <p class="optimization-preview__eyebrow">Optimization notebook</p>
    <h3>{{ question }}</h3>
    <p>{{ later }}</p>
    <a href="#/ch09">Measure it in episode 9 →</a>
  </aside>
</template>

<style scoped>
.optimization-preview { margin: 2rem 0; padding: 1.1rem 1.25rem; border-left: 3px solid #39c6a3; background: color-mix(in srgb, #39c6a3 8%, transparent); }
.optimization-preview__eyebrow { margin: 0 0 .3rem; color: #39c6a3; font-size: .76rem; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
.optimization-preview h3 { margin: 0 0 .45rem; }
.optimization-preview p { margin: 0 0 .6rem; }
</style>
