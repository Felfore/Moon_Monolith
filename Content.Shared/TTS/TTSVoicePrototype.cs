using Content.Shared.Humanoid;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.TTS;

[Prototype("ttsVoice")]
// ReSharper disable once InconsistentNaming
public sealed partial class TTSVoicePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    [DataField(required: true)]
    public Sex Sex;

    [DataField(required: true)]
    public string Model = string.Empty;

    [DataField]
    public string Speaker = "0";

    [DataField]
    public bool CanSelect = true;
}
