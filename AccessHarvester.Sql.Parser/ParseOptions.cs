namespace AccessHarvester.Sql.Parser;

public sealed record ParseOptions(
    ParseMode RequestedMode = ParseMode.Recovering,
    int LossyFallbackErrorThreshold = 25,
    string? SourcePath = null);
