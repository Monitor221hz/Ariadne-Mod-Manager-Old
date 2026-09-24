namespace Daedalus.WebProtocol;

public static class ProtocolCommandLine
{
    public static LaunchIntent Parse(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> schemeByOption
    )
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            foreach (var (option, scheme) in schemeByOption)
            {
                string? value = null;
                if (string.Equals(argument, option, StringComparison.Ordinal))
                {
                    if (i + 1 < arguments.Count)
                    {
                        value = arguments[i + 1];
                    }
                }
                else if (argument.StartsWith($"{option}=", StringComparison.Ordinal))
                {
                    value = argument[(option.Length + 1)..];
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return new LaunchIntent { Scheme = scheme, Link = value };
                }
            }
        }

        return new LaunchIntent();
    }
}
