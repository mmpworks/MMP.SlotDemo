<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { marked } from 'marked'
import { getJson } from '../api/labs'
import { chapterForArticle } from '../teach/pairing'

defineProps<{ title: string; blurb: string }>()
const emit = defineEmits<{ navigate: [id: string] }>()

interface ArticleSummary {
  id: string
  number: string
  slug: string
  title: string
}

interface Article extends ArticleSummary {
  markdown: string
}

const index = ref<ArticleSummary[]>([])
const article = ref<Article | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

/// The article currently open, or null while the reader is on the contents list.
const openId = ref<string | null>(null)

const rendered = computed(() => (article.value ? marked.parse(article.value.markdown) : ''))

/// The lab that runs the code this article describes, when there is one built.
const pairedChapter = computed(() =>
  article.value ? chapterForArticle(article.value.id) : null,
)

async function loadIndex() {
  try {
    index.value = await getJson<ArticleSummary[]>('/api/articles')
  } catch {
    error.value = 'The article list could not be loaded.'
  }
}

async function open(id: string) {
  openId.value = id
  loading.value = true
  error.value = null
  try {
    article.value = await getJson<Article>(`/api/articles/${id}`)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  } catch {
    error.value = `Article ${id} could not be loaded.`
    article.value = null
  } finally {
    loading.value = false
  }
}

