using System;
using System.Collections.Generic;

namespace AccessHarvester.Cli;

/// <summary>
/// The single CLI option tokeniser shared by every subcommand (006 O-1-4).
/// </summary>
/// <remarks>
/// Before this consolidation the same <c>ParseOptions</c> body was copy-pasted
/// seven times (<c>Program.cs</c> plus six command classes) and only
/// <c>GenerateMigrationCommand</c> rejected unknown option keys — every other
/// command silently stored an unrecognised <c>--key value</c> pair that was then
/// never read, so a typo (<c>--datta</c> for <c>--data</c>) degraded the run
/// instead of surfacing as a usage error. The harvester's contract is
/// explicit-over-silent (exit 0 means "all objects processed"), so a mistyped
/// switch must be an honest usage error, not a quiet no-op.
///
/// Each subcommand passes its own accepted value-option and flag-option names;
/// the parser rejects positional arguments and any <c>--key</c> outside those two
/// sets with an <see cref="ArgumentException"/> — the existing exit-1 usage path.
/// The consolidation only adds unknown-switch rejection uniformly; it does not
/// change which options any individual command accepts.
/// </remarks>
public static class OptionParser
{
    /// <summary>
    /// Parses <paramref name="args"/> into a case-insensitive option map.
    /// </summary>
    /// <param name="args">The subcommand's argument tail (after the verb).</param>
    /// <param name="valueOptions">Option names that take a following value.</param>
    /// <param name="flagOptions">Option names that are presence-only flags.</param>
    /// <returns>
    /// A case-insensitive map; flag options present in <paramref name="args"/>
    /// map to <c>"true"</c>, value options to their supplied value.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown for a positional argument, an unknown <c>--key</c>, or a value
    /// option with no following value.
    /// </exception>
    public static Dictionary<string, string> Parse(
        string[] args,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string> flagOptions)
    {
        var values = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(flagOptions, StringComparer.OrdinalIgnoreCase);

        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected positional argument: {token}");

            string key = token[2..];
            bool isFlag = flags.Contains(key);
            bool isValue = values.Contains(key);
            if (!isFlag && !isValue)
                throw new ArgumentException($"Unknown option: --{key}");

            // Reject a repeated option rather than silently letting the last
            // occurrence win (006 P5 honest-error contract). No subcommand
            // documents a repeatable option — list-valued options like --types
            // take a single comma-separated value — so a second --key is a
            // malformed invocation, not a merge, and must be a usage error
            // instead of a wrong-but-successful run. Keyed case-insensitively,
            // so "--out a --OUT b" is also caught.
            if (opts.ContainsKey(key))
                throw new ArgumentException($"Option --{key} specified more than once.");

            if (isFlag)
            {
                opts[key] = "true";
            }
            else
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Option --{key} requires a value.");
                opts[key] = args[++i];
            }
        }
        return opts;
    }
}
