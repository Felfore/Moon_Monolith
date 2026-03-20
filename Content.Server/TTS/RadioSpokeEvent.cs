using Robust.Shared.Player;

namespace Content.Server.TTS;

public sealed class RadioSpokeEvent : EntityEventArgs
{
    public EntityUid Source;
    public string Message;
    public List<ICommonSession> Receivers;

    public RadioSpokeEvent(EntityUid source, string message, List<ICommonSession> receivers)
    {
        Source = source;
        Message = message;
        Receivers = receivers;
    }
}
