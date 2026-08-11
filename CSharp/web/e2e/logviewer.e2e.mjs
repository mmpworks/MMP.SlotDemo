// Browser E2E for the LogViewer against a LIVE server on :5090.
// Prereq: build the SPA (npm run build) and start the server
// (dotnet run --project ../src/SlotDemo.Server), then: npm run e2e
//
// Uses puppeteer-core + the system Chrome install — no bundled browser download.

import puppeteer from 'puppeteer-core'

const BASE = process.env.E2E_BASE_URL ?? 'http://localhost:5090'
const CHROME = process.env.CHROME_PATH ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe'

const results = []
let failures = 0

function check(name, condition, detail = '') {
  results.push(`${condition ? 'PASS' : 'FAIL'}  ${name}${detail ? ` — ${detail}` : ''}`)
  if (!condition) failures++
}

/** POST synthetic Herald-shaped events straight into the ingest relay. */
async function injectEvents(count, { level = 'information', levelName = 'INF', rank = '5', tag = 'E2E' } = {}) {
  const BATCH = 500
  for (let sent = 0; sent < count; sent += BATCH) {
    const size = Math.min(BATCH, count - sent)
    const batch = Array.from({ length: size }, (_, i) => ({
      time: new Date().toISOString(),
      level: levelName,
      level_key: level,
      level_rank: rank,
      category: 'E2E.Harness',
      message_template: `${tag} event {Index} of {Total}`,
      message: `${tag} event ${sent + i} of ${count}`,
      properties: {
        Index: { value: sent + i, capture_mode: 'Default' },
        Total: { value: count, capture_mode: 'Default' },
      },
      context: {},
    }))
    const response = await fetch(`${BASE}/api/logs/ingest`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(batch),
    })
    if (!response.ok) throw new Error(`ingest POST failed: ${response.status}`)
  }
}

const wait = (ms) => new Promise(r => setTimeout(r, ms))

