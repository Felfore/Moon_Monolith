using System.Linq;
using Content.Client.TTS;
using Content.Shared.TTS;
using Robust.Shared.Random;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private IRobustRandom _random = default!;
    private TTSSystem _ttsSys = default!;
    private DynamicVoiceSystem _dynamicVoiceSys = default!;

    // Local copy of the voice list, populated from DynamicVoiceSystem
    private List<DynamicVoiceData> _voiceList = new();

    private readonly List<string> _sampleText = new()
    {
        "Test.",
        "I'm testing the voice.",
    };

    private void InitializeVoice()
    {
        _random = IoCManager.Resolve<IRobustRandom>();
        _ttsSys = _entManager.System<TTSSystem>();
        _dynamicVoiceSys = _entManager.System<DynamicVoiceSystem>();

        // Subscribe to voice list updates so the dropdown refreshes automatically
        // when new voices are added to the Chatterbox server without a restart.
        _dynamicVoiceSys.VoiceListUpdated += OnVoiceListUpdated;

        // Request the current voice list from the server immediately.
        // The response will arrive async and trigger OnVoiceListUpdated.
        _dynamicVoiceSys.RequestVoiceList();

        VoiceButton.OnItemSelected += args =>
        {
            // Reject disabled header items (negative IDs used as separators)
            if (args.Id < 0)
            {
                var currentIndex = _voiceList.FindIndex(x => x.Id == Profile?.Voice);
                if (currentIndex >= 0)
                    VoiceButton.SelectId(currentIndex);
                return;
            }

            VoiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].Id);
        };

        VoicePlayButton.OnPressed += _ => { PlayTTS(); };
    }

    private void OnVoiceListUpdated()
    {
        // Cache the updated list locally, sorted alphabetically by display name.
        _voiceList = _dynamicVoiceSys.AvailableVoices
            .OrderBy(v => v.Name)
            .ToList();

        // Repopulate the dropdown with the new list.
        UpdateTTSVoicesControls();
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null)
            return;

        VoiceButton.Clear();

        if (_voiceList.Count == 0)
        {
            // Show a placeholder while waiting for the server response.
            VoiceButton.AddItem(Loc.GetString("humanoid-profile-editor-voice-loading"), 0);
            VoiceButton.SetItemDisabled(0, true);
            return;
        }

        for (var i = 0; i < _voiceList.Count; i++)
        {
            VoiceButton.AddItem(_voiceList[i].Name, i);
        }

        // Try to re-select the profile's current voice by ID.
        var currentIndex = _voiceList.FindIndex(x => x.Id == Profile.Voice);
        if (currentIndex >= 0)
        {
            VoiceButton.SelectId(currentIndex);
        }
        else if (_voiceList.Count > 0)
        {
            // Profile's saved voice no longer exists on the server;
            // fall back to the first available voice.
            VoiceButton.SelectId(0);
            SetVoice(_voiceList[0].Id);
        }
    }

    private void PlayTTS()
    {
        if (Profile is null)
            return;

        _ttsSys.RequestPreviewTTS(Profile.Voice);
    }
}
