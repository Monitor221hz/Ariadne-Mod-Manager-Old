namespace Ariadne.WebProtocol.Nexus;

public sealed class ClientWebSocketConnectionFactory : IWebSocketConnectionFactory
{
    public IWebSocketConnection Create()
    {
        return new ClientWebSocketConnection();
    }
}
