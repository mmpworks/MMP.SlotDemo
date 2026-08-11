<template>
  <div class="loading" role="status" aria-live="polite" aria-label="Loading system stats">
    <div class="loading__bar">
      <div class="loading__sweep" />
    </div>
    <p class="loading__label">Probing installed AI systems&hellip;</p>
  </div>
</template>

<style scoped>
.loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-md);
  padding: var(--space-xl) 0;
}

.loading__bar {
  width: 260px;
  height: 2px;
  background: var(--color-accent-dim);
  position: relative;
  overflow: hidden;
}

.loading__sweep {
  position: absolute;
  inset: 0;
  width: 40%;
  background: linear-gradient(90deg, transparent, var(--color-accent), transparent);
  animation: sweep 1.3s linear infinite;
}

.loading__label {
  font-family: var(--font-display);
  letter-spacing: 0.08em;
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

@keyframes sweep {
  from {
    transform: translateX(-100%);
  }
  to {
    transform: translateX(350%);
  }
}

@media (prefers-reduced-motion: reduce) {
  .loading__sweep {
    animation: none;
    width: 100%;
    background: var(--color-accent-dim);
  }
}
</style>
