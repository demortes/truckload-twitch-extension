import { test } from 'node:test';
import assert from 'node:assert/strict';
import { DEV_DEFAULT_BACKEND, resolveDevBackend } from './devConfig.js';

const base = { isDev: true, search: '', env: {} };

test('never active outside the Vite dev server', () => {
  assert.equal(resolveDevBackend({ ...base, isDev: false, search: '?channel=9285' }), null);
});

test('no channel anywhere means mock data', () => {
  assert.equal(resolveDevBackend(base), null);
  assert.equal(resolveDevBackend({ ...base, search: '?backend=https://x.example' }), null);
});

test('a channel in the URL defaults to the production backend, not localhost', () => {
  assert.deepEqual(resolveDevBackend({ ...base, search: '?channel=9285' }), {
    channel: '9285',
    backend: DEV_DEFAULT_BACKEND,
    channelSource: 'url',
    backendSource: 'default',
  });
});

test('the URL backend wins and a trailing slash is trimmed', () => {
  const r = resolveDevBackend({ ...base, search: '?channel=9285&backend=http://localhost:8787/' });
  assert.equal(r.backend, 'http://localhost:8787');
  assert.equal(r.backendSource, 'url');
});

test('VITE_DEV_CHANNEL and VITE_DEV_BACKEND make every page live without query parameters', () => {
  const r = resolveDevBackend({ ...base, env: { VITE_DEV_CHANNEL: '9285', VITE_DEV_BACKEND: 'https://staging.example.com' } });
  assert.deepEqual(r, { channel: '9285', backend: 'https://staging.example.com', channelSource: 'env', backendSource: 'env' });
});

test('the URL overrides the env for both channel and backend', () => {
  const r = resolveDevBackend({
    ...base,
    search: '?channel=1&backend=http://localhost:8787',
    env: { VITE_DEV_CHANNEL: '9285', VITE_DEV_BACKEND: 'https://staging.example.com' },
  });
  assert.equal(r.channel, '1');
  assert.equal(r.backend, 'http://localhost:8787');
});

test('an unrelated VITE_BACKEND_URL is ignored (it used to hijack the dev backend)', () => {
  const r = resolveDevBackend({ ...base, search: '?channel=9285', env: { VITE_BACKEND_URL: 'http://localhost:8080' } });
  assert.equal(r.backend, DEV_DEFAULT_BACKEND);
});

test('rejects a channel with unsafe characters and a non-http backend', () => {
  assert.equal(resolveDevBackend({ ...base, search: '?channel=../etc' }), null);
  assert.equal(resolveDevBackend({ ...base, search: '?channel=9285&backend=javascript:alert(1)' }), null);
});