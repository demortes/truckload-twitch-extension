// Pure resolution of the dev-only "show a real channel's live data in a plain browser tab" mode.
// No import.meta here so it can be unit-tested; devBackend.js feeds it the real values.

/** What a dev tab talks to unless told otherwise: the production Worker. */
export const DEV_DEFAULT_BACKEND = 'https://truckload.demortes.com';

/**
 * @param {object} input
 * @param {boolean} input.isDev   true only for the Vite dev server (never in a production build)
 * @param {string} input.search   window.location.search
 * @param {{VITE_DEV_CHANNEL?: string, VITE_DEV_BACKEND?: string}} input.env
 * @returns {{channel: string, backend: string, channelSource: 'url'|'env', backendSource: 'url'|'env'|'default'} | null}
 *   null means "no channel configured": the views keep showing mock data.
 */
export function resolveDevBackend({ isDev, search, env }) {
  if (!isDev) return null;

  const params = new URLSearchParams(search);
  const channelFromUrl = params.get('channel');
  const channel = channelFromUrl || env?.VITE_DEV_CHANNEL;
  if (!channel || !/^[\w-]+$/.test(channel)) return null;

  const backendFromUrl = params.get('backend');
  const backendRaw = backendFromUrl || env?.VITE_DEV_BACKEND || DEV_DEFAULT_BACKEND;
  const backend = backendRaw.replace(/\/+$/, '');
  if (!/^https?:\/\//.test(backend)) return null;

  return {
    channel,
    backend,
    channelSource: channelFromUrl ? 'url' : 'env',
    backendSource: backendFromUrl ? 'url' : env?.VITE_DEV_BACKEND ? 'env' : 'default',
  };
}