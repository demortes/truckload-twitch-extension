// Publishes the docs site and the logo as static assets. Run by `npm run dev` / `npm run deploy` and CI
// before wrangler bundles ./public. The copies are gitignored; ../docs is the single source of truth.

import { cpSync, copyFileSync, mkdirSync, rmSync } from "node:fs";
import { join } from "node:path";

const here = import.meta.dirname;
const docs = join(here, "../../docs");
const pub = join(here, "../public");

rmSync(join(pub, "about"), { recursive: true, force: true });
mkdirSync(join(pub, "about"), { recursive: true });

// The marketing page and its assets live at /about/ (its asset links are relative).
copyFileSync(join(docs, "index.html"), join(pub, "about/index.html"));
cpSync(join(docs, "assets"), join(pub, "about/assets"), { recursive: true });

// The landing page, the favicon and any <link rel=icon> use the same logo.
copyFileSync(join(docs, "assets/logo.png"), join(pub, "logo.png"));
copyFileSync(join(docs, "assets/logo.png"), join(pub, "favicon.ico"));

console.log("Static assets synced from ../docs into public/");