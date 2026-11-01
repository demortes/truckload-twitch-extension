import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseConfig, parsePlayerIds } from './convoyConfig.js';

test('empty or missing content gives the defaults', () => {
  assert.deepEqual(parseConfig(undefined), { playerIds: [], showWhenOffline: false });
  assert.deepEqual(parseConfig(''), { playerIds: [], showWhenOffline: false });
});

test('malformed content gives the defaults', () => {
  assert.deepEqual(parseConfig('not json'), { playerIds: [], showWhenOffline: false });
  assert.deepEqual(parseConfig('{"playerIds":"nope","showWhenOffline":"yes"}'), { playerIds: [], showWhenOffline: false });
});

test('reads both settings from one segment', () => {
  const content = JSON.stringify({ playerIds: ['1', '2', '', 3], showWhenOffline: true });
  assert.deepEqual(parseConfig(content), { playerIds: ['1', '2'], showWhenOffline: true });
});

test('configs saved before showWhenOffline existed still load', () => {
  assert.deepEqual(parseConfig('{"playerIds":["7"]}'), { playerIds: ['7'], showWhenOffline: false });
  assert.deepEqual(parsePlayerIds('{"playerIds":["7"]}'), ['7']);
});
