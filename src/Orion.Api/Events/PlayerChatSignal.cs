namespace Orion.Api.Events;

public sealed class PlayerChatSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerChat;
    public string RawMessage { get; }
    public string Message { get; set; }
    public bool Cancelled { get; private set; }

    public PlayerChatSignal(IPlayer player, string rawMessage, string message) : base(player)
    {
        RawMessage = rawMessage;
        Message = message;
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
