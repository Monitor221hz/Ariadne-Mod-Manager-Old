namespace Ariadne.VFS;

/// Derived wildcard matching by Martin Richter under CPOL (http://www.codeproject.com/Articles/188256/A-Simple-Wildcard-Matching-Function)
public partial class VirtualNode<TNodeData>
{
    public static class Wildcard
    {
        public static bool Match(string name, string pattern) =>
            Match(name.AsSpan(), pattern.AsSpan());

        public static string? PartialMatch(string name, string pattern)
        {
            return PartialMatch(name.AsSpan(), pattern.AsSpan(), out var remainder)
                ? remainder.ToString()
                : null;
        }

        public static bool Match(ReadOnlySpan<char> name, ReadOnlySpan<char> pattern) =>
            PartialMatch(name, pattern, out var remainder) && remainder.IsEmpty;

        public static bool PartialMatch(
            ReadOnlySpan<char> name,
            ReadOnlySpan<char> pattern,
            out ReadOnlySpan<char> remainder
        )
        {
            if (name.Length > 0 && name[0] == '.')
            {
                // cmd.exe ignores a leading dot
                return PartialMatch(name[1..], pattern, out remainder);
            }

            if (pattern.Length > 2 && pattern.EndsWith(".*", StringComparison.Ordinal))
            {
                pattern = pattern[..^2];
            }

            bool matched = InnerMatch(name, 0, pattern, 0, out int index);
            remainder = matched ? pattern[index..] : default;
            return matched;
        }

        // on success, remainder is the absolute index of the unconsumed pattern part
        private static bool InnerMatch(
            ReadOnlySpan<char> str,
            int s,
            ReadOnlySpan<char> match,
            int m,
            out int remainder
        )
        {
            while (s < str.Length)
            {
                if (m >= match.Length)
                {
                    remainder = 0;
                    return false;
                }

                char sc = str[s];
                char mc = match[m];

                if (mc is '?' or '>')
                {
                    if (sc is '\\' or '/')
                    {
                        remainder = 0;
                        return false;
                    }
                    s++;
                    m++;
                }
                else if (mc is '*' or '<')
                {
                    if (sc is '\\' or '/')
                    {
                        m++;
                        continue;
                    }

                    if (InnerMatch(str, s, match, m + 1, out remainder))
                    {
                        return true;
                    }

                    if (InnerMatch(str, s + 1, match, m, out remainder))
                    {
                        return true;
                    }
                    return false;
                }
                else
                {
                    if (char.ToUpperInvariant(sc) != char.ToUpperInvariant(mc))
                    {
                        remainder = 0;
                        return false;
                    }
                    s++;
                    m++;
                }
            }

            while (m < match.Length && match[m] is '*' or '<')
            {
                m++;
            }
            remainder = m;
            return true;
        }
    }
}
