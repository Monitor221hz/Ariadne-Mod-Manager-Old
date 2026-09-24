namespace Daedalus.WebProtocol.Nexus;

public static class NexusCommandLine
{
    public const string LinkOption = "--nxm";

    public static NexusLaunchIntent Parse(IReadOnlyList<string> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            string? value = null;
            if (string.Equals(argument, LinkOption, StringComparison.Ordinal))
            {
                if (i + 1 < arguments.Count)
                {
                    value = arguments[i + 1];
                }
            }
            else if (argument.StartsWith($"{LinkOption}=", StringComparison.Ordinal))
            {
                value = argument[(LinkOption.Length + 1)..];
            }

            if (value is not null && NxmLink.TryParse(value, out var link))
            {
                return new NexusLaunchIntent { Link = link };
            }
        }

        return new NexusLaunchIntent { Link = null };
    }
}
