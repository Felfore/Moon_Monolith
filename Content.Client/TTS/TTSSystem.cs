using System.IO;
using Content.Shared._Goobstation.CCVars;
using Content.Shared.TTS;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IAudioManager _audioInt = default!;

    private ISawmill _sawmill = default!;
    private readonly MemoryContentRoot _contentRoot = new();
    private static readonly ResPath Prefix = ResPath.Root ;/// "";

    /// <summary>
    /// Reducing the volume of the TTS when whispering. Will be converted to logarithm.
    /// </summary>
    private const float WhisperFade = 4f;

    /// <summary>
    /// The volume at which the TTS sound will not be heard.
    /// </summary>
    private const float MinimalVolume = -10f;

    private float _volume = GoobCVars.TTSVolume.DefaultValue;
    private int _fileIdx = 0;

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        _res.AddRoot(Prefix, _contentRoot);
        _cfg.OnValueChanged(GoobCVars.TTSVolume, OnTtsVolumeChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(GoobCVars.TTSVolume, OnTtsVolumeChanged);
        _contentRoot.Dispose();
    }

    public void RequestPreviewTTS(string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        _sawmill.Debug($"OnPlayTTS: received {ev.Data.Length} bytes from source {ev.SourceUid}, isRadio={ev.IsRadio}, isWhisper={ev.IsWhisper}");

        // Ensure that the data is not empty
        if (ev.Data.Length == 0)
        {
            _sawmill.Error("Received empty TTS audio data");
            return;
        }

        // Load the OGG/Opus audio bytes directly
        using var stream = new MemoryStream(ev.Data);
        var audioStream = _audioInt.LoadAudioOggVorbis(stream);
        //using var audioStream = _audioInt.LoadAudioOggVorbis(new MemoryStream(ev.Data));

        // Set audio parameters for volume and distance
        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(ev.IsWhisper))
            .WithMaxDistance(AdjustDistance(ev.IsWhisper));

        // Play the audio either attached to an entity or globally
        if (ev.SourceUid != null && !ev.IsRadio)
            _audio.PlayEntity(audioStream, GetEntity(ev.SourceUid.Value), null, audioParams);
        else
            _audio.PlayGlobal(audioStream, null, audioParams);
    }

    private float AdjustVolume(bool isWhisper)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper)
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? 5 : 10;
    }
}
