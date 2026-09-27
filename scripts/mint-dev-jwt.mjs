#!/usr/bin/env node
// Zero-dependency helper for local development: mints a broadcaster JWT signed
// the same way Twitch would, so you can call POST/GET /api/channels/keys
// against a local backend without a real Twitch extension context.
//
// Usage:
//   node scripts/mint-dev-jwt.mjs --channel 12345 --secret <base64-extension-secret> [--role broadcaster]

import { createHmac } from 'node:crypto';

function parseArgs(argv) {
  const args = { role: 'broadcaster' };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--channel') args.channel = argv[++i];
    else if (argv[i] === '--secret') args.secret = argv[++i];
    else if (argv[i] === '--role') args.role = argv[++i];
  }
  return args;
}

function base64url(input) {
  return Buffer.from(input).toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

const { channel, secret, role } = parseArgs(process.argv.slice(2));

if (!channel || !secret) {
  console.error('Usage: node scripts/mint-dev-jwt.mjs --channel <channelId> --secret <base64Secret> [--role broadcaster|viewer]');
  process.exit(1);
}

const header = { alg: 'HS256', typ: 'JWT' };
const now = Math.floor(Date.now() / 1000);
const payload = {
  channel_id: channel,
  role,
  exp: now + 5 * 60,
  iat: now,
};

const encodedHeader = base64url(JSON.stringify(header));
const encodedPayload = base64url(JSON.stringify(payload));
const signingInput = `${encodedHeader}.${encodedPayload}`;

const signature = createHmac('sha256', Buffer.from(secret, 'base64'))
  .update(signingInput)
  .digest('base64')
  .replace(/\+/g, '-')
  .replace(/\//g, '_')
  .replace(/=+$/, '');

console.log(`${signingInput}.${signature}`);
