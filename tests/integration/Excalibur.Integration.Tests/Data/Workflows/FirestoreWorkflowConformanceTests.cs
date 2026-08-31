// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Firestore;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Workflows;

/// <summary>
/// Durable-execution conformance for the workflow engine over <see cref="FirestoreEventStore"/>, using the
/// shared <see cref="WorkflowConformanceTestKit"/>. Verifies crash-recovery and exactly-once against a real
/// Firestore journal, whose optimistic concurrency on append backs the exactly-once guarantee.
/// </summary>
/// <remarks>
/// Cloud provider: this suite is CLOUD-GATED — it runs when the Firestore emulator fixture is provisioned and
/// is otherwise skipped (<c>Assert.SkipUnless</c>), mirroring the provider-parity posture. Binds the
/// emulator-connected <c>FirestoreDb</c> (default serializer) directly so the store talks to real infrastructure.
/// </remarks>
[Collection(FirestoreEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class FirestoreWorkflowConformanceTests
    : WorkflowConformanceTestKit
{
    private readonly FirestoreEventStoreContainerFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="FirestoreWorkflowConformanceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared Firestore EventStore container fixture.</param>
    public FirestoreWorkflowConformanceTests(FirestoreEventStoreContainerFixture fixture) => _fixture = fixture;

    /// <inheritdoc/>
    protected override IEventStore CreateEventStore()
    {
        // Cloud-gated: run when the emulator fixture is provisioned, otherwise skip.
        Assert.SkipUnless(
            _fixture.DockerAvailable,
            "Firestore durable-execution conformance runs against the Firestore emulator when a fixture is provisioned.");

        var options = Options.Create(new FirestoreEventStoreOptions
        {
            ProjectId = _fixture.ProjectId,
            EventsCollectionName = _fixture.CollectionName,
            EmulatorHost = _fixture.EmulatorEndpoint,
        });

        return new FirestoreEventStore(_fixture.Db, options, NullLogger<FirestoreEventStore>.Instance, UntenantedContext.Instance);
    }

    /// <inheritdoc/>
    protected override async Task CleanupAsync() => await _fixture.CleanupCollectionAsync().ConfigureAwait(false);

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

    [Fact]
    public override Task ConformanceSuite_ShouldWireEveryArm() =>
    	base.ConformanceSuite_ShouldWireEveryArm();
}
