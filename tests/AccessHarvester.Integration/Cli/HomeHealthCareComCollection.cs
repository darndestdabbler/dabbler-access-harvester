using Xunit;

namespace AccessHarvester.Integration.Cli;

/// <summary>
/// Serializes Integration test classes that spawn a CLI process against
/// the committed <c>samples/HomeHealthCare.accdb</c> fixture. xUnit runs
/// test classes in parallel within an assembly; two classes that each
/// spawn a CLI process touching the same <c>.accdb</c> race on ACE OLE
/// DB's file-level lock and time out before contention clears (see
/// <c>docs/planning/project-guidance.md</c> Conventions → Testing
/// Conventions → "Integration tests that drive COM against a shared
/// <c>.accdb</c>"). This collection serializes them so each waits its
/// turn instead of competing for the lock.
///
/// <para>
/// Applied to the Cli command tests <see cref="ClassifyCommandTests"/>,
/// <see cref="SanitizeCommandTests"/>, <see cref="CliExtractTests"/>,
/// <see cref="CliRunTests"/>, <see cref="CliTranslateTests"/>, and to the
/// Pipeline tests that drive in-process COM against the same fixture —
/// <c>CanonicalGoldenRegressionTests</c>, <c>EndToEndPipelineTests</c>,
/// and <c>ManifestSchemaConformanceTests</c> (added in
/// <c>harvester-access-com-graceful-shutdown</c> Session 2 after
/// <see cref="ClassifyCommandTests"/> timed out at 240s while a Pipeline
/// class held the HomeHealthCare.accdb lock in parallel).
/// </para>
/// </summary>
[CollectionDefinition("HomeHealthCareCom")]
public sealed class HomeHealthCareComCollection { }
