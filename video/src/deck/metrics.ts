/**
 * Slide geometry, solved once.
 *
 * The shell and the slide bodies both need to know how much room the body
 * actually has. Writing that arithmetic in two places is how a code slide ends
 * up printing over its own footer: the shell let the heading grow to two lines
 * and the body sized itself from width alone, never knowing it had lost 70px.
 *
 * So the numbers live here, the shell paints to them, and a body that has to
 * fit its content (the code panel) solves against `bodyHeight`. The heading
 * block is a RESERVED height — two lines' worth whether the heading uses one
 * line or two — which is what makes `bodyHeight` a constant an editor can rely
 * on across every slide in a deck.
 */

/** Heading lines the shell reserves room for, used or not. */
const HEADING_LINES = 2;
const HEADING_LINE_HEIGHT = 1.06;
const FOOTER_LINE_HEIGHT = 1.5;

export type SlideMetrics = {
  padX: number;
  padTop: number;
  padBottom: number;
  headingSize: number;
  headingBlock: number;
  ruleGap: number;
  bodyGap: number;
  footerSize: number;
  footerBlock: number;
  /** Room the body has, after the heading and footer take theirs. */
  bodyWidth: number;
  bodyHeight: number;
};

export function slideMetrics(width: number, height: number): SlideMetrics {
  const padX = Math.round(width * 0.085);
  const padTop = Math.round(height * 0.11);
  const padBottom = Math.round(height * 0.1);

  const headingSize = Math.round(width * 0.037);
  const ruleGap = Math.round(height * 0.026);
  const ruleHeight = 2;
  const headingBlock =
    ruleHeight + ruleGap + Math.ceil(headingSize * HEADING_LINE_HEIGHT * HEADING_LINES);

  const bodyGap = Math.round(height * 0.05);

  const footerSize = Math.round(width * 0.0105);
  const footerRule = 1;
  const footerPad = Math.round(height * 0.018);
  const footerBlock = footerRule + footerPad + Math.ceil(footerSize * FOOTER_LINE_HEIGHT);

  return {
    padX,
    padTop,
    padBottom,
    headingSize,
    headingBlock,
    ruleGap,
    bodyGap,
    footerSize,
    footerBlock,
    bodyWidth: width - padX * 2,
    bodyHeight: height - padTop - padBottom - headingBlock - bodyGap - footerBlock,
  };
}
