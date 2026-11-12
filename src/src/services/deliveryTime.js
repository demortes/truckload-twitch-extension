// How a completed delivery's time is shown in Recent Deliveries: "5m ago", or a clock time in the viewer's own
// time zone (12-hour or 24-hour). The extension shows no other clock times; everything else is a duration.

function ago(ms, now) {
  const diffMinutes = Math.max(0, Math.round((now - ms) / 60_000));
  if (diffMinutes < 1) return 'just now';
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  const hours = Math.floor(diffMinutes / 60);
  if (hours < 24) return `${hours}h ago`;

  return `${Math.floor(hours / 24)}d ago`;
}

// Newer ICU puts a narrow no-break space before AM/PM; a plain space renders the same everywhere.
const tidy = (text) => text.replace(/\u202f/g, ' ');

function clock(ms, mode, now, locale, timeZone) {
  // h23, not hour12:false, which can print "24:05" for just after midnight.
  const hourCycle = mode === '12h' ? 'h12' : 'h23';
  const time = tidy(new Intl.DateTimeFormat(locale, { hour: 'numeric', minute: '2-digit', hourCycle, timeZone }).format(ms));

  const day = new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit', timeZone });
  if (day.format(ms) === day.format(now)) return time;

  // Another day: add a short date so "3:42 PM" is not ambiguous.
  return `${tidy(new Intl.DateTimeFormat(locale, { month: 'short', day: 'numeric', timeZone }).format(ms))}, ${time}`;
}

/**
 * @param {string|number|Date} completedAt
 * @param {'ago'|'12h'|'24h'} mode anything else behaves like 'ago'
 * @param {{now?: number, locale?: string, timeZone?: string}} [options] for tests; the defaults are the viewer's
 * @returns {string} '' when the timestamp is invalid
 */
export function formatDeliveryTime(completedAt, mode, { now = Date.now(), locale, timeZone } = {}) {
  const ms = new Date(completedAt).getTime();
  if (Number.isNaN(ms)) return '';
  return mode === '12h' || mode === '24h' ? clock(ms, mode, now, locale, timeZone) : ago(ms, now);
}