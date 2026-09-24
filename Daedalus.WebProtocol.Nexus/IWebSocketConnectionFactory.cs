namespace Daedalus.WebProtocol.Nexus;

public interface IWebSocketConnectionFactory
{
    IWebSocketConnection Create();
}
