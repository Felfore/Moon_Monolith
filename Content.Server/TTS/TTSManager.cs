using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared._Goobstation.CCVars;
using Prometheus;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using System.Threading;

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Content.Server.TTS;

// ReSharper disable once InconsistentNaming
public sealed class TTSManager
{
    private static readonly Histogram RequestTimings = Metrics.CreateHistogram(
        "tts_req_timings",
        "Timings of TTS API requests",
        new HistogramConfiguration()
        {
            LabelNames = new[] { "type" },
            Buckets = Histogram.ExponentialBuckets(.1, 1.5, 10),
        });

    private static readonly Counter WantedCount = Metrics.CreateCounter(
        "tts_wanted_count",
        "Amount of wanted TTS audio.");

    private static readonly Counter ReusedCount = Metrics.CreateCounter(
        "tts_reused_count",
        "Amount of reused TTS audio from cache.");

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    private ISawmill _sawmill = default!;

    private readonly Dictionary<string, byte[]> _memoryCache = new();
    private ResPath _cachePath = new();
    private ResPath _modelPath = new();

    private SemaphoreSlim _generationSemaphore = new SemaphoreSlim(1);
    private int _queuedGenerations = 0;
    private int _maxQueuedGenerations = 20;

    public TTSManager()
    {
        Initialize();
    }

    private void Initialize()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = Logger.GetSawmill("tts");

        _cachePath = MakeDataPath(_cfg.GetCVar(GoobCVars.TTSCachePath));
        _cfg.OnValueChanged(GoobCVars.TTSCachePath, OnCachePathChanged);
        _modelPath = MakeDataPath(_cfg.GetCVar(GoobCVars.TTSModelPath));
        _cfg.OnValueChanged(GoobCVars.TTSModelPath, OnModelPathChanged);
        _generationSemaphore = new SemaphoreSlim(_cfg.GetCVar(GoobCVars.TTSSimultaneousGenerations));
        _cfg.OnValueChanged(GoobCVars.TTSSimultaneousGenerations, OnRateLimitChanged);
        _maxQueuedGenerations = _cfg.GetCVar(GoobCVars.TTSQueueMax);
        _cfg.OnValueChanged(GoobCVars.TTSQueueMax, OnQueueMaxChanged);

