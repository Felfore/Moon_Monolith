using Content.Shared.Humanoid.Markings;
using Content.Shared.MagicMirror;
using Content.Shared.TTS;
using Content.Client.TTS;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Content.Shared.Humanoid.Markings;
using Content.Shared.MagicMirror;
using Content.Shared.TTS;
using Content.Client.TTS;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.MagicMirror;

public sealed class MagicMirrorBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    [ViewVariables]
    private MagicMirrorWindow? _window;

    public MagicMirrorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MagicMirrorWindow>();

        _window.OnHairSelected += tuple => SelectHair(MagicMirrorCategory.Hair, tuple.id, tuple.slot);
        _window.OnHairColorChanged += args => ChangeColor(MagicMirrorCategory.Hair, args.marking, args.slot);
        _window.OnHairSlotAdded += delegate () { AddSlot(MagicMirrorCategory.Hair); };
        _window.OnHairSlotRemoved += args => RemoveSlot(MagicMirrorCategory.Hair, args);

        _window.OnFacialHairSelected += tuple => SelectHair(MagicMirrorCategory.FacialHair, tuple.id, tuple.slot);
        _window.OnFacialHairColorChanged +=
            args => ChangeColor(MagicMirrorCategory.FacialHair, args.marking, args.slot);
        _window.OnFacialHairSlotAdded += delegate () { AddSlot(MagicMirrorCategory.FacialHair); };
        _window.OnFacialHairSlotRemoved += args => RemoveSlot(MagicMirrorCategory.FacialHair, args);

        _window.OnVoiceSelected += voice => SendMessage(new MagicMirrorChangeVoiceMessage(voice));
        _window.OnVoicePreview += voice => _entManager.System<TTSSystem>().RequestPreviewTTS(voice);
    }

    private void SelectHair(MagicMirrorCategory category, string marking, int slot)
    {
        SendMessage(new MagicMirrorSelectMessage(category, marking, slot));
    }

    private void ChangeColor(MagicMirrorCategory category, Marking marking, int slot)
    {
        SendMessage(new MagicMirrorChangeColorMessage(category, new(marking.MarkingColors), slot));
    }

    private void RemoveSlot(MagicMirrorCategory category, int slot)
    {
        SendMessage(new MagicMirrorRemoveSlotMessage(category, slot));
    }

    private void AddSlot(MagicMirrorCategory category)
    {
        SendMessage(new MagicMirrorAddSlotMessage(category));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MagicMirrorUiState data || _window == null)
        {
            return;
        }

        var isTarget = _entManager.GetEntity(data.Target) == _playerManager.LocalSession?.AttachedEntity;
        _window.UpdateState(data, isTarget);
    }
}
