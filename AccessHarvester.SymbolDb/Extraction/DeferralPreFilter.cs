using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AccessHarvester.SymbolDb.Extraction;

// Detects modules whose names look like superseded copies kept around as
// reference (the canonical Access pattern: a working "frmFoo" alongside an
// older "frmFoo_Old" or "Old_frmFoo"). Deferred modules are written to the
// Modules table with is_deferred=1 so the AI interpretation pipeline (and
// the migration tooling) skips them; per-spec they are NOT parsed.
//
// Matching strategy: a module name matches one of the configured patterns
// AND a companion module without that suffix exists in the same name space.
// The companion-existence check prevents flagging a module that legitimately
// has "Old" in its business name with no live counterpart.
public sealed class DeferralPreFilter
{
    public static readonly IReadOnlyList<DeferralPattern> DefaultPatterns = new[]
    {
        new DeferralPattern("trailing-_Old", new Regex(@"^(?<base>.+)_Old$", RegexOptions.IgnoreCase)),
        new DeferralPattern("trailing-Old", new Regex(@"^(?<base>.+?)Old$", RegexOptions.IgnoreCase)),
        new DeferralPattern("leading-Old_", new Regex(@"^Old_(?<base>.+)$", RegexOptions.IgnoreCase)),
        new DeferralPattern("dash-Old-suffix", new Regex(@"^(?<base>.+)-Old-?$", RegexOptions.IgnoreCase)),
        new DeferralPattern("dash-Old-prefix", new Regex(@"^Old-(?<base>.+)$", RegexOptions.IgnoreCase)),
    };

    private readonly IReadOnlyList<DeferralPattern> _patterns;

    public DeferralPreFilter(IReadOnlyList<DeferralPattern>? patterns = null)
    {
        _patterns = patterns ?? DefaultPatterns;
    }

    public DeferralOutcome Classify(string moduleName, ISet<string> allModuleNames)
    {
        foreach (var pattern in _patterns)
        {
            var match = pattern.Regex.Match(moduleName);
            if (!match.Success) continue;
            string baseName = match.Groups["base"].Value;
            if (allModuleNames.Contains(baseName))
            {
                return new DeferralOutcome(true, $"{pattern.Name}; companion={baseName}", baseName);
            }
        }
        return new DeferralOutcome(false, null, null);
    }
}

public sealed record DeferralPattern(string Name, Regex Regex);

public sealed record DeferralOutcome(bool IsDeferred, string? Reason, string? CompanionName);
