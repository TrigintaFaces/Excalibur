// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis.MSBuild;

using RepositoryChecks;

if (await ArtifactCommands.TryRunAsync(args).ConfigureAwait(false) is { } artifactExitCode)
{
    return artifactExitCode;
}

var cosmosAdmission = args.Length == 4 && string.Equals(args[1], "--cosmos-admission", StringComparison.Ordinal);
if (args.Length != 3 && !cosmosAdmission)
{
    await Console.Error.WriteLineAsync("Usage: RepositoryChecks <repository-root> <project.csproj|--conformance|--persistence-health|--backend-coverage|--test-timing|--tenant-mutations|--release-policies|--documentation> <report.json>").ConfigureAwait(false);
    return 2;
}

var root = Path.GetFullPath(args[0]);
var health = string.Equals(args[1], "--persistence-health", StringComparison.Ordinal);
var backends = string.Equals(args[1], "--backend-coverage", StringComparison.Ordinal);
var timing = string.Equals(args[1], "--test-timing", StringComparison.Ordinal);
var tenant = string.Equals(args[1], "--tenant-mutations", StringComparison.Ordinal);
var all = string.Equals(args[1], "--release-policies", StringComparison.Ordinal);
var documentation = string.Equals(args[1], "--documentation", StringComparison.Ordinal);
var batch = documentation || all || health || backends || timing || tenant || string.Equals(args[1], "--conformance", StringComparison.Ordinal);
var projectPath = batch || cosmosAdmission ? string.Empty : Path.GetFullPath(args[1]);
var reportPath = Path.GetFullPath(args[cosmosAdmission ? 3 : 2]);
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(batch || cosmosAdmission ? 60 : 5));
try
{
    var expectedSdk = await SdkRegistration.RegisterAsync(root, timeout.Token).ConfigureAwait(false);

    using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Configuration"] = "Release",
        ["BuildExamplesAndTests"] = "true",

        // OpenProject has no solution configuration mapping. Preserve the selected configuration
        // across references instead of design-time evaluation falling back to each child's Debug.
        ["ShouldUnsetParentConfigurationAndPlatform"] = "false",
        ["SolutionDir"] = root + Path.DirectorySeparatorChar,
        ["SolutionName"] = "Excalibur",
    });

    // Never replace referenced source projects with potentially stale binaries.
    workspace.LoadMetadataForReferencedProjects = false;
    if (cosmosAdmission)
    {
        return await CosmosAdmissionCensus.WriteAsync(workspace, args[2], reportPath, timeout.Token).ConfigureAwait(false);
    }

    if (all)
    {
        return await RepositoryPolicyCensus.WriteAsync(workspace, root, reportPath, timeout.Token).ConfigureAwait(false);
    }

    if (documentation)
    {
        return await DocumentationCensus.PrepareAndWriteAsync(root, reportPath, timeout.Token).ConfigureAwait(false);
    }

    if (tenant)
    {
        return await TenantMutationCensus.WriteAsync(workspace, root, reportPath, timeout.Token).ConfigureAwait(false);
    }

    if (timing)
    {
        return await TestTimingCensus.WriteAsync(workspace, root, reportPath, timeout.Token).ConfigureAwait(false);
    }

    if (backends)
    {
        return await BackendCoverageCensus.WriteAsync(workspace, root, reportPath, timeout.Token).ConfigureAwait(false);
    }

    if (batch)
    {
        return health
            ? await PersistenceHealthCensus.WriteAsync(workspace, root, reportPath, timeout.Token).ConfigureAwait(false)
            : await ConformanceCensus.WriteAsync(workspace, root, reportPath, timeout.Token).ConfigureAwait(false);
    }

    var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: timeout.Token).ConfigureAwait(false);
    var compilation = await project.GetCompilationAsync(timeout.Token).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"No compilation for {projectPath}.");
    await CompilationValidation.ValidateProjectClosureAsync(project, timeout.Token).ConfigureAwait(false);
    await CompilationValidation.ValidateWorkspaceAsync(workspace).ConfigureAwait(false);

    var publishedAssembly = await PublishedContracts.FindAsync(project, root, timeout.Token).ConfigureAwait(false);
    await ReportWriter.WriteAsync(root, project, reportPath, expectedSdk, compilation, publishedAssembly, timeout.Token).ConfigureAwait(false);
    return 0;
}
catch (Exception exception) when (exception is not OutOfMemoryException)
{
    await Console.Error.WriteLineAsync($"REFUSE: {exception.Message}").ConfigureAwait(false);
    return 2;
}
