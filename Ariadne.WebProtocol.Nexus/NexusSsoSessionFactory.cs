namespace Ariadne.WebProtocol.Nexus;

public sealed class NexusSsoSessionFactory : INexusSsoSessionFactory
{
    private readonly string _applicationSlug;
    private readonly IWebSocketConnectionFactory _socketFactory;

    public NexusSsoSessionFactory(string applicationSlug, IWebSocketConnectionFactory socketFactory)
    {
        _applicationSlug = applicationSlug;
        _socketFactory = socketFactory;
    }

    public INexusSsoSession Create()
    {
        return new NexusSsoSession(_applicationSlug, _socketFactory);
    }
}
