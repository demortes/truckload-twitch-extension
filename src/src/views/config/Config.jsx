import React, { useState, useEffect } from 'react';
import '../../index.css';

const BACKEND_URL = import.meta.env.VITE_BACKEND_URL || 'http://localhost:8080';

const Config = () => {
    const [twitchAuth, setTwitchAuth] = useState(null);
    const [isAuthorized, setIsAuthorized] = useState(false);
    const [keyData, setKeyData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [copied, setCopied] = useState(null);

    useEffect(() => {
        if (window.Twitch?.ext) {
            window.Twitch.ext.onAuthorized(async (auth) => {
                setTwitchAuth(auth);
                setIsAuthorized(true);

                try {
                    const response = await fetch(`${BACKEND_URL}/api/channels/keys`, {
                        headers: { 'Authorization': `Bearer ${auth.token}` }
                    });
                    if (response.ok) {
                        setKeyData(await response.json());
                    }
                } catch (err) {
                    console.warn('[Config] Failed to fetch existing key:', err);
                }
                setLoading(false);
            });
        } else {
            setLoading(false);
        }
    }, []);

    const generateKey = async () => {
        if (!twitchAuth) return;
        setError(null);

        try {
            const response = await fetch(`${BACKEND_URL}/api/channels/keys`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${twitchAuth.token}` }
            });
            if (response.ok) {
                setKeyData(await response.json());
            } else {
                const data = await response.json();
                setError(data.error || 'Failed to generate key');
            }
        } catch (err) {
            setError('Failed to connect to backend');
        }
    };

    const regenerateKey = async () => {
        if (confirm('This will invalidate your current key. Any existing Trucky configuration will need to be updated. Continue?')) {
            await generateKey();
        }
    };

    const copyToClipboard = (text, label) => {
        navigator.clipboard.writeText(text).then(() => {
            setCopied(label);
            setTimeout(() => setCopied(null), 2000);
        });
    };

    if (!isAuthorized) {
        return (
            <div className="flex items-center justify-center h-screen bg-gray-900 text-white">
                <div className="text-xl">Waiting for Twitch Authorization...</div>
            </div>
        );
    }

    if (loading) {
        return (
            <div className="flex items-center justify-center h-screen bg-gray-900 text-white">
                <div className="text-xl">Loading configuration...</div>
            </div>
        );
    }

    const ingestUrl = `${BACKEND_URL}/api/ingest`;

    return (
        <div className="min-h-screen bg-gray-900 text-white p-6 font-sans">
            <div className="max-w-2xl mx-auto bg-gray-800 rounded-lg p-8 shadow-lg">
                <h1 className="text-3xl font-bold mb-6 text-orange-500">Truckload Configuration</h1>

                <p className="mb-6 text-gray-300">
                    Configure your Trucky application to send telemetry data to viewers.
                </p>

                {error && (
                    <div className="mb-4 bg-red-900 border border-red-700 text-red-200 px-4 py-3 rounded">
                        {error}
                    </div>
                )}

                {!keyData ? (
                    <div className="bg-gray-700 p-6 rounded-md text-center">
                        <p className="text-gray-300 mb-4">
                            Generate an API key to start sending telemetry data from Trucky.
                        </p>
                        <button
                            onClick={generateKey}
                            className="bg-orange-600 hover:bg-orange-700 text-white px-6 py-3 rounded font-semibold transition"
                        >
                            Generate API Key
                        </button>
                    </div>
                ) : (
                    <div className="space-y-6">
                        <div className="bg-gray-700 p-4 rounded-md">
                            <h2 className="text-lg font-semibold mb-2">1. Ingest URL</h2>
                            <p className="text-sm text-gray-400 mb-2">
                                Enter this URL in your Trucky Custom Telemetry settings.
                            </p>
                            <div className="flex gap-2">
                                <input
                                    type="text"
                                    value={ingestUrl}
                                    readOnly
                                    className="flex-1 bg-gray-900 border border-gray-600 rounded px-3 py-2 text-gray-200"
                                />
                                <button
                                    onClick={() => copyToClipboard(ingestUrl, 'url')}
                                    className="bg-orange-600 hover:bg-orange-700 text-white px-4 py-2 rounded transition"
                                >
                                    {copied === 'url' ? 'Copied!' : 'Copy'}
                                </button>
                            </div>
                        </div>

                        <div className="bg-gray-700 p-4 rounded-md">
                            <h2 className="text-lg font-semibold mb-2">2. API Key</h2>
                            <p className="text-sm text-gray-400 mb-2">
                                Use this as the <code className="bg-gray-900 px-1 rounded">X-Api-Key</code> header value.
                            </p>
                            <div className="flex gap-2">
                                <input
                                    type="text"
                                    value={keyData.ingestKey}
                                    readOnly
                                    className="flex-1 bg-gray-900 border border-gray-600 rounded px-3 py-2 text-gray-200 font-mono text-sm"
                                />
                                <button
                                    onClick={() => copyToClipboard(keyData.ingestKey, 'key')}
                                    className="bg-orange-600 hover:bg-orange-700 text-white px-4 py-2 rounded transition"
                                >
                                    {copied === 'key' ? 'Copied!' : 'Copy'}
                                </button>
                            </div>
                            <p className="text-xs text-yellow-500 mt-2">
                                Keep this key secret. Anyone with this key can send data to your channel's overlay.
                            </p>
                        </div>

                        <div className="flex justify-between items-center pt-2">
                            <div className="flex items-center gap-2">
                                <div className="w-3 h-3 rounded-full bg-green-500"></div>
                                <span className="text-sm text-gray-300">Key Active</span>
                            </div>
                            <button
                                onClick={regenerateKey}
                                className="text-sm text-gray-400 hover:text-red-400 transition underline"
                            >
                                Regenerate Key
                            </button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

export default Config;
