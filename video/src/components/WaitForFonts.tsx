import React, { useEffect, useState } from 'react';
import { cancelRender, continueRender, delayRender } from 'remotion';
import { fontsReady } from '../tokens/fonts';

/**
 * Holds the first frame until every font has loaded.
 *
 * Without this the renderer can capture a frame while the browser is still
 * falling back to a system face — the frame paints at the wrong metrics, and
 * because the size solvers in `fitText.ts` are calibrated to Jost, the layout
 * is wrong too. `delayRender` makes the wait explicit rather than a race that
 * usually wins.
 *
 * Every composition wraps in this, so it is one place rather than a rule each
 * composition has to remember.
 */
export const WaitForFonts: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [handle] = useState(() => delayRender('Loading fonts'));
  const [ready, setReady] = useState(false);

  useEffect(() => {
    fontsReady
      .then(() => {
        setReady(true);
        continueRender(handle);
      })
      .catch((err) => cancelRender(err));
  }, [handle]);

  if (!ready) return null;
  return <>{children}</>;
};
