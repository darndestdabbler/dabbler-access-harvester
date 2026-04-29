using System;
using System.IO;
using AccessHarvester.SymbolDb.Cli;

string repoRoot = Directory.GetCurrentDirectory();
string? outputPath = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--repo-root":
            repoRoot = RequireValue(args, ref i, "--repo-root");
            break;
        case "--output":
            outputPath = RequireValue(args, ref i, "--output");
            break;
        default:
            throw new ArgumentException($"Unknown argument: {args[i]}");
    }
}

if (string.IsNullOrWhiteSpace(outputPath))
    throw new ArgumentException("--output is required.");

repoRoot = Path.GetFullPath(repoRoot);
outputPath = Path.GetFullPath(outputPath);

int exitCode = BuildArtifacts.RunForNorthwind(
    repoRoot,
    outputPath,
    writeMeasurementReport: false);

Console.WriteLine($"Probe temp DB ready: {outputPath}");
return exitCode;

static string RequireValue(string[] args, ref int index, string option)
{
    if (index + 1 >= args.Length)
        throw new ArgumentException($"{option} requires a value.");

    index++;
    return args[index];
}
