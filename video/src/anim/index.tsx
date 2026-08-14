import React from 'react';
import { Ch01TwoLanes } from './Ch01TwoLanes';
import { Ch02SeedFanOut } from './Ch02SeedFanOut';
import { Ch03StripWindow } from './Ch03StripWindow';
import { Ch04Pricing } from './Ch04Pricing';
import { Ch05WeightTree } from './Ch05WeightTree';
import { Ch06Quotas } from './Ch06Quotas';
import { Ch07GameAsData } from './Ch07GameAsData';
import { Ch08Referee } from './Ch08Referee';
import { Ch09ByteWindow } from './Ch09ByteWindow';

/**
 * One mechanism animation per chapter, keyed by chapter id.
 *
 * Each animation illustrates the one thing its chapter is about, using that
 * chapter's own numbers. They are registered as standalone compositions too,
 * so an editor can pull a single mechanism without rendering its deck.
 */
export const CHAPTER_ANIMATIONS: Record<string, React.FC> = {
  '01': Ch01TwoLanes,
  '02': Ch02SeedFanOut,
  '03': Ch03StripWindow,
  '04': Ch04Pricing,
  '05': Ch05WeightTree,
  '06': Ch06Quotas,
  '07': Ch07GameAsData,
  '08': Ch08Referee,
  '09': Ch09ByteWindow,
};

export const ChapterAnimation: React.FC<{ chapterId: string }> = ({ chapterId }) => {
  const Component = CHAPTER_ANIMATIONS[chapterId];
  if (!Component) throw new Error(`No animation registered for chapter "${chapterId}"`);
  return <Component />;
};
