// Shows this deployment's own ingest URL in the setup steps (the page itself is static).
for (const el of document.querySelectorAll(".ingest-url")) el.textContent = `${location.origin}/api/ingest`;