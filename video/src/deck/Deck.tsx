import React from 'react';
import { AbsoluteFill, Series } from 'remotion';
import { FPS } from '../tokens';
import { Frame } from '../components/Frame';
import { SlideShell } from './SlideShell';
import { AnimBody, CodeBody, DeckOpener, PointsBody, StatBody } from './slides';
import { OPENER_SECONDS, slideFrames } from './timing';
import type { Chapter, Slide } from '../data/chapters';

/**
 * One chapter's deck.
 *
 * The deck is a `Series` of slides driven entirely by the chapter's row in
 * `data/chapters.ts` — there is no per-chapter component. Adding a slide to a
 * chapter is a data edit, and the deck's length follows from `deckFrames`.
 */
export const Deck: React.FC<{ chapter: Chapter }> = ({ chapter }) => {
  const total = chapter.slides.length;

  return (
    <AbsoluteFill>
      <Series>
        <Series.Sequence durationInFrames={Math.round(OPENER_SECONDS * FPS)}>
          <Frame>
            <AbsoluteFill style={{ padding: '11% 8.5%' }}>
              <DeckOpener chapter={chapter} />
            </AbsoluteFill>
          </Frame>
        </Series.Sequence>

        {chapter.slides.map((slide, i) => (
          <Series.Sequence key={`${slide.kind}-${slide.heading}`} durationInFrames={slideFrames(slide)}>
            <SlideShell
              heading={slide.heading}
              chapterNumber={chapter.number}
              chapterTitle={chapter.title}
              index={i + 1}
              total={total}
            >
              <SlideBody slide={slide} chapterId={chapter.id} />
            </SlideShell>
          </Series.Sequence>
        ))}
      </Series>
    </AbsoluteFill>
  );
};

const SlideBody: React.FC<{ slide: Slide; chapterId: string }> = ({ slide, chapterId }) => {
  switch (slide.kind) {
    case 'points':
      return <PointsBody points={slide.points} />;
    case 'stat':
      return <StatBody stats={slide.stats} note={slide.note} />;
    case 'code':
      return <CodeBody lines={slide.lines} source={slide.source} caption={slide.caption} />;
    case 'anim':
      return <AnimBody chapterId={chapterId} caption={slide.caption} />;
  }
};
