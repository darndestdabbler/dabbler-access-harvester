using System;
using System.IO;
using AccessHarvester.SymbolDb.Cli;

string repoRoot = Directory.GetCurrentDirectory();
string? outputPath = null;
string? fixtureName = null;
string? formJson = null;
string? formModule = null;
string? moduleJson = null;
string? moduleName = null;

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
        case "--fixture":
            fixtureName = RequireValue(args, ref i, "--fixture");
            break;
        case "--form-json":
            formJson = RequireValue(args, ref i, "--form-json");
            break;
        case "--form-module":
            formModule = RequireValue(args, ref i, "--form-module");
            break;
        case "--module-json":
            moduleJson = RequireValue(args, ref i, "--module-json");
            break;
        case "--module-name":
            moduleName = RequireValue(args, ref i, "--module-name");
            break;
        default:
            throw new ArgumentException($"Unknown argument: {args[i]}");
    }
}

if (string.IsNullOrWhiteSpace(outputPath))
    throw new ArgumentException("--output is required.");

// The fixture object set is supplied at the command line so this tool — and
// the symbol-db build helper it drives — carry no fixture-specific identifiers
// in source. Point them at whatever local Access fixture is being measured.
var fixture = new FixtureObjectSet(
    FixtureName: Require(fixtureName, "--fixture"),
    FormKbJsonFile: Require(formJson, "--form-json"),
    FormModuleFqn: Require(formModule, "--form-module"),
    ModuleKbJsonFile: Require(moduleJson, "--module-json"),
    ModuleFqn: Require(moduleName, "--module-name"));

repoRoot = Path.GetFullPath(repoRoot);
outputPath = Path.GetFullPath(outputPath);

int exitCode = BuildArtifacts.RunForFixture(
    repoRoot,
    fixture,
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

static string Require(string? value, string option) =>
    string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException($"{option} is required.")
        : value;
