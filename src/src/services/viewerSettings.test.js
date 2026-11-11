import { test } from 'node:test';
import assert from 'node:assert/strict';
import { DEFAULT_SETTINGS, STORAGE_KEY, createSettingsStore, parseSettings } from './viewerSettings.js';

function fakeStorage(initial = {}) {
  const data = { ...initial };
  return {
    data,
    getItem: (k) => (k in data ? data[k] : null),
    setItem: (k, v) => {
      data[k] = String(v);
    },
  };
}

test('missing or empty storage gives the defaults', () => {
  assert.deepEqual(parseSettings(null), DEFAULT_SETTINGS);
  assert.deepEqual(parseSettings(''), DEFAULT_SETTINGS);
});

test('garbage and wrong shapes give the defaults', () => {
  assert.deepEqual(parseSettings('not json'), DEFAULT_SETTINGS);
  assert.deepEqual(parseSettings('[1,2]'), DEFAULT_SETTINGS);
  assert.deepEqual(parseSettings('"hi"'), DEFAULT_SETTINGS);
  assert.deepEqual(parseSettings('null'), DEFAULT_SETTINGS);
});

test('each field is validated on its own, so one bad value does not reset the rest', () => {
  const out = parseSettings(JSON.stringify({ units: 'metric', layout: 'wide', deliveryTimes: '24h', sections: { truck: false, history: 'no' } }));
  assert.equal(out.units, 'metric');
  assert.equal(out.layout, DEFAULT_SETTINGS.layout);
  assert.equal(out.deliveryTimes, '24h');
  assert.equal(out.sections.truck, false);
  assert.equal(out.sections.history, true);
  assert.equal(out.sections.job, true);
});

test('unknown keys are dropped', () => {
  const out = parseSettings(JSON.stringify({ units: 'metric', evil: '<script>', sections: { nope: false } }));
  assert.equal('evil' in out, false);
  assert.equal('nope' in out.sections, false);
});

test('the store starts from what is saved', () => {
  const storage = fakeStorage({ [STORAGE_KEY]: JSON.stringify({ units: 'metric' }) });
  assert.equal(createSettingsStore(storage).get().units, 'metric');
});

test('set merges a patch, validates it, saves it and notifies', () => {
  const storage = fakeStorage();
  const store = createSettingsStore(storage);
  let calls = 0;
  store.subscribe(() => calls++);

  store.set({ units: 'metric', sections: { truck: false } });

  assert.equal(store.get().units, 'metric');
  assert.equal(store.get().sections.truck, false);
  assert.equal(store.get().sections.job, true);
  assert.equal(calls, 1);
  assert.equal(JSON.parse(storage.data[STORAGE_KEY]).units, 'metric');
});

test('an invalid patch value is ignored rather than stored', () => {
  const store = createSettingsStore(fakeStorage());
  store.set({ units: 'furlongs' });
  assert.equal(store.get().units, DEFAULT_SETTINGS.units);
});

test('get returns a stable reference until something changes (needed by useSyncExternalStore)', () => {
  const store = createSettingsStore(fakeStorage());
  const a = store.get();
  assert.equal(store.get(), a);
  store.set({ units: 'metric' });
  assert.notEqual(store.get(), a);
  const b = store.get();
  store.set({ units: 'metric' }); // no change
  assert.equal(store.get(), b);
});

test('without storage the settings still work for the session', () => {
  const store = createSettingsStore(null);
  store.set({ layout: 'compact' });
  assert.equal(store.get().layout, 'compact');
});

test('a storage that throws never breaks the store', () => {
  const broken = {
    getItem: () => {
      throw new Error('blocked');
    },
    setItem: () => {
      throw new Error('quota');
    },
  };
  const store = createSettingsStore(broken);
  assert.deepEqual(store.get(), DEFAULT_SETTINGS);
  store.set({ units: 'metric' });
  assert.equal(store.get().units, 'metric');
});

test('reload picks up a change made by another view in the same browser', () => {
  const storage = fakeStorage();
  const store = createSettingsStore(storage);
  let calls = 0;
  store.subscribe(() => calls++);

  storage.setItem(STORAGE_KEY, JSON.stringify({ units: 'imperial', layout: 'compact' }));
  store.reload();

  assert.equal(store.get().layout, 'compact');
  assert.equal(calls, 1);
  store.reload(); // nothing new
  assert.equal(calls, 1);
});

test('unsubscribe stops notifications', () => {
  const store = createSettingsStore(fakeStorage());
  let calls = 0;
  const off = store.subscribe(() => calls++);
  off();
  store.set({ units: 'metric' });
  assert.equal(calls, 0);
});