        // Make the needed directories if they don't exist
        new Process
        {
            StartInfo = new ProcessStartInfo
            {
                #if WINDOWS
                FileName = "cmd.exe",
                Arguments = $"/C \"mkdir {_cachePath} {_modelPath}\"",
                #else
                FileName = "/bin/sh",
                Arguments = $"-c \"mkdir -p {_cachePath} {_modelPath}\"",
                #endif
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            },
        }.Start();
    }

    private void OnCachePathChanged(string path)
        => _cachePath = MakeDataPath(path);
    private void OnModelPathChanged(string path)
        => _modelPath = MakeDataPath(path);
    private void OnRateLimitChanged(int limit)
    {
        int currentCount = _generationSemaphore.CurrentCount;

        if (limit > currentCount)
            _generationSemaphore.Release(limit - currentCount);
        else if (limit < currentCount)
        {
            for (int i = 0; i < (currentCount - limit); i++)
                _generationSemaphore.Wait();
        }
    }
    private void OnQueueMaxChanged(int maxQueue)
        => _maxQueuedGenerations = maxQueue;

    private ResPath MakeDataPath(string path)
    {
        if (path.StartsWith("data/"))
            // return new(_resource.UserData.RootDir + path.Remove(0, 5));
            return new ResPath("/" + path.Substring(5)); // "data/tts/cache" → "/tts/cache"
        else
            return new(path); // Hope it's valid
    }


    /// <summary>
    /// Generates audio with passed text by API
    /// </summary>
    /// <param name="model">File name for the model</param>
    /// <param name="speaker">Identifier of speaker</param>
    /// <param name="text">SSML formatted text</param>
    /// <returns>OGG audio bytes or null if failed</returns>
    public async Task<byte[]?> ConvertTextToSpeech(string model, string speaker, string text)
    {
        WantedCount.Inc();

        var key = GetStableKey($"{model}/{speaker}/{text}");
        var cachedData = await TryGetCached(key);
        if (cachedData != null)
        {
            _sawmill.Debug($"ConvertTextToSpeech: cache hit for '{text}'");
            ReusedCount.Inc();
            return cachedData;
        }

        _sawmill.Debug($"ConvertTextToSpeech: cache miss for '{text}', calling API");

        // TODO:
        // Instead of just incrementing a integer, we should really keep track of what text + voice is in queue to be generated
        // This would stop the issue of Urist McHands saying "godo" 30 times before the first "godo" can even be generated and added to the cache
        // Which would cause it to try to generate the same message 30 times, and would instead just waiting for the first one to generate and then
        // just reuse the cached version of it.

        if (Interlocked.Increment(ref _queuedGenerations) > _maxQueuedGenerations)
        {
            Interlocked.Decrement(ref _queuedGenerations);
            _sawmill.Warning($"Queue limit exceeded for TTS generation: {text}");
            return null;
        }

        try
        {
            _sawmill.Debug($"ConvertTextToSpeech: waiting for semaphore (queued={_queuedGenerations})");
            await _generationSemaphore.WaitAsync();
            _sawmill.Debug("ConvertTextToSpeech: semaphore acquired");
            var reqTime = DateTime.UtcNow;

            try
            {
                using var client = new HttpClient();

                var requestUrl = $"http://localhost:8004/tts";

                // JSON payload for Chatterbox TTS (Custom Endpoint)
                var payload = new
                {
                    text = text,
                    voice_mode = "predefined",
                    predefined_voice_id = speaker + ".wav",
                    output_format = "opus",
                    temperature = _cfg.GetCVar(GoobCVars.TTSTemperature),
                    // exaggeration = _cfg.GetCVar(GoobCVars.TTSExaggeration), // Not supported by Turbo
                    // cfg_weight = _cfg.GetCVar(GoobCVars.TTSCfgWeight), // Not supported by Turbo
                    seed = _cfg.GetCVar(GoobCVars.TTSSeed) == 0 ? (int?)null : _cfg.GetCVar(GoobCVars.TTSSeed),
                    speed_factor = _cfg.GetCVar(GoobCVars.TTSSpeedFactor),
                    language = _cfg.GetCVar(GoobCVars.TTSLanguage),
                    split_text = _cfg.GetCVar(GoobCVars.TTSSplitText),
                    chunk_size = _cfg.GetCVar(GoobCVars.TTSChunkSize)
                };

                _sawmill.Debug($"ConvertTextToSpeech: API request sent to {requestUrl}, status={payload}");
                var response = await client.PostAsJsonAsync(requestUrl, payload);
                _sawmill.Debug($"ConvertTextToSpeech: API response received, status={response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    _sawmill.Error($"Chatterbox TTS request failed for '{text}' by '{speaker}'. Status: {response.StatusCode}");
                    RequestTimings.WithLabels("Error").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
                    return null;
                }

                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                _sawmill.Debug($"ConvertTextToSpeech: received {audioBytes.Length} bytes from API");

                // Convert TTS to .ogg file for compatability
                _sawmill.Debug("ConvertTextToSpeech: converting audio to OGG");
                audioBytes = await AudioConverter.ConvertToOggAsync(audioBytes);
                _sawmill.Debug($"ConvertTextToSpeech: conversion complete, final size={audioBytes.Length} bytes");

                TryCache(key, audioBytes);
                RequestTimings.WithLabels("API").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
                return audioBytes;
            }
            catch (Exception e)
            {
                RequestTimings.WithLabels("Error").Observe((DateTime.UtcNow - reqTime).TotalSeconds);
                _sawmill.Error($"Failed to generate new sound for '{text}' speech by '{speaker}' speaker\n{e}");
                return null;
            }
            finally
            {
                _generationSemaphore.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _queuedGenerations);
        }
    }

    public async Task<byte[]?> ConvertTextToSpeechRadio(string model, string speaker, string text)
    {
        _sawmill.Debug($"ConvertTextToSpeechRadio: started for '{text}'");

        var radioKey = GetStableKey($"radio/{model}/{speaker}/{text}");
        var cached = await TryGetCached(radioKey);
        if (cached != null)
        {
            _sawmill.Debug($"ConvertTextToSpeechRadio: cache hit for '{text}'");
            return cached;
        }

        _sawmill.Debug($"ConvertTextToSpeechRadio: no cache, calling ConvertTextToSpeech for '{text}'");
        var normalAudio = await ConvertTextToSpeech(model, speaker, text);
        if (normalAudio is null)
        {
            _sawmill.Debug($"ConvertTextToSpeechRadio: ConvertTextToSpeech returned null for '{text}'");
            return null;
        }

        _sawmill.Debug($"ConvertTextToSpeechRadio: got {normalAudio.Length} bytes, applying radio effect");
        var radioAudio = await AudioConverter.ApplyRadioEffect(normalAudio);
        _sawmill.Debug($"ConvertTextToSpeechRadio: radio effect produced {radioAudio.Length} bytes, caching");

        TryCache(radioKey, radioAudio);
        return radioAudio;
    }

    private bool TryCache(string key, byte[] file)
    {
        if (_cfg.GetCVar(GoobCVars.TTSCacheType) != "memory")
        {
            var files = Directory.GetFiles(_cachePath.ToString()).ToList()
                .OrderBy(f => File.GetLastWriteTimeUtc(f).Ticks);
            var count = files.Count();
            var toDelete = count - _cfg.GetCVar(GoobCVars.TTSMaxCached);

            for (var i = toDelete; i > 0; i--)
            {
                File.Delete(files.ElementAt(i));
            }

            var filePath = Path.Combine(_cachePath.ToString(), key + ".ogg");
            File.WriteAllBytes(filePath, file);

            return true;
        }

        // Handle memory caching
        while (_memoryCache.Count > _cfg.GetCVar(GoobCVars.TTSMaxCached))
        {
            _memoryCache.Remove(_memoryCache.First().Key);
        }

        // Cache to memory
        return _memoryCache.TryAdd(key, file);
    }


    /// Tries to find an existing audio file so we don't have to make another
    private async Task<byte[]?> TryGetCached(string key)
    {
        var type = _cfg.GetCVar(GoobCVars.TTSCacheType);
        switch (type)
        {
            case "file":
                var path = Path.Combine(_cachePath.ToString(), key + ".ogg");
                return !File.Exists(path) ? null : await File.ReadAllBytesAsync(path);
            case "memory":
                return _memoryCache.GetValueOrDefault(key);
            default:
                DebugTools.Assert(false, "TTSCacheType is invalid, must be one of \"file\", \"memory\"");
                return null;
        }
    }

    /// Deletes every file with the .raw extension in the _cachePath and clears the memory cache
    public void ClearCache()
    {
        new Process
        {
            StartInfo = new ProcessStartInfo
            {
                #if WINDOWS
                FileName = "cmd.exe",
                Arguments = $"/C \"del /q {_cachePath}\\*.ogg\"",
                #else
                FileName = "/bin/sh",
                Arguments = $"-c \"rm {_cachePath}/*.ogg\"",
                #endif
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            },
        }.Start();
        _memoryCache.Clear();
    }

    private static string GetStableKey(string input) // hash key for storing cached files, which stays the same even after restart
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
