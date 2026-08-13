/**
 * Remotion CLI configuration.
 *
 * Node APIs (render-all.mjs) do not read this file — they pass the same
 * options explicitly, so the two paths stay in agreement. Change both.
 */
import { Config } from '@remotion/cli/config';

Config.setVideoImageFormat('jpeg');
Config.setOverwriteOutput(true);
Config.setCodec('h264');
Config.setCrf(16);
