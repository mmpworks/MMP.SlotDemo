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
import { getCompositions, renderMedia, selectComposition } from '@remotion/renderer';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(ROOT, 'out');

/**
 * Which output folder a composition belongs in, by its id prefix. Nothing here
 * knows how many chapters exist — the compositions come from the bundle, which
 * builds them from `src/data/chapters.ts`. A tenth chapter is a tenth row in
 * that file and nothing in this script changes.
 */
const GROUP_OF = [
  { group: 'titles', match: (id) => id === 'series-opener' || id.startsWith('chapter-card-') },
  { group: 'decks', match: (id) => id.startsWith('deck-') },
  { group: 'mechanisms', match: (id) => id.startsWith('mechanism-') },
];

const only = process.argv.find((a) => a.startsWith('--only='))?.slice('--only='.length);
const groupNames = GROUP_OF.map((g) => g.group);

if (only && !groupNames.includes(only)) {
  console.error(`Unknown group "${only}". Expected one of: ${groupNames.join(', ')}`);
  process.exit(1);
}

console.log('Bundling…');
const serveUrl = await bundle({
  entryPoint: path.join(ROOT, 'src', 'index.ts'),
  onProgress: () => undefined,
});

const targets = (await getCompositions(serveUrl))
  .map(({ id }) => {
    const entry = GROUP_OF.find((g) => g.match(id));
    return entry ? { id, group: entry.group, out: `${entry.group}/${id}.mp4` } : null;
  })
  .filter((t) => t !== null)
  .filter((t) => !only || t.group === only)
  .sort((a, b) => a.out.localeCompare(b.out));

if (targets.length === 0) {
  console.error('No compositions matched. Has a composition id prefix changed?');
  process.exit(1);
}

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
    // PNG, not JPEG. The opener's sunburst is low-opacity brass over
    // near-black; JPEG's chroma subsampling bands it into visible rings.
    imageFormat: 'png',
    // Every composition is silent by convention — these cut under live
    // narration. Dropping the track keeps the file's duration exactly the
    // composition's, rather than the silent track's slightly longer one.
    muted: true,
    outputLocation,
    overwrite: true,
  });
  console.log('done');
}

console.log(`\nRendered ${targets.length} composition(s) into ${OUT}`);
