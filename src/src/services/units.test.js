import { test } from 'node:test';
import assert from 'node:assert/strict';
import { applyViewerUnits, convertDistance, convertSpeed, effectiveUnits } from './units.js';

test('effectiveUnits follows the stream on auto and honours an explicit choice', () => {
  assert.equal(effectiveUnits('auto', 'imperial'), 'imperial');
  assert.equal(effectiveUnits('auto', 'metric'), 'metric');
  assert.equal(effectiveUnits('metric', 'imperial'), 'metric');
  assert.equal(effectiveUnits('imperial', 'metric'), 'imperial');
});

test('effectiveUnits falls back safely on junk input', () => {
  assert.equal(effectiveUnits('bananas', 'metric'), 'metric');
  assert.equal(effectiveUnits(undefined, 'imperial'), 'imperial');
  assert.equal(effectiveUnits('auto', 'bananas'), 'imperial');
});

test('converts miles to km and back, rounded to whole numbers', () => {
  assert.equal(convertDistance(100, 'imperial', 'metric'), 161);
  assert.equal(convertDistance(100, 'metric', 'imperial'), 62);
  assert.equal(convertDistance(372, 'imperial', 'metric'), 599);
  assert.equal(convertDistance(124532, 'imperial', 'metric'), 200415);
  assert.equal(convertDistance(0, 'imperial', 'metric'), 0);
});

test('speed converts the same way (mph and km/h)', () => {
  assert.equal(convertSpeed(60, 'imperial', 'metric'), 97);
  assert.equal(convertSpeed(100, 'metric', 'imperial'), 62);
});

test('the same units are returned untouched, with no rounding side effects', () => {
  assert.equal(convertDistance(372, 'imperial', 'imperial'), 372);
  assert.equal(convertDistance(12.5, 'metric', 'metric'), 12.5);
});

test('missing or non-numeric values pass through unchanged', () => {
  assert.equal(convertDistance(null, 'imperial', 'metric'), null);
  assert.equal(convertDistance(undefined, 'imperial', 'metric'), undefined);
  assert.ok(Number.isNaN(convertDistance(NaN, 'imperial', 'metric')));
});

const sample = {
  units: 'imperial',
  job: { active: true, cargo: 'Steel', source: 'Reno', destination: 'Boise', distance: 300, etaMinutes: 200 },
  truck: { make: 'Peterbilt', odometer: 1000, fuelPercent: 80 },
  dashboard: { speed: 60, signal: 'off', restMinutes: 120 },
  history: [{ id: 'a', cargo: 'Coal', distance: 100 }],
};

test('applyViewerUnits converts every distance and speed and reports the units shown', () => {
  const out = applyViewerUnits(sample, 'metric');
  assert.equal(out.units, 'metric');
  assert.equal(out.job.distance, 483);
  assert.equal(out.truck.odometer, 1609);
  assert.equal(out.dashboard.speed, 97);
  assert.equal(out.history[0].distance, 161);
});

test('applyViewerUnits leaves non-distance fields alone', () => {
  const out = applyViewerUnits(sample, 'metric');
  assert.equal(out.job.etaMinutes, 200);
  assert.equal(out.job.cargo, 'Steel');
  assert.equal(out.truck.fuelPercent, 80);
  assert.equal(out.dashboard.restMinutes, 120);
});

test('applyViewerUnits does not mutate its input', () => {
  const copy = JSON.parse(JSON.stringify(sample));
  applyViewerUnits(sample, 'metric');
  assert.deepEqual(sample, copy);
});

test('auto, or matching units, returns the same object so React can skip work', () => {
  assert.equal(applyViewerUnits(sample, 'auto'), sample);
  assert.equal(applyViewerUnits(sample, 'imperial'), sample);
});

test('applyViewerUnits copes with missing pieces', () => {
  const out = applyViewerUnits({ units: 'metric', job: null, truck: null, dashboard: null, history: [] }, 'imperial');
  assert.equal(out.units, 'imperial');
  assert.equal(out.job, null);
  assert.equal(out.truck, null);
  assert.equal(out.dashboard, null);
  assert.deepEqual(out.history, []);
  assert.deepEqual(applyViewerUnits({ units: 'metric' }, 'imperial').history, undefined);
});