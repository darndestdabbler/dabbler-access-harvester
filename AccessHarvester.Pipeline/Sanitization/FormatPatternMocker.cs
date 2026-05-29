using System.Text;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Renders a mock value from a scanner-emitted <c>valueFormatPattern</c>.
/// The schema's pattern alphabet is documented on
/// <c>classification-schema.json</c>: <c>#</c> = a digit, <c>?</c> = any
/// single character, uppercase ASCII letters are literals, and the
/// separators <c>space . , ( ) + : / -</c> are literals.
///
/// <para>
/// Deterministic: digits are picked from <paramref name="seed"/> via a
/// linear-congruential generator, and any-character placeholders are
/// rendered as <c>X</c>. The downstream consumer's only contract on a
/// mocked column is "same shape as the original"; producing identical
/// X-glyphs for the any-character slot keeps the output trivially
/// recognisable as synthetic.
/// </para>
/// </summary>
public static class FormatPatternMocker
{
    public static string Render(string pattern, int seed)
    {
        if (pattern is null) return string.Empty;
        uint rng = unchecked((uint)seed);
        var sb = new StringBuilder(pattern.Length);
        foreach (char c in pattern)
        {
            if (c == '#')
            {
                rng = NextLcg(rng);
                sb.Append((char)('0' + (rng % 10u)));
            }
            else if (c == '?')
            {
                sb.Append('X');
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static uint NextLcg(uint state) =>
        unchecked(state * 1103515245u + 12345u);
}
