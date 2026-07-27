// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Workflows;

/// <summary>
/// Real-infrastructure durable-execution conformance for the workflow engine over
/// <see cref="SqlServerEventStore"/>, using the shared <see cref="WorkflowConformanceTestKit"/>. Verifies
/// crash-recovery and exactly-once (resume-from-last-completed-step) against a real SQL Server journal, whose
/// optimistic concurrency (<c>expectedVersion</c> on append) backs the exactly-once guarantee.
/// </summary>
/// <remarks>
/// Non-cloud provider: this suite is NEVER skipped (<c>DockerAvailable.ShouldBeTrue</c>). Reuses the shared
/// <see cref="SqlServerEventStoreContainerFixture"/> so no additional container is spun up. The SQL Server
/// journal schema is created once per class via <see cref="InitializeAsync"/> before the sync
/// <see cref="CreateEventStore"/> constructs the store.
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class SqlServerWorkflowConformanceTests
    : WorkflowConformanceTestKit, IClassFixture<SqlServerEventStoreContainerFixture>, IAsyncLifetime
{
    private readonly SqlServerEventStoreContainerFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerWorkflowConformanceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared SQL Server EventStore container fixture.</param>
    public SqlServerWorkflowConformanceTests(SqlServerEventStoreContainerFixture fixture) => _fixture = fixture;

    /// <summary>Ensures the container is ready and the journal schema is created before any scenario runs.</summary>
    /// <returns>A task representing the initialization.</returns>
    public async ValueTask InitializeAsync() => await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc/>
    protected override IEventStore CreateEventStore()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "SQL Server durable-execution conformance runs against real infrastructure and is never skipped");

        // Consumer-default (connectionString, logger) constructor; the schema was created in InitializeAsync.
        return new SqlServerEventStore(_fixture.ConnectionString, NullLogger<SqlServerEventStore>.Instance);
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
