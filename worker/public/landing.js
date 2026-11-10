// Shows this deployment's own ingest URL in the setup steps (the page itself is static).
const el = document.getElementById("ingest-url");
if (el) el.textContent = `${location.origin}/api/ingest`;