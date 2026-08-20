import { onMounted, onUnmounted, ref } from 'vue'

/**
 * Hash routing in a dozen lines. The site is a fixed list of chapter pages with no
 * params, no guards, and no lazy loading, so a router dependency would carry more
 * concepts than the navigation has.
 */
export function useHashRoute(fallback: string) {
  const current = ref(readHash() || fallback)

  /**
   * The page id is the FIRST path segment. Anything after it belongs to the page: the
   * teach section uses #/teach/<article-id> so an article can be linked to directly, and
   * without this split that whole hash would fail to match a page and fall back home.
   */
  function readHash(): string {
    return window.location.hash.replace(/^#\/?/, '').split('/')[0]
  }

  function sync(): void {
    current.value = readHash() || fallback
    window.scrollTo({ top: 0 })
  }

  function go(id: string): void {
    window.location.hash = `#/${id}`
  }

  onMounted(() => window.addEventListener('hashchange', sync))
  onUnmounted(() => window.removeEventListener('hashchange', sync))

  return { current, go }
}
