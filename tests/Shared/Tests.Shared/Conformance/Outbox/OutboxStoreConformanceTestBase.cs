// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Tests.Shared.Conformance.Outbox;

/// <summary>
/// Base class for IOutboxStore conformance tests.
/// Implementations must provide a concrete IOutboxStore instance for testing.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies that outbox store implementations
/// correctly implement the IOutboxStore interface contract, including:
/// </para>
/// <list type="bullet">
///   <item>Entry staging and enqueuing</item>
///   <item>Status transitions (Staged -> Sending -> Sent/Failed)</item>
///   <item>Message retrieval by status</item>
///   <item>Cleanup operations</item>
///   <item>Statistics tracking</item>
///   <item>Concurrent access and atomicity</item>
/// </list>
/// <para>
/// To create conformance tests for your own IOutboxStore implementation:
/// <list type="number">
///   <item>Inherit from OutboxStoreConformanceTestBase</item>
///   <item>Override CreateStoreAsync() to create an instance of your IOutboxStore implementation</item>
///   <item>Override CleanupAsync() to properly clean up the store between tests</item>
/// </list>
/// </para>
/// </remarks>
public abstract class OutboxStoreConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// The outbox store instance under test.
	/// </summary>
	protected IOutboxStore Store { get; private set; } = null!;

	/// <summary>
	/// The admin interface for the outbox store under test.
	/// </summary>
	protected IOutboxStoreAdmin Admin { get; private set; } = null!;

	/// <inheritdoc/>
	/// <remarks>
	/// Cleans BEFORE the test as well as after it, and the "before" is the one that matters.
	///
	/// Cleanup used to run only in <see cref="DisposeAsync"/>, which makes every test's starting state a
	/// function of whether its PREDECESSOR cleaned up successfully. A test that fails partway, a store
	/// whose delete lags its commit, or a provider whose cleanup silently misses rows all leave residue,
	/// and the next test to assert an exact count is the one that fails. That is why this suite failed a
	/// DIFFERENT test on each run while passing in isolation -- the defect was never in the test that
	/// reported it.
	///
	/// Tearing down after is still correct and stays; leaving a shared table dirty for the next class is
	/// its own problem. But only cleaning first makes a test independent of everything that ran before it.
	/// </remarks>
	public async ValueTask InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
		Admin = (Store as IOutboxStoreAdmin)!;

		// ResetDataAsync, NOT CleanupAsync. The pre-test hook must clear residual DATA while leaving the
		// store just created above fully usable. CleanupAsync is end-of-test TEARDOWN, and derivers whose
		// teardown also disposes a client (Redis multiplexer, Marten IDocumentStore, CosmosClient) were
		// having that client disposed here -- before the first assertion ran. Every arm in those classes
		// then failed with ObjectDisposedException, and because the store is rebuilt per test (xUnit
		// constructs a fresh instance per test) it failed on EVERY arm, not just the second onward.
		await ResetDataAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);

		if (Store is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (Store is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Creates a new instance of the IOutboxStore implementation under test.
	/// </summary>
	/// <returns>A configured IOutboxStore instance.</returns>
	protected abstract Task<IOutboxStore> CreateStoreAsync();

	/// <summary>
	/// Cleans up the IOutboxStore instance after each test. This is TEARDOWN: it may dispose clients and
	/// connections the deriver opened, because nothing runs against the store afterwards.
	/// </summary>
	protected abstract Task CleanupAsync();

	/// <summary>
	/// Clears residual DATA before each test, leaving the freshly created store usable.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="CleanupAsync"/>, which is correct for any deriver whose teardown only
	/// deletes rows/keys/documents. A deriver whose <see cref="CleanupAsync"/> ALSO disposes a connection
	/// or client MUST override this with the data-only half -- otherwise it disposes the store the test
	/// is about to use. The split exists because "clean up after" and "reset before" are different
	/// operations that were previously the same method.
	/// </remarks>
	/// <returns>A task that completes when residual data has been cleared.</returns>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// y1moc0 (GUIDE ruling 1) — whether the store-under-test RETAINS sent messages (sent-tracking) or is a
	/// delete-on-sent store. Delete-on-sent stores (Postgres / Oracle) advertise
	/// <see cref="IOutboxStoreCapabilities.SupportsSentTracking"/> == <see langword="false"/>; every other
	/// store (Marten / SqlServer / Mongo / InMemory) does not implement the capability and defaults to
	/// tracking. This is a capability-as-data query on the store instance (mirroring
	/// <c>IInboxStoreCapabilities</c>), NOT a base virtual/skip — the sent-tracking facts below are
	/// <b>inverted</b> per this flag (both arms asserted, testing-patterns §3), never skipped: a tracking
	/// store fails the delete-on-sent arm and a delete-on-sent store fails the tracking arm, so neither arm
	/// is vacuous.
	/// </summary>
	protected bool SupportsSentTracking =>
		Store is not IOutboxStoreCapabilities capabilities || capabilities.SupportsSentTracking;

	#region Helper Methods

	/// <summary>
	/// Creates a test outbound message with the given parameters.
	/// </summary>
	protected static OutboundMessage CreateTestMessage(
		string? id = null,
		string? messageType = null,
		string? destination = null,
		DateTimeOffset? scheduledAt = null)
	{
		return new OutboundMessage(
			messageType ?? "Test.MessageType",
			"test-payload"u8.ToArray(),
			destination ?? "test-queue")
		{
			Id = id ?? Guid.NewGuid().ToString(),
			ScheduledAt = scheduledAt
		};
	}

	#endregion Helper Methods

	#region Interface Implementation Tests

	[Fact]
	public void Store_ShouldImplementIOutboxStore()
	{
		// Assert
		_ = Store.ShouldBeAssignableTo<IOutboxStore>();
	}

	#endregion Interface Implementation Tests

	#region StageMessage Tests

	[Fact]
	public async Task StageMessage_ValidMessage_StagesSuccessfully()
	{
		// Arrange
		var message = CreateTestMessage();

		// Act
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert - Message should be retrievable as unsent
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task StageMessage_WithNullMessage_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await Store.StageMessageAsync(null!, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task StageMessage_DuplicateId_ThrowsInvalidOperationException()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert - Staging with same ID should fail
		var duplicate = CreateTestMessage(id: message.Id);
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store.StageMessageAsync(duplicate, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task StageMessage_WithScheduledTime_SetsScheduledAt()
	{
		// Arrange
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(1);
		var message = CreateTestMessage(scheduledAt: scheduledTime);

		// Act
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert - Should not appear in unsent (because it's scheduled)
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldNotContain(m => m.Id == message.Id, "Scheduled messages should not appear in unsent");

		// Should appear in scheduled
		var scheduled = await Admin.GetScheduledMessagesAsync(
			scheduledTime.AddMinutes(1), 10, CancellationToken.None).ConfigureAwait(false);
		scheduled.ShouldContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task StageMessage_MultipleMessages_AllStaged()
	{
		// Arrange
		var messages = Enumerable.Range(0, 5)
			.Select(_ => CreateTestMessage())
			.ToList();

		// Act
		foreach (var message in messages)
		{
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Assert
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.Count().ShouldBe(5);
	}

	[Fact]
	public async Task StageMessage_ConcurrentStaging_AllSucceed()
	{
		// Arrange
		const int concurrentMessages = 10;
		var messages = Enumerable.Range(0, concurrentMessages)
			.Select(_ => CreateTestMessage())
			.ToList();

		// Act - Stage all concurrently
		var tasks = messages.Select(m =>
			Store.StageMessageAsync(m, CancellationToken.None).AsTask());
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		var unsent = await Store.GetUnsentMessagesAsync(20, CancellationToken.None).ConfigureAwait(false);
		unsent.Count().ShouldBe(concurrentMessages);
	}

	[Fact]
	public async Task StageMessage_PreservesMessageProperties()
	{
		// Arrange
		var message = new OutboundMessage(
			"Test.OrderPlaced",
			"order-data"u8.ToArray(),
			"orders-queue")
		{
			CorrelationId = "corr-123",
			CausationId = "cause-456",
			TenantId = "tenant-abc",
			Priority = 5
		};

		// Act
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var retrieved = unsent.FirstOrDefault(m => m.Id == message.Id);

		_ = retrieved.ShouldNotBeNull();
		retrieved.MessageType.ShouldBe("Test.OrderPlaced");
		retrieved.Destination.ShouldBe("orders-queue");
		retrieved.CorrelationId.ShouldBe("corr-123");
		retrieved.CausationId.ShouldBe("cause-456");
		retrieved.TenantId.ShouldBe("tenant-abc");
		retrieved.Priority.ShouldBe(5);
	}

	#endregion StageMessage Tests

	#region Full-Field Round-Trip Conformance (n20aqx)

	/// <summary>
	/// n20aqx (S872, da8mc3) — author≠impl UNIVERSAL full-field round-trip conformance, reused by every
	/// outbox-store conformance subclass. Stages an <see cref="OutboundMessage"/> with EVERY
	/// consumer-supplied field populated and asserts each survives the serialize→store→reload boundary.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The persisted-then-reloaded projection MUST preserve every field the consumer set — not just the
	/// commonly-checked <c>MessageType</c>/<c>Destination</c>, but the routing/ordering fields
	/// (<see cref="OutboundMessage.PartitionKey"/>, <see cref="OutboundMessage.GroupKey"/>,
	/// <see cref="OutboundMessage.TargetTransports"/>, <see cref="OutboundMessage.IsMultiTransport"/>),
	/// the context fields (correlation/causation/tenant), <see cref="OutboundMessage.Priority"/>,
	/// <see cref="OutboundMessage.ScheduledAt"/>, the raw <see cref="OutboundMessage.Payload"/>, and the
	/// <see cref="OutboundMessage.Headers"/> bag. A store whose INSERT/SELECT drops any one of these
	/// silently corrupts delivery routing/ordering.
	/// </para>
	/// <para>
	/// NON-VACUOUS: RED against a pre-fix store projection that omits any of these columns (e.g. a SELECT
	/// that never maps <c>GroupKey</c>/<c>PartitionKey</c>/<c>TargetTransports</c>). The reload happens
	/// through the store's real query path (a live DB round-trip for the real-infra derivers), not an
	/// in-memory handle. <c>ScheduledAt</c> is set in the past so the message is delivery-ready and returns
	/// from <c>GetUnsentMessagesAsync</c>. Store-managed fields (Status/SentAt/RetryCount/SequenceNumber/
	/// per-transport deliveries) are intentionally excluded — they are the store's to assign, not the
	/// consumer's to round-trip.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task StageMessage_RoundTripsEveryConsumerSuppliedField_OnReload()
	{
		// ScheduledAt in the PAST → the message is delivery-ready and appears in GetUnsentMessages.
		var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-5);
		var headers = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["x-custom-header"] = "header-value",
		};

		var message = new OutboundMessage(
			"Test.FullFieldRoundTrip",
			"full-field-payload"u8.ToArray(),
			"orders.topic",
			headers)
		{
			CorrelationId = "corr-777",
			CausationId = "cause-888",
			TenantId = "tenant-xyz",
			Priority = 9,
			ScheduledAt = scheduledAt,
			PartitionKey = "partition-A",
			GroupKey = "group-B",
			TargetTransports = "kafka,rabbitmq",
			IsMultiTransport = true,
		};

		// Act
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var unsent = await Store.GetUnsentMessagesAsync(50, CancellationToken.None).ConfigureAwait(false);
		var reloaded = unsent.FirstOrDefault(m => m.Id == message.Id);

		// Assert — every consumer-supplied field survived the reload.
		_ = reloaded.ShouldNotBeNull("the staged full-field message must be reloadable from the store");
		reloaded.Id.ShouldBe(message.Id);
		reloaded.MessageType.ShouldBe("Test.FullFieldRoundTrip");
		reloaded.Payload.ShouldBe(message.Payload);
		reloaded.Destination.ShouldBe("orders.topic");
		reloaded.CorrelationId.ShouldBe("corr-777");
		reloaded.CausationId.ShouldBe("cause-888");
		reloaded.TenantId.ShouldBe("tenant-xyz");
		reloaded.Priority.ShouldBe(9);
		reloaded.PartitionKey.ShouldBe("partition-A");
		reloaded.GroupKey.ShouldBe("group-B");
		reloaded.TargetTransports.ShouldBe("kafka,rabbitmq");
		reloaded.IsMultiTransport.ShouldBeTrue();
		_ = reloaded.ScheduledAt.ShouldNotBeNull("ScheduledAt must round-trip");
		reloaded.ScheduledAt!.Value.ShouldBe(scheduledAt, TimeSpan.FromSeconds(1));
		reloaded.Headers.ShouldContainKey("x-custom-header");
		reloaded.Headers["x-custom-header"].ToString().ShouldBe("header-value");
	}

	#endregion Full-Field Round-Trip Conformance (n20aqx)

	#region GetUnsentMessages Tests

	[Fact]
	public async Task GetUnsentMessages_EmptyStore_ReturnsEmpty()
	{
		// Act
		var messages = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);

		// Assert
		messages.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetUnsentMessages_RespectsBatchSize()
	{
		// Arrange
		for (int i = 0; i < 10; i++)
		{
			await Store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var messages = await Store.GetUnsentMessagesAsync(3, CancellationToken.None).ConfigureAwait(false);

		// Assert
		messages.Count().ShouldBe(3);
	}

	[Fact]
	public async Task GetUnsentMessages_WithInvalidBatchSize_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await Store.GetUnsentMessagesAsync(0, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task GetUnsentMessages_ExcludesSentMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);

		// Assert
		unsent.ShouldNotContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task GetUnsentMessages_OrdersByCreatedAt()
	{
		// Arrange
		var messages = new List<OutboundMessage>();
		for (int i = 0; i < 3; i++)
		{
			var message = CreateTestMessage();
			messages.Add(message);
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await Task.Delay(10).ConfigureAwait(false); // Small delay to ensure different timestamps
		}

		// Act
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		// Assert — CreatedAt reflects staging order (FIFO on the TIMESTAMP), verified deterministically.
		// GetUnsentMessagesAsync is a CLAIM, not a plain read: SQL Server issues UPDATE…OUTPUT and
		// Postgres/Oracle FOR UPDATE SKIP LOCKED, neither of which guarantees the OUTPUT *row order* matches
		// the ORDER BY used to select the batch. Asserting the claim's iteration order == CreatedAt order is
		// therefore provider-non-deterministic (it only happened to hold for MongoDB's per-doc loop). Instead
		// assert the guaranteed property: every staged message is claimed, and when the claimed set is ordered
		// by CreatedAt it matches the staging order — i.e. CreatedAt is populated monotonically as staged.
		unsentList.Count.ShouldBe(messages.Count, "the batch claim must hand out every staged message");
		var orderedByCreatedAt = unsentList.OrderBy(m => m.CreatedAt).ToList();
		for (int i = 0; i < orderedByCreatedAt.Count; i++)
		{
			orderedByCreatedAt[i].Id.ShouldBe(
				messages[i].Id,
				"messages ordered by CreatedAt must match staging order (CreatedAt is monotonic with staging)");
		}
	}

	/// <summary>
	/// Liskov L2 / atomic-claim contract: <c>GetUnsentMessagesAsync</c> is a CLAIM, not a plain read. Two
	/// concurrent claimers MUST receive DISJOINT message sets — every provider implements this as an atomic
	/// lease/lock (InMemory <c>_claimLock</c>+leases, SQL Server <c>UPDATE…OUTPUT</c> with
	/// <c>READPAST,UPDLOCK,ROWLOCK</c>, MongoDB per-doc <c>FindOneAndUpdate</c>, Postgres/Oracle
	/// <c>FOR UPDATE SKIP LOCKED</c>). Without disjointness two pollers drain — and send — the same message,
	/// the outbox's at-most-once-per-claim guarantee broken.
	/// </summary>
	/// <remarks>
	/// SAFETY = the claimed sets never overlap. LIVENESS = staged, claimable messages ARE actually handed out
	/// (a store that returned empty to everyone would satisfy safety by doing no work and stall the drain).
	/// Both arms are required (<c>testing-patterns §3</c>): a claim that is only "safe" is inert. The
	/// disjointness outcome is deterministic under any scheduling (the atomic claim cannot hand the same row
	/// to both), so the concurrent shape is not timing-dependent.
	/// </remarks>
	[Fact]
	public async Task GetUnsentMessages_ConcurrentClaimers_ReceiveDisjointSets()
	{
		SkipIfPending(nameof(GetUnsentMessages_ConcurrentClaimers_ReceiveDisjointSets));

		// Arrange: 2N claimable messages, two claimers each asking for a full batch of N.
		const int perBatch = 5;
		const int total = perBatch * 2;
		for (var i = 0; i < total; i++)
		{
			await Store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act: two concurrent claimers race for the same eligible rows.
		var claim1Task = Store.GetUnsentMessagesAsync(perBatch, CancellationToken.None).AsTask();
		var claim2Task = Store.GetUnsentMessagesAsync(perBatch, CancellationToken.None).AsTask();
		var results = await Task.WhenAll(claim1Task, claim2Task).ConfigureAwait(false);

		var claim1 = results[0].Select(static m => m.Id).ToList();
		var claim2 = results[1].Select(static m => m.Id).ToList();

		// SAFETY: no message id is claimed by both — the atomic claim gives at-most-once ownership.
		claim1.Intersect(claim2, StringComparer.Ordinal).ShouldBeEmpty(
			"Two concurrent claimers received the same message id(s): the claim is not atomic, so a message "
			+ "can be drained and sent by two pollers at once. Every IOutboxStore claim must lease/lock the "
			+ "rows it returns so concurrent claimers get disjoint sets.");

		// LIVENESS: the claim did real work — staged messages were actually handed out, not withheld from
		// everyone (which would pass the safety arm vacuously and permanently stall the outbox drain).
		var union = claim1.Union(claim2, StringComparer.Ordinal).ToList();
		union.ShouldNotBeEmpty(
			"Neither claimer received any of the 10 staged, claimable messages: the claim returns nothing, "
			+ "which is 'safe' only by doing no work. The outbox would never drain.");

		// Disjointness restated as a count identity — guards against a duplicate that Union silently collapsed.
		union.Count.ShouldBe(
			claim1.Count + claim2.Count,
			"The union of the two claims is smaller than their combined size, so a message was claimed twice "
			+ "(the Intersect assertion above should already have caught it — this pins the invariant from the "
			+ "other direction).");
	}

	#endregion GetUnsentMessages Tests

	#region MarkSent Tests

	[Fact]
	public async Task MarkSent_ValidMessage_UpdatesStatus()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldNotContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task MarkSent_WithNullMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.MarkSentAsync(null!, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkSent_WithEmptyMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.MarkSentAsync(string.Empty, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkSent_NonExistentMessage_ThrowsInvalidOperationException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store.MarkSentAsync("non-existent-id", CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkSent_AlreadySent_ThrowsInvalidOperationException()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false));
	}

	#endregion MarkSent Tests

	#region MarkFailed Tests

	[Fact]
	public async Task MarkFailed_ValidMessage_UpdatesStatusAndError()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act
		await Store.MarkFailedAsync(message.Id, "Connection timeout", 1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var failed = await Admin.GetFailedMessagesAsync(5, null, 10, CancellationToken.None)
			.ConfigureAwait(false);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);

		_ = failedMessage.ShouldNotBeNull();
		failedMessage.LastError.ShouldBe("Connection timeout");
		failedMessage.RetryCount.ShouldBe(1);
	}

	[Fact]
	public async Task MarkFailed_WithNullMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.MarkFailedAsync(null!, "error", 1, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkFailed_WithNullErrorMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await Store.MarkFailedAsync(message.Id, null!, 1, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkFailed_IncrementingRetryCount_TracksRetries()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act - Fail multiple times
		await Store.MarkFailedAsync(message.Id, "Error 1", 1, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error 2", 2, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error 3", 3, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var failed = await Admin.GetFailedMessagesAsync(10, null, 10, CancellationToken.None)
			.ConfigureAwait(false);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);

		_ = failedMessage.ShouldNotBeNull();
		failedMessage.RetryCount.ShouldBe(3);
		failedMessage.LastError.ShouldBe("Error 3");
	}

	#endregion MarkFailed Tests

	#region GetFailedMessages Tests

	[Fact]
	public async Task GetFailedMessages_ReturnsOnlyFailed()
	{
		// Arrange
		var stagedMessage = CreateTestMessage();
		var sentMessage = CreateTestMessage();
		var failedMessage = CreateTestMessage();

		await Store.StageMessageAsync(stagedMessage, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(sentMessage, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(failedMessage, CancellationToken.None).ConfigureAwait(false);

		await Store.MarkSentAsync(sentMessage.Id, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(failedMessage.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act
		var failed = await Admin.GetFailedMessagesAsync(5, null, 10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		failed.Count().ShouldBe(1);
		failed.First().Id.ShouldBe(failedMessage.Id);
	}

	[Fact]
	public async Task GetFailedMessages_RespectsMaxRetries()
	{
		// Arrange
		var lowRetryMessage = CreateTestMessage();
		var highRetryMessage = CreateTestMessage();

		await Store.StageMessageAsync(lowRetryMessage, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(highRetryMessage, CancellationToken.None).ConfigureAwait(false);

		await Store.MarkFailedAsync(lowRetryMessage.Id, "Error", 2, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(highRetryMessage.Id, "Error", 5, CancellationToken.None).ConfigureAwait(false);

		// Act - Get only messages with < 3 retries
		var failed = await Admin.GetFailedMessagesAsync(3, null, 10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		failed.ShouldContain(m => m.Id == lowRetryMessage.Id);
		failed.ShouldNotContain(m => m.Id == highRetryMessage.Id);
	}

	[Fact]
	public async Task GetFailedMessages_RespectsOlderThan()
	{
		SkipIfPending(nameof(GetFailedMessages_RespectsOlderThan));

		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act - Use future timestamp (should exclude all current failures)
		var failed = await Admin.GetFailedMessagesAsync(
			10,
			DateTimeOffset.UtcNow.AddSeconds(-1),
			10,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		failed.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetFailedMessages_RespectsBatchSize()
	{
		// Arrange
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await Store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var failed = await Admin.GetFailedMessagesAsync(10, null, 2, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		failed.Count().ShouldBe(2);
	}

	#endregion GetFailedMessages Tests

	#region GetScheduledMessages Tests

	[Fact]
	public async Task GetScheduledMessages_ReturnsScheduledBeforeTimestamp()
	{
		// Arrange
		var pastScheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddHours(-1));
		var futureScheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddHours(2));

		await Store.StageMessageAsync(pastScheduled, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(futureScheduled, CancellationToken.None).ConfigureAwait(false);

		// Act - Get messages scheduled before now + 1 hour
		var scheduled = await Admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			10,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		scheduled.ShouldContain(m => m.Id == pastScheduled.Id);
		scheduled.ShouldNotContain(m => m.Id == futureScheduled.Id);
	}

	[Fact]
	public async Task GetScheduledMessages_RespectsBatchSize()
	{
		// Arrange
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-10 + i));
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var scheduled = await Admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			2,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		scheduled.Count().ShouldBe(2);
	}

	#endregion GetScheduledMessages Tests

	#region CleanupSentMessages Tests

	[Fact]
	public async Task CleanupSentMessages_RemovesOldSentMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act - Cleanup messages older than 0 seconds (all sent messages)
		var removed = await Admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert — LIVENESS (tracking store retained the sent row → cleanup removes it, >=1) vs SAFETY
		// (delete-on-sent store already removed it at mark-sent → cleanup is a no-op, 0). y1moc0-gated.
		if (SupportsSentTracking)
		{
			removed.ShouldBeGreaterThanOrEqualTo(1, "a tracking store retains sent rows for cleanup to remove");
		}
		else
		{
			removed.ShouldBe(0, "a delete-on-sent store has no retained sent rows left to clean up");
		}
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesRecentMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act - Cleanup only messages older than 1 hour ago (should preserve current)
		var removed = await Admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(-1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesPendingMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act - Cleanup all
		var removed = await Admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(0);

		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesFailedMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act - Cleanup all
		var removed = await Admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(0);

		var failed = await Admin.GetFailedMessagesAsync(5, null, 10, CancellationToken.None)
			.ConfigureAwait(false);
		failed.ShouldContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task CleanupSentMessages_RespectsBatchSize()
	{
		SkipIfPending(nameof(CleanupSentMessages_RespectsBatchSize));

		// Arrange
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Cleanup with batch size of 2
		var removed = await Admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1),
			2,
			CancellationToken.None).ConfigureAwait(false);

		// Assert — LIVENESS (tracking store retained 5 sent rows → cleanup honours the batch size of 2) vs
		// SAFETY (delete-on-sent store retained none → nothing to remove, 0). y1moc0-gated, both arms bind.
		removed.ShouldBe(
			SupportsSentTracking ? 2 : 0,
			"a tracking store cleans up to the batch size; a delete-on-sent store has nothing to clean up");
	}

	#endregion CleanupSentMessages Tests

	#region GetStatistics Tests

	[Fact]
	public async Task GetStatistics_EmptyStore_ReturnsZeroCounts()
	{
		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.TotalMessageCount.ShouldBe(0);
		stats.StagedMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(0);
		stats.FailedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatistics_TracksStagedMessages()
	{
		// Arrange
		for (int i = 0; i < 3; i++)
		{
			await Store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.StagedMessageCount.ShouldBe(3);
	}

	[Fact]
	public async Task GetStatistics_TracksSentMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — LIVENESS (tracking store retains the sent row → count 1) vs SAFETY (delete-on-sent store
		// removed it at mark-sent → count 0). y1moc0 capability-gated, both arms non-vacuous.
		stats.SentMessageCount.ShouldBe(
			SupportsSentTracking ? 1 : 0,
			"a tracking store counts the sent message; a delete-on-sent store already removed it");
		stats.StagedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatistics_TracksFailedMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.FailedMessageCount.ShouldBe(1);
	}

	[Fact]
	public async Task GetStatistics_TracksAllStates()
	{
		SkipIfPending(nameof(GetStatistics_TracksAllStates));

		// Arrange
		var staged = CreateTestMessage();
		var sent = CreateTestMessage();
		var failed = CreateTestMessage();

		await Store.StageMessageAsync(staged, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(sent, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(failed, CancellationToken.None).ConfigureAwait(false);

		await Store.MarkSentAsync(sent.Id, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(failed.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — the sent row is retained by a tracking store (Total 3 / Sent 1) but already removed by a
		// delete-on-sent store (Total 2 = staged+failed / Sent 0). Staged + Failed are unaffected by the
		// sent-retention policy. y1moc0 capability-gated, both arms non-vacuous.
		stats.TotalMessageCount.ShouldBe(SupportsSentTracking ? 3 : 2);
		stats.StagedMessageCount.ShouldBe(1);
		stats.SentMessageCount.ShouldBe(SupportsSentTracking ? 1 : 0);
		stats.FailedMessageCount.ShouldBe(1);
	}

	#endregion GetStatistics Tests

	#region Concurrency Tests

	[Fact]
	public async Task ConcurrentMarkSent_OnlyOneSucceeds()
	{
		SkipIfPending(nameof(ConcurrentMarkSent_OnlyOneSucceeds));

		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		const int concurrentAttempts = 10;
		var tasks = new List<Task<bool>>();

		// Act - Try to mark sent concurrently
		for (int i = 0; i < concurrentAttempts; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
					return true;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Exactly one should succeed
		var successCount = results.Count(r => r);
		successCount.ShouldBe(1, "Only one concurrent MarkSent should succeed");
	}

	[Fact]
	public async Task ConcurrentStagingDifferentMessages_AllSucceed()
	{
		// Arrange
		const int concurrentMessages = 20;
		var messages = Enumerable.Range(0, concurrentMessages)
			.Select(_ => CreateTestMessage())
			.ToList();

		// Act
		var tasks = messages.Select(m =>
			Store.StageMessageAsync(m, CancellationToken.None).AsTask());
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.StagedMessageCount.ShouldBe(concurrentMessages);
	}

	[Fact]
	public async Task ConcurrentMixedOperations_MaintainsConsistency()
	{
		// Arrange
		const int messageCount = 10;
		var messages = Enumerable.Range(0, messageCount)
			.Select(_ => CreateTestMessage())
			.ToList();

		// Stage all first
		foreach (var message in messages)
		{
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Concurrently mark some sent and some failed
		var tasks = new List<Task>();
		for (int i = 0; i < messageCount; i++)
		{
			var idx = i;
			if (idx % 2 == 0)
			{
				tasks.Add(Store.MarkSentAsync(messages[idx].Id, CancellationToken.None).AsTask());
			}
			else
			{
				tasks.Add(Store.MarkFailedAsync(messages[idx].Id, "Error", 1, CancellationToken.None).AsTask());
			}
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert — half were marked sent, half failed. A tracking store still counts the sent half; a
		// delete-on-sent store removed them at mark-sent (Sent 0). Failed + Staged are unaffected. y1moc0-gated.
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.StagedMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(SupportsSentTracking ? messageCount / 2 : 0);
		stats.FailedMessageCount.ShouldBe(messageCount / 2);
	}

	#endregion Concurrency Tests

	#region Leadership Fencing Conformance

	/// <summary>
	/// UNIVERSAL leadership-fencing conformance, inherited by every outbox-store conformance subclass. When the
	/// store-under-test advertises <see cref="IFencedOutboxStore"/>, these arms verify the CAS monotonicity
	/// contract against the store's REAL claim/mark path; when it does not advertise the capability, the arms
	/// self-skip with a documented reason (a non-fencing store structurally cannot receive a token — the
	/// property does not apply, it is not silently vacuous-passing).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The two arms are paired (safety + liveness). A store that <i>rejected everything</i> would satisfy the
	/// safety arm alone; the liveness arm is what fails against it. A store that <i>accepted any token</i>
	/// (never fenced) would satisfy the liveness arm alone; the safety arm is what fails against it.
	/// </para>
	/// <para>
	/// <b>NON-VACUOUS.</b> The safety arm is RED against a store that does not fail closed on a stale token
	/// (the <see cref="StaleOutboxFencingTokenException"/> never throws), and RED against a store that throws
	/// on the set-based stale claim instead of yielding empty. The liveness arm is RED against a
	/// reject-everything store (a valid token cannot claim the staged message, or a valid mark-sent throws).
	/// Each conformance subclass runs these against its own store — the in-process InMemory store runs them
	/// non-skipped as a unit test; the real-infra providers (SqlServer / Postgres / Mongo / Oracle) run them
	/// against a live container round-trip. Providers that do not implement fencing (Redis / Elasticsearch /
	/// Marten and the cloud-native family) legitimately self-skip.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Fencing_MarkSentWithStaleToken_IsRejectedFailClosed()
	{
		Assert.SkipWhen(
			Store is not IFencedOutboxStore,
			"Provider does not advertise IFencedOutboxStore — leadership fencing is not applicable to this store.");

		var fenced = (IFencedOutboxStore)Store;

		var a = CreateTestMessage();
		var b = CreateTestMessage();
		await Store.StageMessageAsync(a, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(b, CancellationToken.None).ConfigureAwait(false);

		// A valid leadership tenure marks a message sent and advances the fencing high-water mark to 10.
		await fenced.MarkSentAsync(a.Id, 10, CancellationToken.None).ConfigureAwait(false);

		// SAFETY: a superseded leader presenting a LOWER token must be fenced off fail-closed —
		// the mutation must NOT be silently applied.
		var ex = await Should.ThrowAsync<StaleOutboxFencingTokenException>(
			async () => await fenced.MarkSentAsync(b.Id, 5, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		ex.PresentedToken.ShouldBe(5, "the exception must report the stale token that was presented");
		// The HighWaterToken diagnostic is asserted in Fencing_RejectionException_ReportsHighWaterToken
		// (isolated so a pending-gttg9d provider gap never skips the fence-SAFETY assertions below).

		// SAFETY (set-based claim): a stale token yields zero claimable rows and MUST NOT throw
		// (the IFencedOutboxStore contract: the claim is a set operation, not a fail-closed mutation).
		var stale = await fenced.GetUnsentMessagesAsync(10, 5, CancellationToken.None).ConfigureAwait(false);
		stale.ShouldBeEmpty(
			"a stale fencing token must yield zero claimable rows on the set-based claim, not throw");

		// SAFETY (the mutation must NOT be applied): a fenced-off MarkSent must reject WITHOUT deleting the
		// row. The pre-f5zutu impl checked the fence, then issued an UNGUARDED delete as a separate round-trip
		// (a TOCTOU seam); the fail-closed guarantee is meaningless unless the row genuinely survives the
		// rejection. Asserting the throw alone (above) is the exact gap that let the non-atomic delete pass —
		// the fence-check happening to throw here does not, by itself, prove the delete was never issued.
		var survivors = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		survivors.ShouldContain(
			m => m.Id == b.Id,
			"a stale-token MarkSent must reject the mutation fail-closed — the row MUST survive, not be deleted");
	}

	/// <summary>
	/// gttg9d — the fence-rejection exception must report the <c>HighWaterToken</c> it was fenced against
	/// (part of the <see cref="IFencedOutboxStore"/> diagnostic contract; Postgres populates it).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The fence SAFETY (a superseded leader is rejected fail-closed, the message is not lost/double-mutated)
	/// is covered by <see cref="Fencing_MarkSentWithStaleToken_IsRejectedFailClosed"/> and
	/// <see cref="Fencing_SupersededLeaderCannotMutateOrLoseMessage_AfterHandover"/>, which stay GREEN for all
	/// fencing providers. This arm isolates ONLY the diagnostic <c>HighWaterToken</c> field, so the guarantee-path
	/// safety coverage is never skipped to carry a diagnostic gap.
	/// </para>
	/// <para>
	/// Providers that populate the field (Postgres) assert it strictly here. Providers that do not yet populate
	/// it (SqlServer, Mongo — tracked pending <c>gttg9d</c>) self-skip on the null; this arm then
	/// AUTO-RED-RETURNS the instant the provider populates the field — a real forward regression guard for the
	/// carried fix, not a dropped assertion. Remove the pending <see cref="Xunit.Assert.SkipWhen"/> when
	/// <c>gttg9d</c> lands.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Fencing_RejectionException_ReportsHighWaterToken()
	{
		Assert.SkipWhen(
			Store is not IFencedOutboxStore,
			"Provider does not advertise IFencedOutboxStore — leadership fencing is not applicable to this store.");

		var fenced = (IFencedOutboxStore)Store;

		var a = CreateTestMessage();
		var b = CreateTestMessage();
		await Store.StageMessageAsync(a, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(b, CancellationToken.None).ConfigureAwait(false);

		// A valid leadership tenure advances the fencing high-water mark to 10.
		await fenced.MarkSentAsync(a.Id, 10, CancellationToken.None).ConfigureAwait(false);

		// A superseded leader presenting a LOWER token (5) is fenced off fail-closed.
		var ex = await Should.ThrowAsync<StaleOutboxFencingTokenException>(
			async () => await fenced.MarkSentAsync(b.Id, 5, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		// Every provider that advertises IFencedOutboxStore must report the high-water the stale token was
		// fenced against (Postgres, SqlServer, Mongo). This strict diagnostic arm is isolated from the
		// fence-SAFETY arms so a diagnostic regression can never mask a safety failure (see gttg9d — the
		// masked-diagnostic that hid a real claim-semantics bug is exactly why this is a separate arm).
		ex.HighWaterToken.ShouldBe(10, "the exception must report the high-water it was fenced against");
	}

	/// <summary>
	/// B2 (S894 REVIEW_CODE) — the leadership-fencing high-water MUST survive <c>CleanupSentMessages</c>. A store
	/// that derives the high-water from a column on the SENT message rows (which cleanup deletes) silently loses
	/// the fence after a routine cleanup: <c>MAX(FencingToken)→NULL→0</c>, so a paused superseded leader's stale
	/// token then passes (<c>5 >= 0</c>) and re-mutates — a fencing/exactly-once break. The high-water MUST be
	/// durable independently of the message rows (SqlServer: a fence-control row; MongoDB: the <c>::fence</c>
	/// control doc).
	/// </summary>
	/// <remarks>
	/// <b>NON-VACUOUS.</b> RED against a store whose fence high-water lives only on the message rows — after
	/// cleanup the stale token is accepted (no throw). GREEN against a durable-fence store — the stale token is
	/// still rejected because the high-water outlives the deleted rows. This arm is the forward regression guard
	/// for the B2 durable-fence fix; it applies to every provider that advertises <see cref="IFencedOutboxStore"/>
	/// (non-fencing stores self-skip).
	/// </remarks>
	[Fact]
	public async Task Fencing_HighWaterSurvivesCleanup()
	{
		SkipIfPending(nameof(Fencing_HighWaterSurvivesCleanup));

		Assert.SkipWhen(
			Store is not IFencedOutboxStore,
			"Provider does not advertise IFencedOutboxStore — leadership fencing is not applicable to this store.");

		var fenced = (IFencedOutboxStore)Store;

		// A valid leadership tenure marks a message sent and advances the fencing high-water mark to 10.
		var a = CreateTestMessage();
		await Store.StageMessageAsync(a, CancellationToken.None).ConfigureAwait(false);
		await fenced.MarkSentAsync(a.Id, 10, CancellationToken.None).ConfigureAwait(false);

		// Routine cleanup removes the sent (token-bearing) rows. The recorded high-water MUST NOT be forgotten
		// with them — a row-derived high-water collapses to 0 here (the B2 defect).
		_ = await Admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1), 100, CancellationToken.None).ConfigureAwait(false);

		var b = CreateTestMessage();
		await Store.StageMessageAsync(b, CancellationToken.None).ConfigureAwait(false);

		// SAFETY: a superseded leader presenting a LOWER token (5) AFTER cleanup MUST still be fenced off —
		// the durable high-water (10) outlives the deleted rows. RED if the high-water was derived from them.
		var ex = await Should.ThrowAsync<StaleOutboxFencingTokenException>(
			async () => await fenced.MarkSentAsync(b.Id, 5, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
		ex.PresentedToken.ShouldBe(5, "the stale token presented after cleanup must still be reported");

		// SAFETY (set-based claim): the stale token yields zero claimable rows post-cleanup — never a fresh claim.
		var stale = await fenced.GetUnsentMessagesAsync(10, 5, CancellationToken.None).ConfigureAwait(false);
		stale.ShouldBeEmpty(
			"a stale fencing token must claim zero rows even after the token-bearing sent rows were cleaned up");

		// LIVENESS: the current leader (token >= the durable high-water) still works after cleanup — the fence
		// is not stuck/unusable, it is merely durable.
		await Should.NotThrowAsync(
			async () => await fenced.MarkSentAsync(b.Id, 10, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Fencing_ValidMonotonicToken_ClaimsAndDrains()
	{
		Assert.SkipWhen(
			Store is not IFencedOutboxStore,
			"Provider does not advertise IFencedOutboxStore — leadership fencing is not applicable to this store.");

		var fenced = (IFencedOutboxStore)Store;

		var a = CreateTestMessage();
		await Store.StageMessageAsync(a, CancellationToken.None).ConfigureAwait(false);

		// LIVENESS: a valid (monotonic) leadership token CLAIMS the staged message — proving the store is
		// not merely "reject-everything". The claim atomically advances the high-water mark to 100.
		var claimed = await fenced.GetUnsentMessagesAsync(50, 100, CancellationToken.None).ConfigureAwait(false);
		claimed.ShouldContain(
			m => m.Id == a.Id,
			"a valid fencing token must be able to claim the staged message (liveness, not reject-everything)");

		// LIVENESS: a valid mark-sent under the same monotonic token SUCCEEDS (an equal token is the same
		// tenure — the guard is strictly-less-than, so an equal token is honored, never rejected).
		await Should.NotThrowAsync(
			async () => await fenced.MarkSentAsync(a.Id, 100, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		// The message drained: a marked-sent message no longer appears in the unsent set.
		var remaining = await Store.GetUnsentMessagesAsync(50, CancellationToken.None).ConfigureAwait(false);
		remaining.ShouldNotContain(
			m => m.Id == a.Id,
			"a message marked sent under a valid fencing token must drain from the unsent set");
	}

	/// <summary>
	/// f5zutu — leadership-handover ATOMICITY outcome: once a fresher leader has superseded (advanced the
	/// high-water), a superseded leader can neither mutate (mark-sent) nor claim the message, and the message
	/// is neither lost nor double-deliverable — it remains claimable by the current leader.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This locks the OUTCOME the atomic fence-CAS guarantees. The <see cref="IFencedOutboxStore"/> contract
	/// requires the fence-check and the mutation to be a SINGLE atomic compare-and-swap; a two-round-trip
	/// implementation (fence-check, then a separate unguarded mutation) opens a TOCTOU window in which a
	/// superseded leader still completes its mutation after a fresher leader has advanced the high-water —
	/// a lost-update / double-delivery on the exactly-once invariant.
	/// </para>
	/// <para>
	/// <b>NON-VACUITY.</b> A deterministic in-process assertion cannot itself reproduce the concurrent
	/// check-&gt;act race (the atomic fix makes the interleave structurally unrepresentable — a concurrent
	/// harness that forces the gap blocks on the fence-row lock on the fixed tree). The non-vacuity of the
	/// atomicity guarantee is proven separately by the real-infra FOR-UPDATE row-barrier interleave harness
	/// run against the pre-fix tree (RED: a superseded leader deletes the row). This permanent conformance
	/// arm is the forward correctness guard: it fails RED against any future regression that lets a
	/// superseded leader mutate (mark deletes the row) or that loses the message on handover.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Fencing_SupersededLeaderCannotMutateOrLoseMessage_AfterHandover()
	{
		Assert.SkipWhen(
			Store is not IFencedOutboxStore,
			"Provider does not advertise IFencedOutboxStore — leadership fencing is not applicable to this store.");

		var fenced = (IFencedOutboxStore)Store;

		var m = CreateTestMessage();
		var decoy = CreateTestMessage();
		await Store.StageMessageAsync(m, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(decoy, CancellationToken.None).ConfigureAwait(false);

		// A fresher leader (token 6) supersedes — it marks an UNRELATED decoy sent, which advances the shared
		// high-water to 6 without touching (or reserving) m. m stays staged + unreserved so the not-lost check
		// below binds the row's existence, not a claim state.
		await fenced.MarkSentAsync(decoy.Id, 6, CancellationToken.None).ConfigureAwait(false);

		// SAFETY (mark): the superseded leader (token 5) MUST be refused fail-closed AND MUST NOT delete the
		// message — the mutation is not applied.
		var ex = await Should.ThrowAsync<StaleOutboxFencingTokenException>(
			async () => await fenced.MarkSentAsync(m.Id, 5, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
		ex.PresentedToken.ShouldBe(5);
		// The HighWaterToken diagnostic is asserted in Fencing_RejectionException_ReportsHighWaterToken
		// (isolated so a pending-gttg9d provider gap never skips the fence-SAFETY assertions below).

		// SAFETY (claim): the superseded leader's set-based claim yields zero rows (never double-claims).
		var staleClaim = await fenced.GetUnsentMessagesAsync(50, 5, CancellationToken.None).ConfigureAwait(false);
		staleClaim.ShouldBeEmpty("a superseded leader (token 5) must claim zero rows after the high-water advanced to 6");

		// LIVENESS (not lost): the message survives the superseded leader's refused mutation and remains
		// claimable/deliverable by the current leader — the fail-closed rejection must not have deleted it.
		var stillThere = await Store.GetUnsentMessagesAsync(50, CancellationToken.None).ConfigureAwait(false);
		stillThere.ShouldContain(
			m2 => m2.Id == m.Id,
			"the message must NOT be lost — a superseded leader's refused mark must never delete it");

		// LIVENESS (drains under the current leader): the fresher leader (token 6) marks it sent and it drains.
		await Should.NotThrowAsync(
			async () => await fenced.MarkSentAsync(m.Id, 6, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
		var drained = await Store.GetUnsentMessagesAsync(50, CancellationToken.None).ConfigureAwait(false);
		drained.ShouldNotContain(m2 => m2.Id == m.Id, "the message drains once the current leader marks it sent");
	}

	#endregion Leadership Fencing Conformance

	#region Failure-Anchored Re-claim Floor Conformance (lz7us9)

	/// <summary>
	/// Override in providers implementing the lz7us9 canonical <c>MarkFailedAsync</c> re-claim contract (InMemory /
	/// Postgres / Oracle / SqlServer) to build a store configured with the given failure-anchored floor
	/// (<c>FailureBackoffFloorSeconds</c>, seconds).
	/// <para>
	/// The default returns <see langword="null"/>, which is NOT an opt-out: a provider that returns
	/// <see langword="null"/> must DECLARE every re-claim-floor arm in <see cref="PendingConformanceGaps"/> with a
	/// tracking id, or the arms FAIL (see <c>RequireReclaimFloorStoreAsync</c>). Silence is not a pass — an
	/// un-overridden default that self-skips is indistinguishable in the suite output from a provider that
	/// satisfies the contract, and every newly-added provider would inherit that silence for free.
	/// </para>
	/// </summary>
	/// <param name="floorSeconds">The failure-anchored re-claim floor, in seconds, for the returned store.</param>
	/// <returns>A store configured with the floor, or <see langword="null"/> when the contract does not apply.</returns>
	protected virtual Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds) =>
		Task.FromResult<IOutboxStore?>(null);

	/// <summary>
	/// Override in real-infra providers to RESERVE the given (already-staged) message under a FOREIGN dispatcher
	/// identity — one distinct from the store's own static per-process dispatcher id — via the provider's public
	/// explicit-dispatcher-id reserve API. Returns <see langword="true"/> if it reserved the message. The default
	/// returns <see langword="false"/> so a store without an explicit-dispatcher reserve surface (InMemory) self-
	/// skips the R2 stolen-lease arm. This gives the row a DIFFERENT owner than the caller of
	/// <c>IOutboxStore.MarkFailedAsync</c> (whose dispatcher id is static per process), which is the only way to
	/// actually exercise the R2 ownership guard — two in-process store instances share the static dispatcher id and
	/// so cannot (verified: <c>PostgresOutboxStore.DispatcherId</c> is static; SA 34426).
	/// </summary>
	protected virtual Task<bool> TryReserveMessageUnderForeignDispatcherAsync(IOutboxStore store, string messageId) =>
		Task.FromResult(false);

	/// <summary>
	/// Test names for which THIS provider has a KNOWN, TRACKED, documented-pending conformance gap. The listed
	/// behavior is REQUIRED by the <see cref="IOutboxStore"/> contract (NOT a capability difference); its fix is
	/// scheduled and referenced by the skip reason's tracking id. A provider that does NOT list a test still RUNS
	/// it and MUST pass — so declaring a gap here never masks it for any conformant provider. Overridden
	/// per-provider; empty by default, holding every provider to the full contract unless it explicitly declares
	/// a pending gap.
	/// </summary>
	protected virtual IReadOnlyDictionary<string, string> PendingConformanceGaps =>
		new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Honest documented-pending skip: skips <paramref name="testName"/> ONLY for a provider that declares it in
	/// <see cref="PendingConformanceGaps"/>, citing THAT provider's own tracking id for the gap. Every
	/// non-declaring provider runs the test, so the guaranteed behaviour stays covered where it is implemented.
	/// </summary>
	/// <param name="testName">The conformance test's own name (pass <c>nameof(...)</c>).</param>
	private void SkipIfPending(string testName)
	{
		if (PendingConformanceGaps.TryGetValue(testName, out var trackingId))
		{
			Assert.Skip(
				$"pending {trackingId} — {GetType().Name} has a tracked, documented-pending conformance gap for " +
				$"'{testName}' (required contract, fix scheduled next sprint; NOT a capability-gate).");
		}
	}

	/// <summary>
	/// The seven failure-anchored re-claim floor arms. Kept as an explicit roster so the liveness arm below can
	/// count how many of them THIS provider actually executes — a count the suite's own green/red output cannot
	/// express.
	/// </summary>
	private static readonly string[] ReclaimFloorArmNames =
	[
		nameof(MarkFailed_NotReclaimableWithinTheFloor_ReservedPath),
		nameof(MarkFailed_NotReclaimableWithinTheFloor_UnreservedInputPath),
		nameof(MarkFailed_EventuallyReclaimableAfterTheFloorElapses),
		nameof(MarkFailed_OwnedPath_RecordsFailureAndReclaimsAfterTheFloor),
		nameof(MarkFailed_DoesNotDecreaseRetryCount_OnAStaleLateReport),
		nameof(DeadLettered_NeverReclaimed_ByEitherClaimPath),
		nameof(MarkFailed_ByANonOwningDispatcher_DoesNotStealTheLease_R2),
	];

	/// <summary>
	/// SAFETY gate for the re-claim floor arms: returns a floor-configured store, or terminates this arm in a way
	/// that is VISIBLE in the suite output. A provider that DECLARES the gap in <see cref="PendingConformanceGaps"/>
	/// skips honestly, naming itself and its tracking id; a provider that declares nothing and implements nothing
	/// FAILS.
	/// <para>
	/// Previously each arm did <c>Assert.SkipWhen(store is null, ...)</c> against an un-overridden default that
	/// returns <see langword="null"/>. That is a SILENT skip: nothing in the output distinguishes "this provider
	/// satisfies the re-claim-floor contract" from "this provider was never asked", and every newly-added provider
	/// inherits the silence for free. An un-asked contract that reads as a pass is precisely the inert-control
	/// class this suite exists to detect.
	/// </para>
	/// </summary>
	/// <param name="testName">The calling conformance arm's own name (pass <c>nameof(...)</c>).</param>
	/// <param name="floorSeconds">The failure-anchored re-claim floor, in seconds.</param>
	/// <returns>A store configured with the floor. Never <see langword="null"/> on return.</returns>
	private async Task<IOutboxStore> RequireReclaimFloorStoreAsync(string testName, int floorSeconds)
	{
		// Honest, declared, tracked gap -> visible skip citing this provider's own bead id.
		SkipIfPending(testName);

		var store = await CreateStoreWithReclaimFloorAsync(floorSeconds).ConfigureAwait(false);

		store.ShouldNotBeNull(
			$"{GetType().Name} neither implements the failure-anchored re-claim floor (override "
			+ $"{nameof(CreateStoreWithReclaimFloorAsync)}) nor DECLARES a tracked gap for '{testName}' in "
			+ $"{nameof(PendingConformanceGaps)}. A provider may not silently opt out of a required contract: "
			+ "implement it, or declare the gap with a tracking id so the skip is visible and owned.");

		return store;
	}

	/// <summary>
	/// LIVENESS: proves the re-claim-floor suite actually EXERCISES this provider, rather than passing because
	/// every arm self-skipped. The safety arms above are all of the form "the bad thing does not happen" — each is
	/// satisfied by a store that is never asked anything at all. This arm asserts the executed-arm count is
	/// non-zero, which no amount of skipping can satisfy.
	/// <para>
	/// <b>SELF-CERTIFIED — NOT INDEPENDENTLY LOCKED.</b> This arm was written by the same author as the seam it
	/// verifies, so it does not carry the independence that makes a liveness arm trustworthy on its own: an arm
	/// written for one's own code cannot distinguish "the contract was tested" from "the author's assumptions
	/// about the contract were tested". It IS non-vacuous — a provider mutated to drop its override produces
	/// 8 failures where the pre-fix code reported green with 7 silent skips — but non-vacuity and independence
	/// are different properties and only the first has been demonstrated. A second author re-deriving this arm
	/// from the contract alone (a provider must implement the floor or declare the gap with a tracking id, and
	/// the suite must prove it exercised somebody) retires the caveat; disagreement between the two derivations
	/// is the finding.
	/// </para>
	/// </summary>
	[Fact]
	public async Task ReclaimFloorSuite_ActuallyExercisesThisProvider_Liveness()
	{
		var declaredGaps = ReclaimFloorArmNames.Count(PendingConformanceGaps.ContainsKey);
		var store = await CreateStoreWithReclaimFloorAsync(60).ConfigureAwait(false);

		if (store is null)
		{
			// Opting out is allowed ONLY when fully declared: every arm named, every name tracked.
			declaredGaps.ShouldBe(
				ReclaimFloorArmNames.Length,
				$"{GetType().Name} does not implement the re-claim floor, so it must DECLARE all "
				+ $"{ReclaimFloorArmNames.Length} arms in {nameof(PendingConformanceGaps)} with a tracking id. "
				+ $"It declares {declaredGaps}. The undeclared arms would skip silently — indistinguishable in "
				+ "the suite output from a provider that satisfies the contract.");
			return;
		}

		try
		{
			var executed = ReclaimFloorArmNames.Length - declaredGaps;
			executed.ShouldBeGreaterThan(
				0,
				$"{GetType().Name} builds a floor-configured store yet declares ALL "
				+ $"{ReclaimFloorArmNames.Length} arms as pending gaps, so the re-claim-floor suite exercises it "
				+ "zero times while reporting green. A suite that asks nothing proves nothing.");
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	private static async Task DisposeStoreAsync(IOutboxStore store)
	{
		switch (store)
		{
			case IAsyncDisposable asyncDisposable:
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
				break;
			case IDisposable disposable:
				disposable.Dispose();
				break;
			default:
				break;
		}
	}

	/// <summary>
	/// SAFETY (Lamport R1), reserved path: a claimed-then-failed message is NOT re-claimable within the floor.
	/// Non-vacuous against an immediate-reclaim (no-floor) store — it would re-appear here (the zero-backoff
	/// hot-loop the failure-anchored floor exists to prevent).
	/// </summary>
	[Fact]
	public async Task MarkFailed_NotReclaimableWithinTheFloor_ReservedPath()
	{
		var store = await RequireReclaimFloorStoreAsync(nameof(MarkFailed_NotReclaimableWithinTheFloor_ReservedPath), 60).ConfigureAwait(false);

		try
		{
			var msg = CreateTestMessage();
			await store!.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			// Non-vacuity: a freshly-staged message IS claimable — and this reserves it (sets the lease).
			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldContain(m => m.Id == msg.Id, "a freshly-staged message must be claimable");

			await store.MarkFailedAsync(msg.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldNotContain(m => m.Id == msg.Id,
					"a message failed within the 60s floor must NOT be immediately re-claimable — an immediate "
					+ "re-claim is the zero-backoff hot-loop the failure-anchored floor exists to prevent.");
		}
		finally
		{
			await DisposeStoreAsync(store!).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY (Lamport R1), unreserved-input path — stage then fail WITHOUT ever claiming (the
	/// <c>dispatcher_timeout IS NULL</c> state). Still floored. This is the hole R1 named: a "let the lease expire"
	/// floor gives 0 on this path, so it must be an explicit failure-anchored floor.
	/// </summary>
	[Fact]
	public async Task MarkFailed_NotReclaimableWithinTheFloor_UnreservedInputPath()
	{
		var store = await RequireReclaimFloorStoreAsync(nameof(MarkFailed_NotReclaimableWithinTheFloor_UnreservedInputPath), 60).ConfigureAwait(false);

		try
		{
			var msg = CreateTestMessage();
			await store!.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			// No claim — fail an unreserved message directly (never reserved, so no lease to "expire").
			await store.MarkFailedAsync(msg.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldNotContain(m => m.Id == msg.Id,
					"a message failed on the UNRESERVED-input path (never claimed) must still be floored — a "
					+ "'let the lease expire' floor gives 0 here (no lease was ever set), the exact R1 hole.");
		}
		finally
		{
			await DisposeStoreAsync(store!).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS (Lamport R1 / universal at-least-once): after the floor elapses, the failed (sub-ceiling) message
	/// IS re-claimed — never terminally dropped. Non-vacuous against a terminal (silent-drop) store.
	/// </summary>
	[Fact]
	public async Task MarkFailed_EventuallyReclaimableAfterTheFloorElapses()
	{
		var store = await RequireReclaimFloorStoreAsync(nameof(MarkFailed_EventuallyReclaimableAfterTheFloorElapses), 1).ConfigureAwait(false);

		try
		{
			var msg = CreateTestMessage();
			await store!.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(msg.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			// Bounded poll (no fixed sleep-then-assert): the 1s floor elapses via real time and the message must
			// re-enter the claimable set. Determinism: the floor is a real clock-anchored timestamp; the poll
			// window (15s) is far larger than the 1s floor.
			var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
			var reclaimed = false;
			while (DateTimeOffset.UtcNow < deadline)
			{
				var batch = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
				if (batch.Any(m => m.Id == msg.Id))
				{
					reclaimed = true;
					break;
				}

				await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
			}

			reclaimed.ShouldBeTrue(
				"a failed (sub-ceiling) message must remain eventually re-claimable once the floor elapses "
				+ "(universal at-least-once) — never terminally dropped.");
		}
		finally
		{
			await DisposeStoreAsync(store!).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// OWNED-PATH liveness twin (Lamport Q2 / GUIDE §1.2) — the single-writer schedule the S893 owned-path
	/// no-op hid behind. <b>Claim AS the owner, then <c>MarkFailedAsync</c> AS the owner</b>, and assert BOTH:
	/// <list type="bullet">
	///   <item><b>(a) RECORDING</b> — the failure is actually persisted (retrievable via
	///     <c>GetFailedMessages</c>, <c>RetryCount == GREATEST(prev, n)</c>). A silent no-op that "matched the
	///     owner and did nothing" records nothing → RED.</item>
	///   <item><b>(b) LIVENESS</b> — after the floor elapses the message re-enters the claimable set. A no-op
	///     leaves the row under its ORIGINAL claim lease (which far outlasts the short floor) so it never
	///     re-claims; a store that strands the failed row in a set the claim never reads (Redis/Mongo pre-fix,
	///     GUIDE §1.5) also never re-claims — both → RED.</item>
	/// </list>
	/// This disambiguates "floored" from "did nothing": the sibling reserved-path safety arm asserts only
	/// <c>ShouldNotContain</c>-within-floor, which a no-op satisfies vacuously (the row is still under its
	/// original lease). Determinism: a short floor (1s) advanced by real time, polled within a 15s window
	/// (≫ 1s floor, ≪ the default claim-lease so a no-op cannot re-claim by lease-expiry inside the window).
	/// </summary>
	[Fact]
	public async Task MarkFailed_OwnedPath_RecordsFailureAndReclaimsAfterTheFloor()
	{
		var store = await RequireReclaimFloorStoreAsync(nameof(MarkFailed_OwnedPath_RecordsFailureAndReclaimsAfterTheFloor), 1).ConfigureAwait(false);

		try
		{
			var msg = CreateTestMessage();
			await store!.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			// Claim AS the owner — sets the lease under this store's own dispatcher id (the owned path where
			// S893's bare-owner guard matched fine yet did nothing).
			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldContain(m => m.Id == msg.Id, "a freshly-staged message must be claimable (owned-path setup)");

			// Fail AS the owner (R2 permits the lease owner to mark its own claim failed).
			await store.MarkFailedAsync(msg.Id, "owner-boom", 2, CancellationToken.None).ConfigureAwait(false);

			// (a) RECORDING — RED-detects the S893 owned-path silent no-op (records nothing).
			var failed = await ((IOutboxStoreAdmin)store!)
				.GetFailedMessagesAsync(100, null, 10, CancellationToken.None).ConfigureAwait(false);
			var recorded = failed.FirstOrDefault(m => m.Id == msg.Id);
			_ = recorded.ShouldNotBeNull(
				"the owned-path failure MUST be recorded (retrievable via GetFailedMessages) — a silent no-op that "
				+ "'matched the owner and did nothing' records nothing, the exact S893 owned-path hole.");
			recorded.RetryCount.ShouldBe(2,
				"the recorded attempts must reflect the reported failure (GREATEST(prev, n) = 2) — a no-op leaves it unset.");

			// (b) LIVENESS — after the 1s floor elapses the message re-enters the claimable set. A no-op (row
			// still under its original long lease) or a strand (failed row the claim never reads) never re-claims.
			var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
			var reclaimed = false;
			while (DateTimeOffset.UtcNow < deadline)
			{
				var batch = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
				if (batch.Any(m => m.Id == msg.Id))
				{
					reclaimed = true;
					break;
				}

				await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
			}

			reclaimed.ShouldBeTrue(
				"an owned-path failure must free its lease + anchor the short floor as ONE write, so once the floor "
				+ "elapses the message re-enters the claimable set (at-least-once). A no-op leaves the row under its "
				+ "original long lease; a strand hides it in a set the claim never reads — both never re-claim (RED).");
		}
		finally
		{
			await DisposeStoreAsync(store!).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// R3 — persisted attempts are non-decreasing across a stale late failure report: a lower retry count must not
	/// lower it, or the DLQ-ceiling termination guarantee (driven by store-persisted attempts) weakens. Observed
	/// via the failed-retrieval path (floor-independent).
	/// </summary>
	[Fact]
	public async Task MarkFailed_DoesNotDecreaseRetryCount_OnAStaleLateReport()
	{
		var store = await RequireReclaimFloorStoreAsync(nameof(MarkFailed_DoesNotDecreaseRetryCount_OnAStaleLateReport), 60).ConfigureAwait(false);

		try
		{
			var msg = CreateTestMessage();
			await store!.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			await store.MarkFailedAsync(msg.Id, "attempt-3", 3, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(msg.Id, "stale-1", 1, CancellationToken.None).ConfigureAwait(false);

			var failed = await ((IOutboxStoreAdmin)store!).GetFailedMessagesAsync(100, null, 10, CancellationToken.None).ConfigureAwait(false);
			var reloaded = failed.FirstOrDefault(m => m.Id == msg.Id);
			_ = reloaded.ShouldNotBeNull("the failed message must be retrievable via the failed path");
			reloaded.RetryCount.ShouldBe(3,
				"a stale late MarkFailed with a LOWER retry count must NOT lower the persisted attempts (R3) — the "
				+ "DLQ ceiling is driven by store-persisted attempts, so a decrease would weaken termination.");
		}
		finally
		{
			await DisposeStoreAsync(store!).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS-TERMINATION — a dead-lettered (retry-exhausted) message is terminal: it is NEVER re-claimed by
	/// either the delivery-claim path (<c>GetUnsentMessagesAsync</c>) or the failed-retrieval path
	/// (<c>GetFailedMessagesAsync</c>). Without this, a perpetually-failing message livelocks (re-claimed +
	/// re-dead-lettered forever). Non-vacuous against a store whose claim allow-list does not exclude the terminal
	/// status.
	/// </summary>
	[Fact]
	public async Task DeadLettered_NeverReclaimed_ByEitherClaimPath()
	{
		var store = await RequireReclaimFloorStoreAsync(nameof(DeadLettered_NeverReclaimed_ByEitherClaimPath), 60).ConfigureAwait(false);

		try
		{
			var msg = CreateTestMessage();
			await store!.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			// Non-vacuity: staged message IS claimable before it is dead-lettered.
			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldContain(m => m.Id == msg.Id);

			Assert.SkipWhen(
				store is not IDeadLetterableOutboxStore,
				"Store does not advertise IDeadLetterableOutboxStore — terminal dead-lettering is not applicable.");
			await ((IDeadLetterableOutboxStore)store!)
				.MarkDeadLetteredAsync(msg.Id, "retries exhausted", CancellationToken.None).ConfigureAwait(false);

			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldNotContain(m => m.Id == msg.Id,
					"a dead-lettered (terminal) message must never be re-claimed by the delivery poller — re-claim "
					+ "is the unbounded re-deliver + re-dead-letter livelock terminal status exists to stop.");
			(await ((IOutboxStoreAdmin)store!).GetFailedMessagesAsync(100, null, 10, CancellationToken.None).ConfigureAwait(false))
				.ShouldNotContain(m => m.Id == msg.Id,
					"a dead-lettered message must also be excluded from the failed-retrieval path (it is terminal, "
					+ "not a retryable failure).");
		}
		finally
		{
			await DisposeStoreAsync(store!).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY-GUARD (Lamport R2, reservation-ownership) — a <c>MarkFailedAsync</c> issued by a dispatcher that does
	/// NOT own the reservation is a NO-OP: it must not free (steal) the owner's lease. Otherwise a superseded /
	/// second dispatcher unreserves the owner's in-flight row and both drain + send it (the S2 double-delivery).
	/// Paired with a liveness arm: the legitimate OWNER's <c>MarkFailedAsync</c> is still accepted (the guard blocks
	/// only non-owners, never the owner). Real-infra only — needs two dispatcher identities sharing one backing DB.
	/// </summary>
	/// <remarks>
	/// NON-VACUOUS: RED against a store whose <c>MarkFailedAsync</c> unconditionally clears the lease (the pre-fix
	/// unconditional-unreserve) — the non-owning fail would free the row → it becomes re-claimable → the safety
	/// assertion fails. The liveness arm is RED against a guard that rejects everyone (the owner's fail would be
	/// dropped too).
	/// </remarks>
	[Fact]
	public async Task MarkFailed_ByANonOwningDispatcher_DoesNotStealTheLease_R2()
	{
		// Long floor so the owner's reservation cannot expire during the test — the ONLY way the row could become
		// re-claimable is a non-owning fail wrongly freeing the lease.
		var store = await RequireReclaimFloorStoreAsync(nameof(MarkFailed_ByANonOwningDispatcher_DoesNotStealTheLease_R2), 120).ConfigureAwait(false);

		try
		{
			var msg = CreateTestMessage();
			await store!.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			// Give the row a FOREIGN owner — a dispatcher id distinct from this store's static per-process one.
			// Real-infra only (needs the explicit-dispatcher reserve surface); InMemory self-skips.
			var reservedByForeign = await TryReserveMessageUnderForeignDispatcherAsync(store, msg.Id).ConfigureAwait(false);
			Assert.SkipWhen(
				!reservedByForeign,
				"R2 stolen-lease needs an explicit-dispatcher reserve surface to give the row a foreign owner — real-infra only.");

			// Non-vacuity of the setup: the row is now owned by the FOREIGN dispatcher, so this store's own claim
			// (static dispatcher id, 120s lease unexpired) does NOT see it.
			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldNotContain(m => m.Id == msg.Id, "the foreign-reserved row must not be claimable by this store");

			// SAFETY (R2): this store — a NON-owner of the row — fails it. The ownership guard restricts the mutation
			// to rows this caller owns, so it must be a no-op that does NOT free the foreign lease. It may return
			// silently or reject; the OUTCOME (lease survives) below is the real check.
			try
			{
				await store.MarkFailedAsync(msg.Id, "stolen-lease", 1, CancellationToken.None).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// Rejecting a non-owning mark (rather than silently no-op'ing) also satisfies R2 — the point is the
				// foreign lease is not freed.
			}

			// SAFETY: the message is STILL reserved by the foreign owner — the non-owner's fail did NOT steal it.
			// RED against unconditional-unreserve: the non-owner fail would clear the foreign lease → the row would
			// re-appear here (claimable by this store) → the S2 double-delivery.
			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldNotContain(m => m.Id == msg.Id,
					"a NON-owning dispatcher's MarkFailed must NOT free the owner's reservation — if it does, a "
					+ "second/superseded dispatcher steals the in-flight row and both deliver it (S2 double-delivery).");

			// LIVENESS: the R2 guard blocks only NON-owners. The store's OWN reserve + fail still works — stage a
			// fresh message, reserve it under THIS store's own id, and fail it: it IS recorded.
			var owned = CreateTestMessage();
			await store.StageMessageAsync(owned, CancellationToken.None).ConfigureAwait(false);
			(await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ShouldContain(m => m.Id == owned.Id, "the store reserves its own message under its own dispatcher id");
			await store.MarkFailedAsync(owned.Id, "owner-fail", 1, CancellationToken.None).ConfigureAwait(false);
			(await ((IOutboxStoreAdmin)store).GetFailedMessagesAsync(100, null, 10, CancellationToken.None).ConfigureAwait(false))
				.ShouldContain(m => m.Id == owned.Id,
					"the reservation OWNER must still be able to MarkFailed — R2 guards against non-owners, never the "
					+ "owner, or the store could never record its own failures (dead liveness).");
		}
		finally
		{
			await DisposeStoreAsync(store!).ConfigureAwait(false);
		}
	}

	#endregion Failure-Anchored Re-claim Floor Conformance (lz7us9)
}
