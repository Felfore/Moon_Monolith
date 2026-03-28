using Content.Shared.TTS;
using Robust.Shared.Network;

namespace Content.Client.TTS;

public sealed class DynamicVoiceSystem : EntitySystem
{
    private ISawmill _sawmill = default!;

    /// <summary>
    /// Cached voice list from the last server response.
    /// Empty until the server responds.
    /// </summary>
    public List<DynamicVoiceData> AvailableVoices { get; private set; } = new();

    /// <summary>
    /// Fired when the voice list is received or refreshed from the server.
    /// UI should subscribe to this to update dropdowns.
    /// </summary>
    public event Action? VoiceListUpdated;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("tts.voices");
        SubscribeNetworkEvent<VoiceListResponseEvent>(OnVoiceListReceived);
    }

    /// <summary>
    /// Sends a request to the server for the current voice list.
    /// Result will arrive asynchronously via VoiceListUpdated event.
    /// </summary>
    public void RequestVoiceList()
    {
        _sawmill.Debug("RequestVoiceList: sending request to server");
        RaiseNetworkEvent(new RequestVoiceListEvent());
    }

    private void OnVoiceListReceived(VoiceListResponseEvent ev)
    {
        _sawmill.Debug($"OnVoiceListReceived: got {ev.Voices.Count} voices");
        AvailableVoices = ev.Voices;
        VoiceListUpdated?.Invoke();
    }
}
