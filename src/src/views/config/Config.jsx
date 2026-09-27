import { useEffect, useState } from 'react';
import '../../index.css';
import './config.css';

const BACKEND_URL = import.meta.env.VITE_BACKEND_URL || 'http://localhost:8080';
const BRIDGE_RELEASES_URL = 'https://github.com/demortes/truckload-twitch-extension/releases/latest';

function useCountdown(refreshMs) {
  const [tick, setTick] = useState(0);
  useEffect(() => {
    const timer = setInterval(() => setTick((t) => t + 1), refreshMs);
    return () => clearInterval(timer);
  }, [refreshMs]);
  return tick;
}

function Config() {
  const [twitchAuth, setTwitchAuth] = useState(null);
  const [keyData, setKeyData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [copied, setCopied] = useState(null);
  const [status, setStatus] = useState(null);
  const [confirmingRegen, setConfirmingRegen] = useState(false);

  useCountdown(5000); // re-render periodically so "last data received Ns ago" stays fresh

  useEffect(() => {
    if (!window.Twitch?.ext) {
      setLoading(false);
      return;
    }

    let cancelled = false;

    window.Twitch.ext.onAuthorized(async (auth) => {
      if (cancelled) return;
      setTwitchAuth(auth);

      try {
        const response = await fetch(`${BACKEND_URL}/api/channels/keys`, {
          headers: { Authorization: `Bearer ${auth.token}` },
        });
        if (response.ok && !cancelled) {
          setKeyData(await response.json());
        }
      } catch {
        console.warn('[Config] Failed to fetch existing key.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    });

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!twitchAuth) return undefined;

    const poll = async () => {
      try {
        const response = await fetch(`${BACKEND_URL}/api/telemetry/${twitchAuth.channelId}`);
        if (response.ok) {
          setStatus(await response.json());
        } else {
          setStatus(null);
        }
      } catch {
        setStatus(null);
      }
    };

    poll();
    const timer = setInterval(poll, 5000);
    return () => clearInterval(timer);
  }, [twitchAuth]);

  const generateKey = async () => {
    if (!twitchAuth) return;
    setError(null);

    try {
      const response = await fetch(`${BACKEND_URL}/api/channels/keys`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${twitchAuth.token}` },
      });
      if (response.ok) {
        setKeyData(await response.json());
      } else {
        const data = await response.json();
        setError(data.error || 'Failed to generate key');
      }
    } catch {
      setError('Failed to connect to backend');
    }
  };

  const regenerateKey = async () => {
    setConfirmingRegen(false);
    await generateKey();
  };

  const copyToClipboard = (text, label) => {
    navigator.clipboard.writeText(text).then(() => {
      setCopied(label);
      setTimeout(() => setCopied(null), 2000);
    });
  };

  if (!window.Twitch?.ext) {
    return (
      <div className="config-view">
        <div className="config-card">
          <h1>Truckload Configuration</h1>
          <p className="config-muted">Waiting for Twitch Authorization...</p>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="config-view">
        <div className="config-card">
          <p className="config-muted">Loading configuration...</p>
        </div>
      </div>
    );
  }

  const ingestUrl = `${BACKEND_URL}/api/ingest`;
  const bridgeCommand = keyData
    ? `Truckload.Bridge.exe --ingest-url ${ingestUrl} --key ${keyData.ingestKey} --save`
    : null;
  const secondsAgo = status?.ts ? Math.max(0, Math.floor(Date.now() / 1000 - status.ts)) : null;
  const isLive = secondsAgo !== null && secondsAgo < 90;

  return (
    <div className="config-view">
      <div className="config-card">
        <h1>Truckload Configuration</h1>
        <p className="config-intro">
          Run the Truckload Bridge app on your streaming PC to send live job and truck
          telemetry from ATS/ETS2 to your viewers.
        </p>

        {error && <div className="config-alert">{error}</div>}

        {!keyData ? (
          <div className="config-section config-section-centered">
            <p className="config-muted">Generate an API key to get started.</p>
            <button className="config-button" onClick={generateKey}>
              Generate API Key
            </button>
          </div>
        ) : (
          <div className="config-stack">
            <div className="config-section">
              <h2>1. Download the Bridge</h2>
              <p className="config-muted">
                Download <code>Truckload.Bridge.exe</code> and run it on the PC that runs
                ATS/ETS2.
              </p>
              <a className="config-link-button" href={BRIDGE_RELEASES_URL} target="_blank" rel="noreferrer">
                Download latest release
              </a>
            </div>

            <div className="config-section">
              <h2>2. Run it with your key</h2>
              <p className="config-muted">Paste this command into a terminal in the same folder as the exe:</p>
              <div className="config-copy-row">
                <code className="config-code-block">{bridgeCommand}</code>
                <button className="config-button" onClick={() => copyToClipboard(bridgeCommand, 'cmd')}>
                  {copied === 'cmd' ? 'Copied!' : 'Copy'}
                </button>
              </div>
              <p className="config-hint">
                <code>--save</code> remembers these settings, so next time you can just run{' '}
                <code>Truckload.Bridge.exe</code> with no arguments.
              </p>
            </div>

            <div className="config-section">
              <h2>Ingest key</h2>
              <div className="config-copy-row">
                <input className="config-input" type="text" value={keyData.ingestKey} readOnly />
                <button className="config-button" onClick={() => copyToClipboard(keyData.ingestKey, 'key')}>
                  {copied === 'key' ? 'Copied!' : 'Copy'}
                </button>
              </div>
              <p className="config-warning">Keep this key secret. Anyone with it can send data to your channel's overlay.</p>
              {confirmingRegen ? (
                <div className="config-copy-row">
                  <span className="config-warning">
                    This will invalidate your current key. The bridge app on your PC will need the new key.
                  </span>
                  <button className="config-link config-danger" onClick={regenerateKey}>
                    Confirm regenerate
                  </button>
                  <button className="config-link" onClick={() => setConfirmingRegen(false)}>
                    Cancel
                  </button>
                </div>
              ) : (
                <button className="config-link config-danger" onClick={() => setConfirmingRegen(true)}>
                  Regenerate Key
                </button>
              )}
            </div>

            <div className="config-section">
              <h2>Status</h2>
              <div className="config-status-row">
                <span className={`config-status-dot ${isLive ? 'live' : 'offline'}`} />
                <span>
                  {isLive
                    ? `Receiving data (last update ${secondsAgo}s ago)`
                    : status
                      ? `No recent data (last update ${secondsAgo}s ago)`
                      : 'No data received yet'}
                </span>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default Config;
