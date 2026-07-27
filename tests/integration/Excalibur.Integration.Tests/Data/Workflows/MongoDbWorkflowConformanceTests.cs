// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.MongoDB;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Workflows;

/// <summary>
/// Real-infrastructure durable-execution conformance for the workflow engine over
/// <see cref="MongoDbEventStore"/>, using the shared <see cref="WorkflowConformanceTestKit"/>. Verifies
/// crash-recovery and exactly-once (resume-from-last-completed-step) against a real MongoDB journal, whose
/// unique concurrency index (<c>expectedVersion</c> on append) backs the exactly-once guarantee.
/// </summary>
/// <remarks>
/// Non-cloud provider: this suite is NEVER skipped (<c>DockerAvailable.ShouldBeTrue</c>). Reuses the shared
/// <see cref="MongoDbEventStoreContainerFixture"/>. The options-only constructor builds the provider's DEFAULT
/// <c>MongoClient</c>; the store self-initializes its collections and the unique concurrency index on first use.
/// </remarks>
[Collection(MongoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class MongoDbWorkflowConformanceTests
    : WorkflowConformanceTestKit, IClassFixture<MongoDbEventStoreContainerFixture>
{
    private readonly MongoDbEventStoreContainerFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbWorkflowConformanceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared MongoDB EventStore container fixture.</param>
    public MongoDbWorkflowConformanceTests(MongoDbEventStoreContainerFixture fixture) => _fixture = fixture;

    /// <inheritdoc/>
    protected override IEventStore CreateEventStore()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "MongoDB durable-execution conformance runs against real infrastructure and is never skipped");

        var options = Options.Create(new MongoDbEventStoreOptions
        {
            ConnectionString = _fixture.ConnectionString,
            DatabaseName = _fixture.DatabaseName,
        });

        return new MongoDbEventStore(options, NullLogger<MongoDbEventStore>.Instance);
    }

    /// <inheritdoc/>
    protected override async Task CleanupAsync() => await _fixture.CleanupAsync().ConfigureAwait(false);

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
