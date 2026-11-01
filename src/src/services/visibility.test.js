import { test } from 'node:test';
import assert from 'node:assert/strict';
import { OFFLINE_GRACE_MS, isTruckGame, shouldShowOverlay } from './visibility.js';

const NOW = 1_000_000_000;
const base = { connected: false, stale: false, lastLiveAt: null, now: NOW, game: '', showWhenOffline: false };

test('recognises ATS and ETS2 categories', () => {
  assert.equal(isTruckGame('American Truck Simulator'), true);
  assert.equal(isTruckGame('Euro Truck Simulator 2'), true);
  assert.equal(isTruckGame('Just Chatting'), false);
  assert.equal(isTruckGame(''), false);
  assert.equal(isTruckGame(undefined), false);
});

test('live telemetry always shows, even in another category', () => {
  assert.equal(shouldShowOverlay({ ...base, connected: true, game: 'Just Chatting' }), true);
});

test('stale telemetry is not live', () => {
  assert.equal(shouldShowOverlay({ ...base, connected: true, stale: true }), false);
});

test('never seen live data: hidden right away', () => {
  assert.equal(shouldShowOverlay(base), false);
});

test('recently live: stays up through the grace period, then hides', () => {
  assert.equal(shouldShowOverlay({ ...base, lastLiveAt: NOW - (OFFLINE_GRACE_MS - 1) }), true);
  assert.equal(shouldShowOverlay({ ...base, lastLiveAt: NOW - OFFLINE_GRACE_MS }), false);
});

test('a non-truck category hides it during the grace period', () => {
  assert.equal(shouldShowOverlay({ ...base, lastLiveAt: NOW - 1000, game: 'Just Chatting' }), false);
});

test('a truck category or an unknown one does not hide it by itself', () => {
  assert.equal(shouldShowOverlay({ ...base, lastLiveAt: NOW - 1000, game: 'Euro Truck Simulator 2' }), true);
  assert.equal(shouldShowOverlay({ ...base, lastLiveAt: NOW - 1000, game: '' }), true);
});

test('showWhenOffline keeps it up in a truck or unknown category, but not in another', () => {
  assert.equal(shouldShowOverlay({ ...base, showWhenOffline: true }), true);
  assert.equal(shouldShowOverlay({ ...base, showWhenOffline: true, game: 'American Truck Simulator' }), true);
  assert.equal(shouldShowOverlay({ ...base, showWhenOffline: true, game: 'Just Chatting' }), false);
});
