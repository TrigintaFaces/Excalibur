// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Testing;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for <c>ICloudNativeOutboxStore</c> conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// <c>ICloudNativeOutboxStore</c> is a change-feed-oriented outbox contract for serverless/document
/// databases (Cosmos DB, DynamoDB, Firestore) - a different shape from the polling-based
/// <c>IOutboxStore</c> this package also certifies (<see cref="OutboxStoreConformanceTestKit"/>): different
/// message type, different return types (every operation reports request cost), and every member takes an
/// explicit <see cref="IPartitionKey"/> rather than resolving one from ambient state.
/// </para>
/// <para>
/// Two capabilities are optional and gated: <see cref="ICloudNativeOutboxStoreBatch"/> (batch add/mark/
/// cleanup, and the retry-count floor) and <see cref="ICloudNativeOutboxStoreClaim"/> (the atomic claim a
/// self-managed poller needs instead of the provider's change-feed trigger). A store that does not
/// implement one reports its arms as skipped via <see cref="ConformanceTestKit.OnArmSkipped"/> rather than
/// failing - see that type's remarks for how to make a required capability failing instead of skipped.
/// </para>
/// <para>
/// The atomic-claim arms are the ones a mocked client cannot certify: the exclusion property depends on the
/// provider's own conditional-write primitive rejecting a losing claimant's write, which a test double
/// cannot reproduce by construction. Certify those arms against a real provider endpoint or its emulator.
/// </para>
/// <para>
/// <b>This kit is trim-excluded, not trim-safe, and that is a statement about the outbox-store contract
/// rather than about the kit.</b> The arms read stored messages back through the store, and a conformant store deserializes the message payload. No annotation on this kit can reach
/// those types, so a deriving suite must itself carry
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> — or suppress the
/// warning deliberately — when it is compiled with the trim analyzer enabled. Overriding an arm
/// rather than wrapping it requires the same annotation on the override. A trimmed test host is not
/// a supported configuration for this kit.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
	"Cloud-native outbox conformance arms read stored messages back through the store, which deserializes the message payload reflectively. A trimmed test host is not a supported configuration for this kit.")]
