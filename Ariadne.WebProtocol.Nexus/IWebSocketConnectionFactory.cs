namespace Ariadne.WebProtocol.Nexus;

public interface IWebSocketConnectionFactory
{
    IWebSocketConnection Create();
}
