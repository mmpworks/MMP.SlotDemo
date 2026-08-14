/**
 * Remotion CLI configuration.
 *
 * Node APIs (render-all.mjs) do not read this file — they pass the same
 * options explicitly, so the two paths stay in agreement. Change both.
 */
import { Config } from '@remotion/cli/config';

// PNG, not JPEG — see the note in render-all.mjs. Near-black gradients band
// badly under chroma subsampling, and this project is almost entirely those.
Config.setVideoImageFormat('png');
Config.setOverwriteOutput(true);
Config.setCodec('h264');
Config.setCrf(16);
