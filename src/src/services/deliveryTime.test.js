import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatDeliveryTime } from './deliveryTime.js';

const NOW = Date.parse('2026-11-11T20:00:00Z');
const at = (iso, mode, extra = {}) => formatDeliveryTime(iso, mode, { now: NOW, locale: 'en-US', timeZone: 'UTC', ...extra });

test('"ago" keeps the existing relative wording', () => {
  assert.equal(at('2026-11-11T19:59:40Z', 'ago'), 'just now');
  assert.equal(at('2026-11-11T19:55:00Z', 'ago'), '5m ago');
  assert.equal(at('2026-11-11T17:00:00Z', 'ago'), '3h ago');
  assert.equal(at('2026-11-09T20:00:00Z', 'ago'), '2d ago');
});

test('a time in the future is not shown as negative', () => {
  assert.equal(at('2026-11-11T20:30:00Z', 'ago'), 'just now');
});

test('12-hour clock, same day: time only', () => {
  assert.equal(at('2026-11-11T15:42:00Z', '12h'), '3:42 PM');
  assert.equal(at('2026-11-11T00:05:00Z', '12h'), '12:05 AM');
  assert.equal(at('2026-11-11T12:00:00Z', '12h'), '12:00 PM');
});

test('24-hour clock, same day: time only, with a proper midnight', () => {
  assert.equal(at('2026-11-11T15:42:00Z', '24h'), '15:42');
  assert.equal(at('2026-11-11T00:05:00Z', '24h'), '00:05');
});

test('a different day adds a short date so the time is not ambiguous', () => {
  assert.equal(at('2026-11-10T15:42:00Z', '12h'), 'Nov 10, 3:42 PM');
  assert.equal(at('2026-11-10T15:42:00Z', '24h'), 'Nov 10, 15:42');
});

test('the day boundary is judged in the viewer time zone', () => {
  // 02:00 UTC on the 12th is still the 11th in Chicago (UTC-6), same day as NOW (14:00 on the 11th there)
  const out = formatDeliveryTime('2026-11-12T02:00:00Z', '24h', { now: NOW, locale: 'en-US', timeZone: 'America/Chicago' });
  assert.equal(out, '20:00');
});

test('invalid or missing timestamps give an empty string in every mode', () => {
  for (const mode of ['ago', '12h', '24h']) {
    assert.equal(at('garbage', mode), '');
    assert.equal(at(undefined, mode), '');
  }
});

test('an unknown mode behaves like "ago"', () => {
  assert.equal(at('2026-11-11T19:55:00Z', 'sideways'), '5m ago');
});