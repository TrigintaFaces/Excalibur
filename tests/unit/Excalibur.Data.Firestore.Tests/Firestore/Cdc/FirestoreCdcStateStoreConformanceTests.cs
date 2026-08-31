// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Firestore;
using Excalibur.Dispatch;
using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Data.Firestore.Tests.Firestore.Cdc;

/// <summary>
/// Runs <see cref="CdcProviderConformanceTestKit"/> against the Firestore CDC state store's in-memory
/// implementation, so Firestore's <see cref="ICdcStateStore"/> behaviour is checked by the same shared
/// suite as every other provider rather than only by its own bespoke tests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coverage boundary — read before treating this green as broader than it is.</b> The subject is
/// <c>InMemoryFirestoreCdcStateStore</c>. The REAL <c>FirestoreCdcStateStore</c>, which talks to Firestore,
/// remains UNCERTIFIED by this kit: nothing here exercises its serialization, its transactions, or its
/// optimistic concurrency. What this does certify is the contract logic both share — the explicit
/// <see cref="ICdcStateStore"/> implementations that convert between the Firestore position shape and the
/// framework's, which is where a provider most easily diverges from the shared contract.
/// </para>
/// <para>
/// The in-memory subject is deliberate, not a compromise for convenience. A deriver against real Firestore
/// would be skip-gated wherever no emulator is provisioned, and an arm that never executes is worse than an
/// absent one: it reports coverage in the results while asserting nothing. This runs everywhere, every time.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "Test method naming convention")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class FirestoreCdcStateStoreConformanceTests : CdcProviderConformanceTestKit
{
    private const string CollectionPath = "conformance/cdc";

    /// <summary>
    /// Exposes the kit's own wiring check to the runner. The check is an arm like any other, so a suite
    /// that omits THIS member disables it silently — the one gap it cannot report itself.
    /// </summary>
    /// <returns>A completed task when every arm in the kit is wired.</returns>
    [Fact]
    public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
        ConformanceSuite_ShouldWireEveryArm();

    /// <inheritdoc />
    protected override Task<ICdcStateStore> CreateStateStoreAsync() =>
        Task.FromResult<ICdcStateStore>(new InMemoryFirestoreCdcStateStore());

    /// <inheritdoc />
    /// <remarks>
    /// Distinct per index on both the update time and the document id: several arms save different
    /// positions and assert the right one came back, and positions that compared equal would let a store
    /// that returned the wrong checkpoint pass.
    /// </remarks>
    protected override ChangePosition CreateTestPosition(int index) =>
        FirestoreCdcPosition.FromUpdateTime(
            CollectionPath,
            DateTimeOffset.UnixEpoch.AddSeconds(index),
            $"doc-{index:D6}");

    [Fact] public Task SaveAndGetPosition_RoundTrips_Test() => SaveAndGetPosition_RoundTrips();
    [Fact] public Task GetPosition_NoCheckpoint_ReturnsNull_Test() => GetPosition_NoCheckpoint_ReturnsNull();
    [Fact] public Task SavePosition_MultipleConsumers_Independent_Test() => SavePosition_MultipleConsumers_Independent();
    [Fact] public Task SavePosition_Overwrites_PreviousCheckpoint_Test() => SavePosition_Overwrites_PreviousCheckpoint();
    [Fact] public Task SavePosition_PreservesPositionValidity_Test() => SavePosition_PreservesPositionValidity();
    [Fact] public Task Resume_FromSavedCheckpoint_ReturnsCorrectPosition_Test() => Resume_FromSavedCheckpoint_ReturnsCorrectPosition();
    [Fact] public Task Resume_AfterDelete_ReturnsNull_Test() => Resume_AfterDelete_ReturnsNull();
    [Fact] public Task DeletePosition_ExistingCheckpoint_ReturnsTrue_Test() => DeletePosition_ExistingCheckpoint_ReturnsTrue();
    [Fact] public Task DeletePosition_NonExistentCheckpoint_ReturnsFalse_Test() => DeletePosition_NonExistentCheckpoint_ReturnsFalse();
    [Fact] public Task DeletePosition_DoesNotAffectOtherConsumers_Test() => DeletePosition_DoesNotAffectOtherConsumers();
    [Fact] public Task GetAllPositions_ReturnsAllConsumerCheckpoints_Test() => GetAllPositions_ReturnsAllConsumerCheckpoints();
    [Fact] public Task GetAllPositions_EmptyStore_ReturnsEmpty_Test() => GetAllPositions_EmptyStore_ReturnsEmpty();
    [Fact] public Task ConcurrentSavePosition_AllSucceed_Test() => ConcurrentSavePosition_AllSucceed();
    [Fact] public Task ConcurrentSavePosition_SameConsumer_LastWriteWins_Test() => ConcurrentSavePosition_SameConsumer_LastWriteWins();
}
