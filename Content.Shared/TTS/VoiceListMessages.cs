using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.TTS;

/// <summary>
/// Sent by the client to request the current list of available TTS voices
/// from the Chatterbox server.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestVoiceListEvent : EntityEventArgs
{
}

/// <summary>
/// Sent by the server in response to RequestVoiceListEvent,
/// containing the current list of available voices.
/// </summary>
[Serializable, NetSerializable]
public sealed class VoiceListResponseEvent : EntityEventArgs
{
    public List<DynamicVoiceData> Voices { get; set; } = new();
}

/// <summary>
/// Data for a single dynamically loaded TTS voice.
/// </summary>
[Serializable, NetSerializable]
public sealed class DynamicVoiceData
{
    /// <summary>
    /// The filename without extension, used as the voice ID sent to Chatterbox.
    /// e.g. "alice" for "alice.wav"
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown to players, derived from the filename.
    /// e.g. "Alice" for "alice.wav"
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
