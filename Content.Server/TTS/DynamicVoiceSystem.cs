using Content.Shared.TTS;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.TTS;

public sealed class DynamicVoiceSystem : EntitySystem
{
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("tts.voices");
        SubscribeNetworkEvent<RequestVoiceListEvent>(OnVoiceListRequested);
    }

    private async void OnVoiceListRequested(RequestVoiceListEvent ev, EntitySessionEventArgs args)
    {
        _sawmill.Debug($"OnVoiceListRequested: request from {args.SenderSession.Name}");

        var voices = await _ttsManager.GetAvailableVoices();
        _sawmill.Debug($"OnVoiceListRequested: returning {voices.Count} voices to {args.SenderSession.Name}");

        var response = new VoiceListResponseEvent { Voices = voices };
        RaiseNetworkEvent(response, args.SenderSession);
    }
}
