// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Persistence;

/// <summary>
/// Author≠impl WIRE lock for wzjj2w: <see cref="CloudNativeOutboxStoreExtensions"/> routes a store that IS an
/// <see cref="ICloudNativeOutboxStoreBatch"/> (Cosmos/DynamoDb/Firestore, which declare it) to its REAL batch
/// implementation, and gives a non-batch store the documented no-op fallback — for ALL four batch/admin
/// operations, not just <c>AddBatch</c>.
/// </summary>
/// <remarks>
/// <b>safety∧liveness:</b> a lock that only asserted the fallback (safety) would pass against an extension that
/// NEVER routes to the real batch path. The liveness arm asserts each of the four operations actually reaches
/// the batch implementation when the capability is present; the safety arm asserts a store lacking the
/// capability takes the graceful no-op (unsuccessful result), never throwing and never a phantom batch call.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudNativeOutboxBatchRoutingShould
{
    private static readonly CloudBatchResult BatchOk = new(true, 1.0, []);
    private static readonly CloudCleanupResult CleanupOk = new(3, 1.0);
    private static readonly CloudOperationResult OperationOk = new(true, 0, 1.0);

    [Fact]
    public async Task RouteEveryOperationToTheRealBatchPath_WhenTheStoreIsBatchCapable()
    {
        // LIVENESS — a store that IS ICloudNativeOutboxStoreBatch must reach its real batch methods for ALL
        // four operations (enumerate every reachable path, not a representative sample).
        var store = A.Fake<ICloudNativeOutboxStore>(x => x.Implements<ICloudNativeOutboxStoreBatch>());
        var batch = (ICloudNativeOutboxStoreBatch)store;
        var pk = A.Fake<IPartitionKey>();

        A.CallTo(() => batch.AddBatchAsync(A<IEnumerable<CloudOutboxMessage>>._, A<IPartitionKey>._, A<CancellationToken>._)).Returns(BatchOk);
        A.CallTo(() => batch.MarkBatchAsPublishedAsync(A<IEnumerable<string>>._, A<IPartitionKey>._, A<CancellationToken>._)).Returns(BatchOk);
        A.CallTo(() => batch.CleanupOldMessagesAsync(A<IPartitionKey>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(CleanupOk);
        A.CallTo(() => batch.IncrementRetryCountAsync(A<string>._, A<IPartitionKey>._, A<string?>._, A<CancellationToken>._)).Returns(OperationOk);

        var add = await store.AddBatchAsync([], pk, CancellationToken.None);
        var mark = await store.MarkBatchAsPublishedAsync([], pk, CancellationToken.None);
        var cleanup = await store.CleanupOldMessagesAsync(pk, TimeSpan.FromDays(1), CancellationToken.None);
        var retry = await store.IncrementRetryCountAsync("m-1", pk, "boom", CancellationToken.None);

        // Reached the real batch impl (the configured success results, not the false fallbacks).
        add.Success.ShouldBeTrue();
        mark.Success.ShouldBeTrue();
        cleanup.DeletedCount.ShouldBe(3);
        retry.Success.ShouldBeTrue();

        A.CallTo(() => batch.AddBatchAsync(A<IEnumerable<CloudOutboxMessage>>._, A<IPartitionKey>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => batch.MarkBatchAsPublishedAsync(A<IEnumerable<string>>._, A<IPartitionKey>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => batch.CleanupOldMessagesAsync(A<IPartitionKey>._, A<TimeSpan>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => batch.IncrementRetryCountAsync(A<string>._, A<IPartitionKey>._, A<string?>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task FallBackToNoOp_ForEveryOperation_WhenTheStoreIsNotBatchCapable()
    {
        // SAFETY — a plain ICloudNativeOutboxStore (does NOT implement the batch interface) must take the
        // documented graceful no-op fallback for every operation: an unsuccessful result, never a throw.
        var store = A.Fake<ICloudNativeOutboxStore>();
        var pk = A.Fake<IPartitionKey>();

        (await store.AddBatchAsync([], pk, CancellationToken.None)).Success.ShouldBeFalse();
        (await store.MarkBatchAsPublishedAsync([], pk, CancellationToken.None)).Success.ShouldBeFalse();
        (await store.CleanupOldMessagesAsync(pk, TimeSpan.FromDays(1), CancellationToken.None)).DeletedCount.ShouldBe(0);
        (await store.IncrementRetryCountAsync("m-1", pk, null, CancellationToken.None)).Success.ShouldBeFalse();
    }
}
