// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-Postgres locks on the property that a delivery failure reported through the backoff path is
/// RECORDED AS A FAILURE, not merely rescheduled.
/// </summary>
/// <remarks>
/// <para>
/// This table is delete-on-sent: a sent row is deleted and a dead-lettered row is moved, so the presence of
/// a row with a non-null <c>error_message</c> is what "failed but still retryable" means here. Both the
/// failed-message query and the failed statistic select on exactly that column.
/// </para>
/// <para>
/// The backoff write set the next-attempt schedule and the attempt count but never the error, so a
/// sub-ceiling failure reported through it was re-claimable — delivery was never at risk — yet appeared in
/// no failed-message query and in no failed count. Because the processor PREFERS this path wherever the
/// store advertises the backoff capability, that was the path production takes: an operator asking why a
/// message had not arrived saw no failure recorded anywhere until it dead-lettered.
/// </para>
/// <para>
/// Deterministic by construction. Nothing here waits for a schedule to elapse: the failure must be visible
/// the moment the mark returns, so there is no clock on either side of the assertion. The third arm is the
/// liveness half — a message that has NOT failed must stay out of both surfaces, or a query returning
/// everything would satisfy the first two.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxBackoffRecordsFailureShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const int MaxRetries = 10;

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="PostgresOutboxBackoffRecordsFailureShould"/> class.</summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresOutboxBackoffRecordsFailureShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ListTheMessageAmongFailedMessages_WhenItFailedThroughTheBackoffPath()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, schedulable) = await CreateStoreAsync().ConfigureAwait(false);

		var message = await StageAndClaimAsync(store, ct).ConfigureAwait(false);
		await schedulable.MarkFailedWithBackoffAsync(
			message.Id, "transport refused the message", 1, DateTimeOffset.UtcNow.AddSeconds(30), ct).ConfigureAwait(false);

		var failed = (await store.GetAllTenantsFailedMessagesAsync(MaxRetries, olderThan: null, batchSize: 50, ct)
			.ConfigureAwait(false)).ToList();

		failed.ShouldContain(
			m => m.Id == message.Id,
			"a delivery failure reported through the backoff path must be retrievable as a failure. This is the "
			+ "path the processor prefers wherever the store advertises the backoff capability, so a failure "
			+ "absent from here is absent from everywhere an operator would look.");
	}

	[Fact]
	public async Task CountTheMessageAmongFailedStatistics_WhenItFailedThroughTheBackoffPath()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, schedulable) = await CreateStoreAsync().ConfigureAwait(false);

		var message = await StageAndClaimAsync(store, ct).ConfigureAwait(false);
		await schedulable.MarkFailedWithBackoffAsync(
			message.Id, "transport refused the message", 1, DateTimeOffset.UtcNow.AddSeconds(30), ct).ConfigureAwait(false);

		var statistics = await store.GetAllTenantsStatisticsAsync(ct).ConfigureAwait(false);

		statistics.FailedMessageCount.ShouldBe(
			1,
			"the failed statistic counts rows carrying an error, so a backoff write that records no error "
			+ "leaves a real delivery failure invisible to every health surface built on it.");
	}

	[Fact]
	public async Task LeaveAMessageThatHasNotFailed_OutOfBothSurfaces()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, _) = await CreateStoreAsync().ConfigureAwait(false);

		var message = await StageAndClaimAsync(store, ct).ConfigureAwait(false);

		var failed = (await store.GetAllTenantsFailedMessagesAsync(MaxRetries, olderThan: null, batchSize: 50, ct)
			.ConfigureAwait(false)).ToList();
		failed.ShouldNotContain(
			m => m.Id == message.Id,
			"a claimed, not-yet-failed message is not a failure — without this the arms above would pass "
			+ "against a query that returned every row.");

		var statistics = await store.GetAllTenantsStatisticsAsync(ct).ConfigureAwait(false);
		statistics.FailedMessageCount.ShouldBe(0, "nothing has failed yet");
	}

	private static async Task<OutboundMessage> StageAndClaimAsync(PostgresOutboxStore store, CancellationToken ct)
	{
		var message = new OutboundMessage("Test.MessageType", "test-payload"u8.ToArray(), "test-queue")
		{
			Id = Guid.NewGuid().ToString(),
		};

		await store.StageMessageAsync(message, ct).ConfigureAwait(false);

		// Claimed first, so the failure is reported against a reservation this caller holds — the same shape
		// the drain produces, and the shape the ownership guard on the write requires.
		var claimed = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		claimed.ShouldContain(m => m.Id == message.Id, "the staged message must be claimable before it can fail");

		return message;
	}

	private async Task<(PostgresOutboxStore Store, IBackoffSchedulableOutboxStore Schedulable)> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - this failure-visibility lock is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = MaxRetries,
			FailureBackoffFloorSeconds = 1,
		};

		var store = new PostgresOutboxStore(db, Options.Create(options), NullLogger<PostgresOutboxStore>.Instance);

		// Non-vacuity: the processor only takes the path under test where the store ADVERTISES the capability.
		// If Postgres ever stopped advertising it these arms would be exercising a path production never runs.
		var schedulable = store as IBackoffSchedulableOutboxStore;
		schedulable.ShouldNotBeNull(
			"this provider must advertise the backoff capability, otherwise the processor would never take the "
			+ "path under test and these locks would be vacuous.");

		return (store, schedulable);
	}
}
