// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.CosmosDb;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Workflows;

/// <summary>
/// Durable-execution conformance for the workflow engine over <see cref="CosmosDbEventStore"/>, using the
/// shared <see cref="WorkflowConformanceTestKit"/>. Verifies crash-recovery and exactly-once against a real
/// Cosmos journal, whose optimistic concurrency (ETag/Conflict on append) backs the exactly-once guarantee.
/// </summary>
/// <remarks>
/// Cloud provider: this suite is CLOUD-GATED — it runs when the Cosmos emulator fixture is provisioned and is
/// otherwise skipped (<c>Assert.SkipUnless</c>), mirroring the provider-parity posture. Binds the SDK DEFAULT
/// (Newtonsoft) client surface; the store self-creates its per-test container lazily on first append/load.
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
// The only Cosmos-backed suite that lacked this trait. Every other Cosmos class carries it, and CI selects
// on it to exclude the provider wholesale where no emulator exists — so without it this suite is invisible
// to that rule and would have to be tracked by name instead.
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbWorkflowConformanceTests
    : WorkflowConformanceTestKit, IClassFixture<CosmosDbEventStoreContainerFixture>
{
    private readonly CosmosDbEventStoreContainerFixture _fixture;
    private string? _containerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbWorkflowConformanceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared Cosmos DB EventStore container fixture.</param>
    public CosmosDbWorkflowConformanceTests(CosmosDbEventStoreContainerFixture fixture) => _fixture = fixture;

    /// <inheritdoc/>
    protected override IEventStore CreateEventStore()
    {
        // Cloud-gated: run when the emulator fixture is provisioned, otherwise skip.
        Assert.SkipUnless(
            _fixture.DockerAvailable,
            "Cosmos DB durable-execution conformance runs against the Cosmos emulator when a fixture is provisioned.");

        // Unique per-test container so each scenario is isolated; the store self-creates it lazily.
        _containerName = $"wf_events_{Guid.NewGuid():N}";

        var options = Options.Create(new CosmosDbEventStoreOptions
        {
            EventsContainerName = _containerName,
            CreateContainerIfNotExists = true,
        });

        return new CosmosDbEventStore(_fixture.Client, options, NullLogger<CosmosDbEventStore>.Instance);
    }

    /// <inheritdoc/>
    protected override async Task CleanupAsync()
    {
        if (_containerName is not null)
        {
            await _fixture.DeleteContainerAsync(_containerName).ConfigureAwait(false);
            _containerName = null;
        }
    }

    /// <inheritdoc/>
    [Fact]
    public override Task CrashMidStep_ResumesFromLastCompletedStep_ExactlyOnce() =>
        base.CrashMidStep_ResumesFromLastCompletedStep_ExactlyOnce();

    /// <inheritdoc/>
    [Fact]
    public override Task DuplicateDelivery_AppliesEachActivityExactlyOnce() =>
        base.DuplicateDelivery_AppliesEachActivityExactlyOnce();

    /// <inheritdoc/>
    [Fact]
    public override Task DelayedRestart_ResumesAndCompletesExactlyOnce() =>
        base.DelayedRestart_ResumesAndCompletesExactlyOnce();
}
