import React from 'react';
import { AbsoluteFill, Composition } from 'remotion';
import { FPS, HD } from './tokens';
import { CHAPTERS } from './data/chapters';
import { SeriesOpener } from './title/SeriesOpener';
import { ChapterCard } from './title/ChapterCard';
import { CHAPTER_CARD_FRAMES, OPENER_FRAMES } from './title/timing';
import { Deck } from './deck/Deck';
import { deckFrames } from './deck/timing';
import { ChapterAnimation } from './anim';
import { Frame } from './components/Frame';

/**
 * Every composition in the project.
 *
 * Four families, all driven by `data/chapters.ts`:
 *   series-opener     — the 6s master, one render, heads every video
 *   chapter-card-NN   — the 4s chapter title card, one render per chapter
 *   deck-NN           — that chapter's full slide deck
 *   mechanism-NN      — that chapter's animation alone, for a cutaway
 *
 * There is no per-chapter component anywhere: a tenth chapter is a tenth row
 * in the data file.
 */
export const RemotionRoot: React.FC = () => (
  <>
    <Composition
      id="series-opener"
      component={SeriesOpener}
      durationInFrames={OPENER_FRAMES}
      fps={FPS}
      width={HD.width}
      height={HD.height}
    />

    {CHAPTERS.map((chapter) => (
      <Composition
        key={`card-${chapter.id}`}
        id={`chapter-card-${chapter.id}`}
        component={ChapterCard}
        durationInFrames={CHAPTER_CARD_FRAMES}
        fps={FPS}
        width={HD.width}
        height={HD.height}
        defaultProps={{ chapter }}
      />
    ))}

    {CHAPTERS.map((chapter) => (
      <Composition
        key={`deck-${chapter.id}`}
        id={`deck-${chapter.id}`}
        component={Deck}
        durationInFrames={deckFrames(chapter)}
        fps={FPS}
        width={HD.width}
        height={HD.height}
        defaultProps={{ chapter }}
      />
    ))}

    {CHAPTERS.map((chapter) => (
      <Composition
        key={`mech-${chapter.id}`}
        id={`mechanism-${chapter.id}`}
        component={MechanismCutaway}
        durationInFrames={16 * FPS}
        fps={FPS}
        width={HD.width}
        height={HD.height}
        defaultProps={{ chapterId: chapter.id }}
      />
    ))}
  </>
);

/** A chapter's mechanism animation on its own, framed, for a cutaway shot. */
const MechanismCutaway: React.FC<{ chapterId: string }> = ({ chapterId }) => (
  <Frame>
    <AbsoluteFill style={{ padding: '7% 6%' }}>
      <ChapterAnimation chapterId={chapterId} />
    </AbsoluteFill>
  </Frame>
);
