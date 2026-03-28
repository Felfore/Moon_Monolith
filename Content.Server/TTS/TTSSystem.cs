using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Goobstation.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.TTS;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using TTSComponent = Content.Shared.TTS.TTSComponent;

namespace Content.Server.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;

    private ISawmill _sawmill = default!;

    private readonly List<string> _sampleText = new()
    {
        "Test.",
        "I'm testing the voice.",
    };

    private const int MaxMessageChars = 100 * 2; // Same as SingleBubbleCharLimit * 2
    private bool _isEnabled = true;

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");

        _cfg.OnValueChanged(GoobCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke, before: new[] { typeof(RadioSystem) });
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);

        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpoke);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _ttsManager.ClearCache();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        if (!_cfg.GetCVar(GoobCVars.TTSCacheRoundPersistence))
            _ttsManager.ClearCache();
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled || string.IsNullOrEmpty(ev.VoiceId))
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, ev.VoiceId);
        _sawmill.Debug($"OnRequestPreviewTTS: generated preview for voice '{ev.VoiceId}', text length={previewText.Length}, success={soundData != null}");
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            string.IsNullOrEmpty(voiceId))
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (string.IsNullOrEmpty(voiceId))
            return;

        if (args.Channel != null)
            return;

        if (args.IsWhisper)
        {
            _sawmill.Debug($"OnEntitySpoke: handling whisper from {uid}, voice={voiceId}, length={args.Message.Length}");
            HandleWhisper(uid, args.Message, voiceId);
            return;
        }

        _sawmill.Debug($"OnEntitySpoke: handling say from {uid}, voice={voiceId}, length={args.Message.Length}");
        HandleSay(uid, args.Message, voiceId);
    }

    private void OnRadioSpoke(RadioSpokeEvent args)
    {
        _sawmill.Debug($"OnRadioSpoke fired, source={args.Source}, receivers={args.Receivers.Count}");
        if (!_isEnabled)
            return;

        if (!TryComp<TTSComponent>(args.Source, out var ttsComp) ||
            string.IsNullOrEmpty(ttsComp.VoicePrototypeId))
            return;

        var voiceId = ttsComp.VoicePrototypeId;
        var sessions = args.Receivers;
        var message = args.Message;
        var source = args.Source;

        ProcessRadioTTS(source, voiceId, message, sessions);
    }

    private async void ProcessRadioTTS(EntityUid source, string voiceId, string message, List<ICommonSession> sessions)
    {
        _sawmill.Debug($"ProcessRadioTTS started, message='{message}'");
        try
        {
            var radioAudio = await _ttsManager.ConvertTextToSpeechRadio(string.Empty, voiceId, message);
            if (radioAudio is null)
                return;

            var ttsEvent = new PlayTTSEvent(radioAudio, GetNetEntity(source), isRadio: true);
            _sawmill.Debug($"ProcessRadioTTS: raising PlayTTSEvent for {source}, audio size={radioAudio.Length}, recipients={sessions.Count}");
            foreach (var session in sessions)
                RaiseNetworkEvent(ttsEvent, session);
        }
        catch (Exception e)
        {
            _sawmill.Error($"ProcessRadioTTS failed: {e}");
        }
    }

    private async void HandleSay(EntityUid uid, string message, string voiceId)
    {
        var soundData = await GenerateTTS(message, voiceId);
        _sawmill.Debug($"HandleSay: generated TTS for {uid}, success={soundData != null}");
        if (soundData is null)
            return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), Filter.Pvs(uid));
    }

    private async void HandleWhisper(EntityUid uid, string message, string voiceId)
    {
        var fullSoundData = await GenerateTTS(message, voiceId, true);
        if (fullSoundData is null)
            return;

        var fullTtsEvent = new PlayTTSEvent(fullSoundData, GetNetEntity(uid), true);

        // TODO: Check obstacles
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourcePos = _xforms.GetWorldPosition(xformQuery.GetComponent(uid), xformQuery);
        var receptions = Filter.Pvs(uid).Recipients;
        foreach (var session in receptions)
        {
            if (!session.AttachedEntity.HasValue)
                continue;
            var xform = xformQuery.GetComponent(session.AttachedEntity.Value);
            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();
            if (distance > 10 * 10)
                continue;

            RaiseNetworkEvent(fullTtsEvent, session);
        }
    }

    // ReSharper disable once InconsistentNaming
    private async Task<byte[]?> GenerateTTS(string text, string voiceId, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "")
            return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        // model is no longer used — Chatterbox handles model selection internally.
        // voiceId maps directly to the speaker filename in the Chatterbox voices folder.
        return await _ttsManager.ConvertTextToSpeech(string.Empty, voiceId, textSanitized);
    }
}

public sealed class TransformSpeakerVoiceEvent(EntityUid sender, string voiceId) : EntityEventArgs
{
    public EntityUid Sender = sender;
    public string VoiceId = voiceId;
}
