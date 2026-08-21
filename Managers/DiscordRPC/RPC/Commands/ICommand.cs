using Nova.Managers.DiscordRPC.RPC.Payload;

namespace Nova.Managers.DiscordRPC.RPC.Commands
{
    internal interface ICommand
    {
        IPayload PreparePayload(long nonce);
    }
}
