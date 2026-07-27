// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Workflows;

/// <summary>
/// Real-infrastructure durable-execution conformance for the workflow engine over
/// <see cref="PostgresEventStore"/>, using the shared <see cref="WorkflowConformanceTestKit"/>. Verifies
/// crash-recovery and exactly-once (resume-from-last-completed-step) against a real Postgres journal, whose
/// optimistic concurrency (<c>expectedVersion</c> on append) backs the exactly-once guarantee.
/// </summary>
/// <remarks>
/// Non-cloud provider: this suite is NEVER skipped (<c>DockerAvailable.ShouldBeTrue</c>). Reuses the shared
/// <see cref="PostgresEventStoreContainerFixture"/> so no additional container is spun up. The journal schema
/// is created once per class via <see cref="InitializeAsync"/> before the sync <see cref="CreateEventStore"/>
/// constructs the store.
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class PostgresWorkflowConformanceTests
    : WorkflowConformanceTestKit, IClassFixture<PostgresEventStoreContainerFixture>, IAsyncLifetime
{
    private readonly PostgresEventStoreContainerFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresWorkflowConformanceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared Postgres EventStore container fixture.</param>
    public PostgresWorkflowConformanceTests(PostgresEventStoreContainerFixture fixture) => _fixture = fixture;

    /// <summary>Ensures the container is ready and the journal schema is created before any scenario runs.</summary>
    /// <returns>A task representing the initialization.</returns>
    public async ValueTask InitializeAsync() => await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc/>
    protected override IEventStore CreateEventStore()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "Postgres durable-execution conformance runs against real infrastructure and is never skipped");

        return new PostgresEventStore(_fixture.ConnectionString, NullLogger<PostgresEventStore>.Instance);
    }

    /// <inheritdoc/>
    protected override async Task CleanupAsync() => await _fixture.CleanupTableAsync().ConfigureAwait(false);

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