const browser = await puppeteer.launch({ executablePath: CHROME, headless: 'new', args: ['--window-size=1500,1000'] })
try {
  const page = await browser.newPage()
  await page.setViewport({ width: 1500, height: 1000 })
  const pageErrors = []
  page.on('pageerror', e => pageErrors.push(String(e)))

  // The SSE stream keeps a request open forever — networkidle never fires.
  await page.goto(BASE, { waitUntil: 'domcontentloaded', timeout: 20000 })
  await page.waitForSelector('.log-viewer', { timeout: 10000 })
  check('viewer panel renders', true)

  // 1. Live SSE: injected events appear as rows with highlighted property values.
  await injectEvents(5, { tag: 'LIVE' })
  await wait(1200)
  const liveState = await page.evaluate(() => ({
    rows: document.querySelectorAll('.log-row').length,
    hasValueSpan: !!document.querySelector('.log-row__value'),
    hasLevelBadge: !!document.querySelector('.log-row__level'),
    liveDot: !!document.querySelector('.log-status__dot--live'),
  }))
  check('SSE rows render', liveState.rows >= 5, `rows=${liveState.rows}`)
  check('property values highlighted', liveState.hasValueSpan)
  check('level badge present', liveState.hasLevelBadge)
  check('connection dot shows live', liveState.liveDot)

  // 2. Pause freezes the view; resume catches up from the ring buffer.
  const pauseButton = await page.$$eval('.log-btn', (buttons) => {
    const target = buttons.find(b => b.textContent.trim() === 'Pause')
    target?.click()
    return !!target
  })
  check('pause button exists', pauseButton)
  const rowsAtPause = await page.$$eval('.log-row', r => r.length)
  await injectEvents(30, { tag: 'PAUSED' })
  await wait(1200)
  const rowsWhilePaused = await page.$$eval('.log-row', r => r.length)
  check('paused view frozen', rowsWhilePaused === rowsAtPause, `${rowsAtPause} -> ${rowsWhilePaused}`)
  await page.$$eval('.log-btn', (buttons) => buttons.find(b => b.textContent.trim() === 'Resume')?.click())
  await wait(800)
  const shownAfterResume = await page.$eval('.log-status__count', el => el.textContent.replace(/,/g, ''))
  const shownCount = Number(shownAfterResume.match(/(?<shown>\d+)\s*\//)?.groups?.shown ?? 0)
  check('resume catches up with buffered events', shownCount >= rowsAtPause + 30, shownAfterResume.trim())

  // 3. Level show/hide: hide the app information lane, E2E rows vanish; show, they return.
  const shownBeforeHide = await page.$$eval('.log-row', r => r.length)
  await page.$$eval('.log-filter-chip', (chips) => {
    // The "app" chip inside the Information family.
    const infoChip = chips.find(c => c.closest('.log-filter-family')?.textContent.includes('Information')
      && !c.classList.contains('log-filter-chip--off') && c.textContent.trim().toLowerCase().includes('app'))
    infoChip?.click()
  })
  await wait(500)
  const rowsAfterHide = await page.$$eval('.log-row', r => r.length)
  check('hiding a level removes its rows from view', rowsAfterHide < shownBeforeHide, `${shownBeforeHide} -> ${rowsAfterHide}`)
  await page.$$eval('.log-filter-chip', (chips) => {
    chips.find(c => c.classList.contains('log-filter-chip--off'))?.click()
  })
  await wait(500)
  const rowsAfterShow = await page.$$eval('.log-row', r => r.length)
  check('showing the level brings rows back', rowsAfterShow >= shownBeforeHide, `${rowsAfterHide} -> ${rowsAfterShow}`)

  // 4. Follow mode: with follow on, viewport sticks to the bottom after a burst.
  await injectEvents(200, { tag: 'FOLLOW' })
  await wait(1500)
  const followState = await page.$eval('.log-viewport', el =>
    ({ atBottom: el.scrollHeight - el.scrollTop - el.clientHeight < 60, scrollHeight: el.scrollHeight }))
  check('follow keeps viewport pinned to newest', followState.atBottom, JSON.stringify(followState))
  // Scrolling up disengages follow and reveals the jump-to-live affordance.
  await page.$eval('.log-viewport', el => { el.scrollTop = 0; el.dispatchEvent(new Event('scroll')) })
  await wait(400)
  const jumpVisible = await page.$$eval('.log-btn', buttons => buttons.some(b => b.textContent.includes('Jump to live')))
  check('manual scroll-up disengages follow (jump-to-live appears)', jumpVisible)

  // 5. Ring cap: push past 25k, buffered counter caps at exactly 25,000.
  await injectEvents(26000, { tag: 'CAP' })
  await wait(4000)
  const counter = await page.$eval('.log-status__count', el => el.textContent.replace(/,/g, ''))
  check('buffer caps at 25000', /\/\s*25000\s*shown/.test(counter), counter.trim())

  // 6. Clear empties the buffer.
  await page.$$eval('.log-btn', (buttons) => buttons.find(b => b.textContent.trim() === 'Clear')?.click())
  await wait(400)
  const afterClear = await page.evaluate(() => ({
    rows: document.querySelectorAll('.log-row').length,
    empty: !!document.querySelector('.log-empty'),
  }))
  check('clear empties the view', afterClear.rows === 0 && afterClear.empty, JSON.stringify(afterClear))

  // 7. Resizable panel + no page errors across the whole run.
  const resizable = await page.$eval('.log-viewer', el => getComputedStyle(el).resize)
  check('panel is user-resizable', resizable === 'both' || resizable === 'vertical', `resize=${resizable}`)
  check('zero page errors across the run', pageErrors.length === 0, pageErrors.slice(0, 2).join(' | '))
} finally {
  await browser.close()
}

console.log(results.join('\n'))
console.log(failures === 0 ? '\nE2E: ALL PASS' : `\nE2E: ${failures} FAILURE(S)`)
process.exit(failures === 0 ? 0 : 1)