function backToContents() {
  openId.value = null
  article.value = null
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

onMounted(loadIndex)

// Deep link support: #/teach/02-money-and-randomness opens that article directly, so a
// lab page can link straight to its reading rather than dropping the reader on a list.
watch(
  () => window.location.hash,
  () => {
    const match = /^#\/teach\/(?<id>[0-9a-z-]+)$/.exec(window.location.hash)
    if (match?.groups?.id) void open(match.groups.id)
  },
  { immediate: true },
)
</script>

<template>
  <section class="chapter">
    <template v-if="!openId">
      <h2>{{ title }}</h2>
      <p class="chapter__blurb">{{ blurb }}</p>
      <p class="teach__lede">
        Each article is the written half of a chapter lab. Read it for the reasoning, then
        run the lab to watch the same code do the thing it describes.
      </p>

      <ol class="teach__toc">
        <li v-for="entry in index" :key="entry.id" class="teach__entry">
          <button type="button" class="teach__link" @click="open(entry.id)">
            <span class="teach__number">{{ entry.number }}</span>
            <span class="teach__title">{{ entry.title }}</span>
          </button>
          <button
            v-if="chapterForArticle(entry.id)"
            type="button"
            class="teach__lab"
            @click="emit('navigate', chapterForArticle(entry.id)!)"
          >
            Run the lab →
          </button>
        </li>
      </ol>

      <p v-if="error" class="lab__error">{{ error }}</p>
    </template>

    <!-- One article. The section heading is dropped here so the article's own title is
         the first thing read. -->
    <template v-else>
      <div class="teach__bar">
        <button type="button" class="teach__back" @click="backToContents">← All articles</button>
        <button
          v-if="pairedChapter"
          type="button"
          class="teach__lab"
          @click="emit('navigate', pairedChapter!)"
        >
          Run this chapter's lab →
        </button>
      </div>

      <p v-if="loading" class="teach__status">Loading…</p>
      <p v-if="error" class="lab__error">{{ error }}</p>

      <!-- The markdown is our own repository content, rendered for reading. -->
      <article v-if="article && !loading" class="teach__article" v-html="rendered" />

      <div v-if="article && !loading" class="teach__bar teach__bar--foot">
        <button type="button" class="teach__back" @click="backToContents">← All articles</button>
        <button
          v-if="pairedChapter"
          type="button"
          class="teach__lab"
          @click="emit('navigate', pairedChapter!)"
        >
          Run this chapter's lab →
        </button>
      </div>
    </template>
  </section>
</template>

<style scoped>
.teach__lede {
  max-width: 62ch;
  color: var(--color-text-secondary);
  margin-bottom: var(--space-lg);
}

.teach__toc {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 1px;
  background: var(--color-rule);
}

.teach__entry {
  display: flex;
  align-items: center;
  gap: var(--space-md);
  background: var(--color-bg);
  padding: 0.15rem 0;
}

.teach__link {
  flex: 1 1 auto;
  display: flex;
  align-items: baseline;
  gap: var(--space-md);
  background: transparent;
  border: 0;
  padding: 0.8rem var(--space-sm);
  text-align: left;
  cursor: pointer;
  color: var(--color-text-primary);
  font-family: inherit;
  font-size: 1rem;
}

.teach__link:hover {
  background: var(--color-surface);
  color: var(--color-accent);
}

.teach__number {
  font-family: var(--font-mono);
  font-size: 0.8rem;
  color: var(--color-accent);
  min-width: 2ch;
}

.teach__title {
  line-height: 1.4;
}

.teach__lab,
.teach__back {
  flex: 0 0 auto;
  background: transparent;
  border: var(--rule-hairline);
  color: var(--color-text-secondary);
  font-family: var(--font-display);
  font-size: 0.72rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  padding: 0.4rem 0.8rem;
  cursor: pointer;
  white-space: nowrap;
}

.teach__lab:hover,
.teach__back:hover {
  color: var(--color-accent);
  border-color: var(--color-accent);
}

.teach__bar {
  display: flex;
  justify-content: space-between;
  gap: var(--space-md);
  margin-bottom: var(--space-lg);
  max-width: 74ch;
}

.teach__bar--foot {
  margin: var(--space-xl) 0 0;
}

.teach__status {
  color: var(--color-text-muted);
}

/* The rendered article. Reading measure is the point here, so the column is narrow and
   the vertical rhythm is looser than the lab pages. */
.teach__article {
  max-width: 74ch;
  line-height: 1.7;
}

.teach__article :deep(h1) {
  font-size: 1.9rem;
  margin: 0 0 var(--space-md);
}

.teach__article :deep(h2) {
  font-size: 1.3rem;
  margin: var(--space-xl) 0 var(--space-sm);
  padding-bottom: 0.3rem;
  border-bottom: var(--rule-hairline);
}

.teach__article :deep(h3) {
  font-size: 1.05rem;
  margin: var(--space-lg) 0 var(--space-xs);
}

.teach__article :deep(p),
.teach__article :deep(li) {
  color: var(--color-text-secondary);
}

.teach__article :deep(code) {
  font-family: var(--font-mono);
  font-size: 0.86em;
  background: var(--color-surface);
  padding: 0.1em 0.35em;
}

.teach__article :deep(pre) {
  background: var(--color-surface);
  border-left: 2px solid var(--color-accent-dim);
  padding: var(--space-sm) var(--space-md);
  overflow-x: auto;
}

.teach__article :deep(pre code) {
  background: none;
  padding: 0;
  font-size: 0.82rem;
  line-height: 1.5;
}

.teach__article :deep(blockquote) {
  margin: var(--space-md) 0;
  padding: 0.2rem var(--space-md);
  border-left: 2px solid var(--color-accent-dim);
  color: var(--color-text-muted);
}

.teach__article :deep(table) {
  width: 100%;
  border-collapse: collapse;
  margin: var(--space-md) 0;
  font-size: 0.88rem;
  display: block;
  overflow-x: auto;
}

.teach__article :deep(th),
.teach__article :deep(td) {
  border-bottom: var(--rule-hairline);
  padding: 0.4rem 0.6rem;
  text-align: left;
}

.teach__article :deep(th) {
  font-family: var(--font-display);
  font-size: 0.75rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.teach__article :deep(a) {
  color: var(--color-accent);
}

.teach__article :deep(img) {
  max-width: 100%;
}
</style>
