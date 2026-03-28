using Robust.Shared.Configuration;

namespace Content.Shared._Goobstation.CCVars;

[CVarDefs]
public sealed partial class GoobCVars
{
    #region Mechs

    /// <summary>
    ///     Whether or not players can use mech guns outside of mechs.
    /// </summary>
    public static readonly CVarDef<bool> MechGunOutsideMech =
        CVarDef.Create("mech.gun_outside_mech", true, CVar.SERVER | CVar.REPLICATED);

    #endregion

    #region TTS

    public static readonly CVarDef<float> TTSVolume =
        CVarDef.Create("tts.volume", 0.5f * 4, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> TTSUnknownVolume =
        CVarDef.Create("tts.unknown_volume", 0.2f * 4, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> TTSEnabled =
        CVarDef.Create("tts.enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Number of TTS generations that can be done simultaneously
    /// </summary>
    public static readonly CVarDef<int> TTSSimultaneousGenerations =
        CVarDef.Create("tts.simultaneous_generations", 1, CVar.SERVERONLY);

    /// <summary>
    /// Number of TTS generations that can be queued, anything new TTS generations will be ignored.
    /// </summary>
    public static readonly CVarDef<int> TTSQueueMax =
        CVarDef.Create("tts.queue_max", 20, CVar.SERVERONLY);

    /// Can be "file" to store in the cache_path, or "memory" to store it in memory.
    /// Memory is way faster, but servers are usually more limited by memory than storage, pick your poison.
    public static readonly CVarDef<string> TTSCacheType =
        CVarDef.Create("tts.cache_type", "memory", CVar.SERVERONLY);

    public static readonly CVarDef<string> TTSCachePath =
        CVarDef.Create("tts.cache_path", "data/tts/cache", CVar.SERVERONLY);

    public static readonly CVarDef<int> TTSMaxCached =
        CVarDef.Create("tts.max_cached", 2048, CVar.SERVERONLY);

    /// Cleans up the cache between rounds if false
    public static readonly CVarDef<bool> TTSCacheRoundPersistence =
        CVarDef.Create("tts.cache_round_persistence", true, CVar.SERVERONLY);

    public static readonly CVarDef<string> TTSModelPath =
        CVarDef.Create("tts.model_path", "data/tts/models", CVar.SERVERONLY);

    public static readonly CVarDef<float> TTSTemperature =
        CVarDef.Create("tts.temperature", 0.8f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TTSSpeedFactor =
        CVarDef.Create("tts.speed_factor", 1.0f, CVar.SERVERONLY);

/*  // Not supported by the Chatterbox Turbo model

    public static readonly CVarDef<float> TTSExaggeration =
        CVarDef.Create("tts.exaggeration", 1.0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TTSCfgWeight =
        CVarDef.Create("tts.cfg_weight", 2.0f, CVar.SERVERONLY);
*/

    public static readonly CVarDef<int> TTSSeed =
        CVarDef.Create("tts.seed", 0, CVar.SERVERONLY);

    public static readonly CVarDef<string> TTSLanguage =
        CVarDef.Create("tts.language", "en", CVar.SERVERONLY);

    public static readonly CVarDef<int> TTSChunkSize =
        CVarDef.Create("tts.chunk_size", 120, CVar.SERVERONLY);

    public static readonly CVarDef<bool> TTSSplitText =
        CVarDef.Create("tts.split_text", true, CVar.SERVERONLY);

    #endregion
}
