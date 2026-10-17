namespace Truckload.Bridge;

/// <summary>What the bridge is doing right now, as shown by a status light.</summary>
public enum BridgeStatus
{
    /// <summary>Started, nothing fetched or sent yet.</summary>
    Starting,

    /// <summary>The telemetry source (TruckTel / Funbit) is not answering: the game is closed or still loading.</summary>
    WaitingForGame,

    /// <summary>Telemetry is being read and delivered to the backend.</summary>
    Live,

    /// <summary>The backend could not be reached or returned an error; the bridge keeps retrying.</summary>
    SendFailing,

    /// <summary>The backend rejected the ingest key. The bridge stops until the key is fixed.</summary>
    InvalidKey,
}
