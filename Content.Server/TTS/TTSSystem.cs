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
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;

    private ISawmill _sawmill = default!;

    private readonly List<string> _sampleText = new()
    {
        "Hello station, I have teleported the janitor.",
        "Yes, Ms. Sarah, about the theater issue -- will Engineering be dealing with it?",
        "Since Samuel was detained should we change it to a code green?",
        "He wants to do an interview, where are you?",
        "Samuel Rodriguez broke the door to the bridge with an e-mag!",
        "I want to give credit where it's due -- the newspaper is working, and it's doing quite well. I like it.",
        "Praise and glory from NT.",
        "Will someone build a podium in the theater?",
        "Clown, I'm about to be interviewed, I'll be gone about 10 minutes.",
        "Chief, I'm about to be interviewed, I'll be gone for about 10 minutes.",
        "As far as I understand, the anomaly broke the barrier between the Singularity and the station.",
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
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Model, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        if (args.Channel != null)
            return;

        if (args.IsWhisper)
        {
            HandleWhisper(uid, args.Message, protoVoice.Model, protoVoice.Speaker);
            return;
        }

        HandleSay(uid, args.Message, protoVoice.Model, protoVoice.Speaker);
    }

    private void OnRadioSpoke(RadioSpokeEvent args)
    {
        _sawmill.Debug($"OnRadioSpoke fired, source={args.Source}, receivers={args.Receivers.Count}");
        if (!_isEnabled)
            return;

        if (!TryComp<TTSComponent>(args.Source, out var ttsComp) ||
            ttsComp.VoicePrototypeId == null)
            return;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(ttsComp.VoicePrototypeId, out var protoVoice))
            return;

        var sessions = args.Receivers;
        var message = args.Message;
        var model = protoVoice.Model;
        var speaker = protoVoice.Speaker;
        var source = args.Source;

        ProcessRadioTTS(source, model, speaker, message, sessions);
    }

    private async void ProcessRadioTTS(EntityUid source, string model, string speaker, string message, List<ICommonSession> sessions)
    {
        _sawmill.Debug($"ProcessRadioTTS started, message='{message}'");
        try
        {
            var radioAudio = await _ttsManager.ConvertTextToSpeechRadio(model, speaker, message);
            if (radioAudio is null)
                return;

            var ttsEvent = new PlayTTSEvent(radioAudio, GetNetEntity(source), isRadio: true);
            foreach (var session in sessions)
                RaiseNetworkEvent(ttsEvent, session);
        }
        catch (Exception e)
        {
            _sawmill.Error($"ProcessRadioTTS failed: {e}");
        }
    }
    private async void HandleSay(EntityUid uid, string message, string model, string speaker)
    {
        var soundData = await GenerateTTS(message, model, speaker);
        if (soundData is null)
            return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), Filter.Pvs(uid));
    }

    private async void HandleWhisper(EntityUid uid, string message, string model, string speaker)
    {
        var fullSoundData = await GenerateTTS(message, model, speaker, true);
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
    private async Task<byte[]?> GenerateTTS(string text, string model, string speaker, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "")
            return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast;
        if (isWhisper)
            ssmlTraits = SoundTraits.PitchVerylow;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        // return await _ttsManager.ConvertTextToSpeech(speaker, textSsml); //TODO: What is this ssml?
        return await _ttsManager.ConvertTextToSpeech(model, speaker, textSanitized);
    }
}

 public sealed class TransformSpeakerVoiceEvent(EntityUid sender, string voiceId) : EntityEventArgs
{
    public EntityUid Sender = sender;
    public string VoiceId = voiceId;
}
