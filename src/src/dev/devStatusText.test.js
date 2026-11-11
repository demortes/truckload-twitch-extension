import { test } from 'node:test';
import assert from 'node:assert/strict';
import { describeDevStatus } from './devStatusText.js';

const NOW = 1_800_000_000_000;
const dev = { channel: '9285', backend: 'https://truckload.demortes.com', channelSource: 'url', backendSource: 'default' };
const ok = { phase: 'ok', error: null };
const telemetry = (ageSeconds, extra = {}) => ({ connected: true, game: 'ats', lastTs: NOW / 1000 - ageSeconds, ...extra });
const describe = (over) => describeDevStatus({ dev, status: ok, telemetry: telemetry(5), now: NOW, ...over });

test('without a channel it says it is mock data and how to get live data', () => {
  const r = describe({ dev: null });
  assert.equal(r.level, 'info');
  assert.match(r.text, /mock data/);
  assert.match(r.text, /\?channel=/);
  assert.match(r.text, /VITE_DEV_CHANNEL/);
});

test('fresh live data names the backend, channel, game and age', () => {
  const r = describe({});
  assert.equal(r.level, 'ok');
  assert.match(r.text, /truckload\.demortes\.com \(default\) \/ channel 9285/);
  assert.match(r.text, /ats running, updated 5s ago/);
});

test('says which backend is used when it was given explicitly', () => {
  const r = describe({ dev: { ...dev, backend: 'http://localhost:8787', backendSource: 'url' } });
  assert.match(r.text, /localhost:8787 \/ channel 9285/);
  assert.doesNotMatch(r.text, /\(default\)/);
});

test('shows connecting before the first poll answers', () => {
  assert.equal(describe({ status: { phase: 'idle' } }).level, 'info');
  assert.match(describe({ status: { phase: 'idle' } }).text, /connecting/);
});

test('surfaces backend errors instead of leaving the page blank', () => {
  const r = describe({ status: { phase: 'error', error: 'no data stored for this channel yet (404)' } });
  assert.equal(r.level, 'error');
  assert.match(r.text, /404/);
});

test('old data is a warning, and past two minutes it says the overlay hides', () => {
  const stale = describe({ telemetry: telemetry(100) });
  assert.equal(stale.level, 'warn');
  assert.match(stale.text, /100s old|2m old/);
  assert.doesNotMatch(stale.text, /hide/);

  const hidden = describe({ telemetry: telemetry(300) });
  assert.match(hidden.text, /5m old/);
  assert.match(hidden.text, /overlay and video component hide/);
});

test('a live bridge with the game closed is called out', () => {
  const r = describe({ telemetry: telemetry(10, { connected: false }) });
  assert.equal(r.level, 'warn');
  assert.match(r.text, /game is not running/);
});

test('a payload without a timestamp does not produce NaN text', () => {
  const r = describe({ telemetry: { connected: true, game: 'ats', lastTs: null } });
  assert.equal(r.level, 'warn');
  assert.doesNotMatch(r.text, /NaN|Infinity/);
});