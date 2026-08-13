/**
 * Render every deliverable in one command.
 *
 *   npm run render:all          everything
 *   npm run render:titles       the 6s opener + the nine 4s chapter cards
 *   npm run render:decks        the nine chapter decks
 *   node render-all.mjs --only=mechanisms
 *
 * Output lands in `out/`, which is gitignored — the source of truth is the
 * code in `src/`, never the rendered file. The bundle is built once and
 * reused across every composition, so a full run pays the bundler cost once.
 */
import { bundle } from '@remotion/bundler';
import { renderMedia, selectComposition } from '@remotion/renderer';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(ROOT, 'out');

const CHAPTER_IDS = ['01', '02', '03', '04', '05', '06', '07', '08', '09'];

const GROUPS = {
  titles: [
    { id: 'series-opener', out: 'titles/series-opener.mp4' },
    ...CHAPTER_IDS.map((c) => ({
      id: `chapter-card-${c}`,
      out: `titles/chapter-card-${c}.mp4`,
    })),
  ],
  decks: CHAPTER_IDS.map((c) => ({ id: `deck-${c}`, out: `decks/deck-${c}.mp4` })),
  mechanisms: CHAPTER_IDS.map((c) => ({
    id: `mechanism-${c}`,
    out: `mechanisms/mechanism-${c}.mp4`,
  })),
};

const only = process.argv.find((a) => a.startsWith('--only='))?.slice('--only='.length);
const groups = only ? [only] : Object.keys(GROUPS);

for (const group of groups) {
  if (!GROUPS[group]) {
    console.error(`Unknown group "${group}". Expected one of: ${Object.keys(GROUPS).join(', ')}`);
    process.exit(1);
  }
}

console.log('Bundling…');
const serveUrl = await bundle({
  entryPoint: path.join(ROOT, 'src', 'index.ts'),
  onProgress: () => undefined,
});

const targets = groups.flatMap((g) => GROUPS[g]);
let done = 0;

for (const target of targets) {
  const outputLocation = path.join(OUT, target.out);
  await mkdir(path.dirname(outputLocation), { recursive: true });

  const composition = await selectComposition({ serveUrl, id: target.id });

  process.stdout.write(`[${++done}/${targets.length}] ${target.id} → ${target.out} `);
  await renderMedia({
    composition,
    serveUrl,
    codec: 'h264',
    crf: 16,
    imageFormat: 'jpeg',
    outputLocation,
    overwrite: true,
  });
  console.log('done');
}

console.log(`\nRendered ${targets.length} composition(s) into ${OUT}`);