public abstract class CloudNativeOutboxStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a fresh <see cref="ICloudNativeOutboxStore"/> instance for testing, addressed at a partition
	/// no other arm has touched.
	/// </summary>
	/// <returns>A store implementation to test.</returns>
	/// <remarks>
	/// Asynchronous for the same reason as the polling kit's seam: a real provider's construction (starting
	/// an emulator, waiting for its data plane, creating a container/table) is an await, and a synchronous
	/// seam would force every deriver into sync-over-async.
	/// </remarks>
	protected abstract Task<ICloudNativeOutboxStore> CreateStoreAsync();

	/// <summary>
	/// Creates a fresh, arm-private <see cref="IPartitionKey"/> so concurrent arms (and repeated runs) never
	/// share state.
	/// </summary>
	/// <returns>A new partition key.</returns>
	protected virtual IPartitionKey CreatePartitionKey() => new PartitionKey($"pk-{Guid.NewGuid():N}");

	/// <summary>Creates a test message addressed at <paramref name="partitionKey"/>.</summary>
	/// <param name="partitionKey">The partition the message is staged into.</param>
	/// <returns>A new <see cref="CloudOutboxMessage"/> carrying representative field values.</returns>
	protected virtual CloudOutboxMessage CreateTestMessage(IPartitionKey partitionKey) => new()
	{
		MessageId = $"msg-{Guid.NewGuid():N}",
		MessageType = "ConformanceTestMessageType",
		Payload = "conformance-payload"u8.ToArray(),
		Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["header-one"] = "value-one" },
		AggregateId = "aggregate-1",
		AggregateType = "ConformanceAggregate",
		CorrelationId = "corr-1",
		CausationId = "cause-1",
		TenantId = "tenant-1",
		Destination = "conformance-destination",
		CreatedAt = DateTimeOffset.UtcNow,
		PartitionKeyValue = partitionKey.Value,
	};

	private static void Assert(bool condition, string message)
	{
		if (!condition)
		{
			throw new TestFixtureAssertionException(message);
		}
	}

	#region Core arms - required (ICloudNativeOutboxStore)

	/// <summary>Adding a new message succeeds.</summary>
	public virtual async Task AddAsync_NewMessage_ReturnsSuccessResult()
	{
		RecordArmExecuted(nameof(AddAsync_NewMessage_ReturnsSuccessResult));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);

		var result = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		Assert(result.Success, "adding a new, valid message must succeed");
	}

	/// <summary>LIVENESS: a staged message is returned by a subsequent pending read of its partition.</summary>
	public virtual async Task AddAsync_ThenGetPending_ReturnsTheStagedMessage()
	{
		RecordArmExecuted(nameof(AddAsync_ThenGetPending_ReturnsTheStagedMessage));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);

		Assert(
			pending.Documents.Any(m => m.MessageId == message.MessageId),
			$"expected staged message '{message.MessageId}' to be returned by a pending read of its partition");
	}

	/// <summary>
	/// Round-trip fidelity: every canonical field on a staged message survives being read back - not just
	/// the identifier.
	/// </summary>
	public virtual async Task AddAsync_PreservesCanonicalFields_OnRoundTrip()
	{
		RecordArmExecuted(nameof(AddAsync_PreservesCanonicalFields_OnRoundTrip));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);
		var reloaded = pending.Documents.Single(m => m.MessageId == message.MessageId);

		Assert(reloaded.MessageType == message.MessageType, "MessageType must round-trip");
		Assert(reloaded.Payload.SequenceEqual(message.Payload), "Payload must round-trip byte-identical");
		Assert(reloaded.AggregateId == message.AggregateId, "AggregateId must round-trip");
		Assert(reloaded.AggregateType == message.AggregateType, "AggregateType must round-trip");
		Assert(reloaded.CorrelationId == message.CorrelationId, "CorrelationId must round-trip");
		Assert(reloaded.CausationId == message.CausationId, "CausationId must round-trip");
		Assert(reloaded.TenantId == message.TenantId, "TenantId must round-trip");
		Assert(reloaded.Destination == message.Destination, "Destination must round-trip");
		Assert(reloaded.PartitionKeyValue == message.PartitionKeyValue, "PartitionKeyValue must round-trip");
		Assert(!reloaded.IsPublished, "a freshly-staged message must not read back as published");
	}

	/// <summary>A partition nothing was ever staged into reads back empty, not an error.</summary>
	public virtual async Task GetPendingAsync_EmptyPartition_ReturnsEmpty()
	{
		RecordArmExecuted(nameof(GetPendingAsync_EmptyPartition_ReturnsEmpty));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);

		Assert(pending.Documents.Count == 0, "a never-staged partition must read back empty");
	}

	/// <summary>LIVENESS: messages read back in the order they were staged (FIFO).</summary>
	public virtual async Task GetPendingAsync_ReturnsMessagesInFifoOrder()
	{
		RecordArmExecuted(nameof(GetPendingAsync_ReturnsMessagesInFifoOrder));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var staged = new List<string>();
		for (var i = 0; i < 3; i++)
		{
			var message = CreateTestMessage(partitionKey);
			staged.Add(message.MessageId);
			_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		}

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);

		Assert(
			pending.Documents.Select(static m => m.MessageId).SequenceEqual(staged),
			"pending messages must read back in the order they were staged (FIFO)");
	}

	/// <summary>SAFETY: a published message is no longer returned by a pending read of its partition.</summary>
	public virtual async Task MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage()
	{
		RecordArmExecuted(nameof(MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var marked = await store.MarkAsPublishedAsync(message.MessageId, partitionKey, CancellationToken.None)
			.ConfigureAwait(false);
		Assert(marked.Success, $"marking an existing message as published must succeed. {marked.ErrorMessage}");

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);
		Assert(
			!pending.Documents.Any(m => m.MessageId == message.MessageId),
			"a published message must not still appear in a pending read of its partition");
	}

	/// <summary>Marking a message that does not exist reports failure rather than a false success.</summary>
	public virtual async Task MarkAsPublishedAsync_UnknownMessage_ReturnsFailureResult()
	{
		RecordArmExecuted(nameof(MarkAsPublishedAsync_UnknownMessage_ReturnsFailureResult));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();

		var result = await store.MarkAsPublishedAsync($"nonexistent-{Guid.NewGuid():N}", partitionKey, CancellationToken.None)
			.ConfigureAwait(false);

		Assert(!result.Success, "marking a message that was never staged must not report success");
	}

	#endregion

	#region Batch arms - optional (ICloudNativeOutboxStoreBatch)

	/// <summary>Batch-adding several messages stages every one of them.</summary>
	public virtual async Task AddBatchAsync_AddsAllMessages_AndTheyAreAllPending()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(AddBatchAsync_AddsAllMessages_AndTheyAreAllPending), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(AddBatchAsync_AddsAllMessages_AndTheyAreAllPending));
		var partitionKey = CreatePartitionKey();
		var messages = Enumerable.Range(0, 3).Select(_ => CreateTestMessage(partitionKey)).ToList();

		var result = await batch.AddBatchAsync(messages, partitionKey, CancellationToken.None).ConfigureAwait(false);
		Assert(result.Success, "batch-adding valid messages must succeed");

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);
		var pendingIds = pending.Documents.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);
		var expectedIds = messages.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);
		Assert(pendingIds.SetEquals(expectedIds), "every message in the batch must be pending after AddBatchAsync");
	}

	/// <summary>Batch-marking several messages as published removes every one of them from the pending set.</summary>
	public virtual async Task MarkBatchAsPublishedAsync_MarksAllAsPublished()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(MarkBatchAsPublishedAsync_MarksAllAsPublished), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(MarkBatchAsPublishedAsync_MarksAllAsPublished));
		var partitionKey = CreatePartitionKey();
		var messages = Enumerable.Range(0, 3).Select(_ => CreateTestMessage(partitionKey)).ToList();
		_ = await batch.AddBatchAsync(messages, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var result = await batch.MarkBatchAsPublishedAsync(
			messages.Select(static m => m.MessageId), partitionKey, CancellationToken.None).ConfigureAwait(false);
		Assert(result.Success, $"batch-marking existing messages as published must succeed. {result.ErrorMessage}");

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);
		Assert(pending.Documents.Count == 0, "every batch-marked message must be gone from the pending set");
	}

	/// <summary>
	/// SAFETY: cleanup deletes only PUBLISHED messages older than the retention window - never an
	/// unpublished message, and never a published message still inside the window.
	/// </summary>
	public virtual async Task CleanupOldMessagesAsync_DeletesOnlyPublishedMessagesOlderThanRetention()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(CleanupOldMessagesAsync_DeletesOnlyPublishedMessagesOlderThanRetention), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(CleanupOldMessagesAsync_DeletesOnlyPublishedMessagesOlderThanRetention));
		var partitionKey = CreatePartitionKey();

		var unpublished = CreateTestMessage(partitionKey);
		var publishedRecent = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(unpublished, partitionKey, CancellationToken.None).ConfigureAwait(false);
		_ = await store.AddAsync(publishedRecent, partitionKey, CancellationToken.None).ConfigureAwait(false);
		_ = await store.MarkAsPublishedAsync(publishedRecent.MessageId, partitionKey, CancellationToken.None).ConfigureAwait(false);

		// A retention window far longer than "just published" - a correct cleanup must leave this alone.
		_ = await batch.CleanupOldMessagesAsync(partitionKey, TimeSpan.FromDays(365), CancellationToken.None).ConfigureAwait(false);

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);
		Assert(
			pending.Documents.Any(m => m.MessageId == unpublished.MessageId),
			"cleanup must never delete an unpublished message, regardless of retention window");
	}

	/// <summary>Incrementing the retry count on a message records it and the error.</summary>
	public virtual async Task IncrementRetryCountAsync_IncrementsRetryCountAndRecordsError()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(IncrementRetryCountAsync_IncrementsRetryCountAndRecordsError), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(IncrementRetryCountAsync_IncrementsRetryCountAndRecordsError));
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var result = await batch.IncrementRetryCountAsync(
			message.MessageId, partitionKey, "transient publish failure", CancellationToken.None).ConfigureAwait(false);
		Assert(result.Success, "incrementing the retry count on an existing message must succeed");

		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);
		var reloaded = pending.Documents.Single(m => m.MessageId == message.MessageId);
		Assert(reloaded.RetryCount == 1, $"expected RetryCount 1 after one increment, got {reloaded.RetryCount}");
		Assert(reloaded.LastError == "transient publish failure", "the recorded error message must round-trip");
	}

	/// <summary>
	/// The retry-visibility floor: the retry count is MONOTONIC across repeated failures of the same
	/// message - it never resets or is overwritten with a smaller value, mirroring the guarantee the
	/// flat (non-cloud-native) backends provide via <c>NextAttemptAt</c> in <c>MarkFailedAsync</c>.
	/// </summary>
	public virtual async Task IncrementRetryCountAsync_IsMonotonic_AcrossRepeatedFailures()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(IncrementRetryCountAsync_IsMonotonic_AcrossRepeatedFailures), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(IncrementRetryCountAsync_IsMonotonic_AcrossRepeatedFailures));
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var previous = 0;
		for (var attempt = 1; attempt <= 3; attempt++)
		{
			_ = await batch.IncrementRetryCountAsync(
				message.MessageId, partitionKey, $"failure #{attempt}", CancellationToken.None).ConfigureAwait(false);

			var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);
			var reloaded = pending.Documents.Single(m => m.MessageId == message.MessageId);

			Assert(
				reloaded.RetryCount > previous,
				$"the retry count must strictly increase on each failure (was {previous}, now {reloaded.RetryCount}), never reset");
			previous = reloaded.RetryCount;
		}

		Assert(previous == 3, $"expected 3 after three increments, got {previous}");
	}

	#endregion

	#region Claim arms - optional (ICloudNativeOutboxStoreClaim)

	/// <summary>An unclaimed message is returned by a claim, up to the requested batch size.</summary>
	public virtual async Task ClaimPendingAsync_ReturnsUnclaimedMessages_UpToBatchSize()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreClaim claimable)
		{
			SkipArm(nameof(ClaimPendingAsync_ReturnsUnclaimedMessages_UpToBatchSize), typeof(ICloudNativeOutboxStoreClaim),
				"store does not implement ICloudNativeOutboxStoreClaim");
			return;
		}

		RecordArmExecuted(nameof(ClaimPendingAsync_ReturnsUnclaimedMessages_UpToBatchSize));
		var partitionKey = CreatePartitionKey();
		for (var i = 0; i < 5; i++)
		{
			_ = await store.AddAsync(CreateTestMessage(partitionKey), partitionKey, CancellationToken.None).ConfigureAwait(false);
		}

		var claimed = await claimable.ClaimPendingAsync(partitionKey, 3, "claimant-1", CancellationToken.None).ConfigureAwait(false);

		Assert(claimed.Documents.Count == 3, $"expected exactly 3 claimed messages (batchSize), got {claimed.Documents.Count}");
	}

	/// <summary>A claimed message carries the lease owner and instant the claim stamped.</summary>
	public virtual async Task ClaimPendingAsync_StampsLeaseOwnerAndInstant()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreClaim claimable)
		{
			SkipArm(nameof(ClaimPendingAsync_StampsLeaseOwnerAndInstant), typeof(ICloudNativeOutboxStoreClaim),
				"store does not implement ICloudNativeOutboxStoreClaim");
			return;
		}

		RecordArmExecuted(nameof(ClaimPendingAsync_StampsLeaseOwnerAndInstant));
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var before = DateTimeOffset.UtcNow;
		var claimed = await claimable.ClaimPendingAsync(partitionKey, 10, "claimant-owner", CancellationToken.None).ConfigureAwait(false);

		var stamped = claimed.Documents.Single(m => m.MessageId == message.MessageId);
		Assert(stamped.LeasedBy == "claimant-owner", "a claimed message must be stamped with the claiming caller's id");
		Assert(stamped.LeasedAt.HasValue, "a claimed message must be stamped with a lease instant");
		Assert(
			stamped.LeasedAt!.Value >= before.AddSeconds(-5),
			"the stamped lease instant must be at or after the claim call, not stale/default data");
	}

	/// <summary>SAFETY: a message already claimed within its lease window is not handed to another claimant.</summary>
	public virtual async Task ClaimPendingAsync_DoesNotReturnAlreadyClaimedMessages_WithinTheLeaseWindow()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreClaim claimable)
		{
			SkipArm(nameof(ClaimPendingAsync_DoesNotReturnAlreadyClaimedMessages_WithinTheLeaseWindow), typeof(ICloudNativeOutboxStoreClaim),
				"store does not implement ICloudNativeOutboxStoreClaim");
			return;
		}

		RecordArmExecuted(nameof(ClaimPendingAsync_DoesNotReturnAlreadyClaimedMessages_WithinTheLeaseWindow));
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var first = await claimable.ClaimPendingAsync(partitionKey, 10, "claimant-first", CancellationToken.None).ConfigureAwait(false);
		Assert(first.Documents.Any(m => m.MessageId == message.MessageId), "the first claimant must win the only staged message");

		var second = await claimable.ClaimPendingAsync(partitionKey, 10, "claimant-second", CancellationToken.None).ConfigureAwait(false);
		Assert(
			!second.Documents.Any(m => m.MessageId == message.MessageId),
			"a message claimed within its lease window must not be handed to a second claimant");
	}

	/// <summary>
	/// SAFETY and LIVENESS together (the atomic-claim property this contract exists to provide): two
	/// claimants racing over one partition must, between them, hand out every message exactly once.
	/// Disjointness alone is satisfied by a store that claims nothing for anybody - the union of what both
	/// claimants won must also equal every message staged.
	/// </summary>
	public virtual async Task ClaimPendingAsync_ConcurrentClaimants_ReceiveDisjointSets()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreClaim claimable)
		{
			SkipArm(nameof(ClaimPendingAsync_ConcurrentClaimants_ReceiveDisjointSets), typeof(ICloudNativeOutboxStoreClaim),
				"store does not implement ICloudNativeOutboxStoreClaim");
			return;
		}

		RecordArmExecuted(nameof(ClaimPendingAsync_ConcurrentClaimants_ReceiveDisjointSets));
		var partitionKey = CreatePartitionKey();
		const int StagedCount = 10;
		var staged = new List<string>();
		for (var i = 0; i < StagedCount; i++)
		{
			var message = CreateTestMessage(partitionKey);
			staged.Add(message.MessageId);
			_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		}

		var claimATask = claimable.ClaimPendingAsync(partitionKey, StagedCount, "claimant-a", CancellationToken.None);
		var claimBTask = claimable.ClaimPendingAsync(partitionKey, StagedCount, "claimant-b", CancellationToken.None);
		var results = await Task.WhenAll(claimATask, claimBTask).ConfigureAwait(false);

		var idsA = results[0].Documents.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);
		var idsB = results[1].Documents.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);

		Assert(!idsA.Overlaps(idsB), "two concurrent claimants must never both win the same message");
		Assert(idsA.Union(idsB).ToHashSet(StringComparer.Ordinal).SetEquals(staged), "between them, the claimants must win every staged message");
	}

	#endregion

	#region Harness guard

	#endregion
}
