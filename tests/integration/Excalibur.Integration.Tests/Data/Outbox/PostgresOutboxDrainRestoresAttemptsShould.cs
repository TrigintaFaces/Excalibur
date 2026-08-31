// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-Postgres lock: the drain reload MUST restore a message's persisted attempt count, so a message
/// that has already failed is handed to the drain carrying its history rather than looking untried.
/// </summary>
/// <remarks>
/// <para>
/// The reload builds an <see cref="OutboundMessage"/> from the reserved row. Its constructor defaults the
/// count to zero, so a reload that copies forward every field except this one produces a message whose
/// attempt history has silently reset. The retry ceiling is compared against that value, so the message
/// can never reach it: it retries for ever and never dead-letters.
/// </para>
/// <para>
/// <b>The arms discriminate.</b> The first claim pins the count at 0 on a genuinely untried message, so
/// the later assertion cannot be satisfied by a store that returns a constant. The stored column is read
/// independently and asserted to hold the failed count, so a reload returning 0 is distinguishable from a
/// persistence layer that never recorded the failure at all — the two failure modes have different causes
/// and this separates them. On the reload that drops the count the reclaim arm reads 0 against a stored 2
/// and is RED; nothing else in the file changes.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxDrainRestoresAttemptsShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const int FailedAttemptCount = 2;

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresOutboxDrainRestoresAttemptsShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task HandTheDrainThePersistedAttemptCount_NotAFreshZero()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "outbox-drain-restores-attempts";

		await store.StageMessageAsync(
			new OutboundMessage { Id = messageId, MessageType = "T", Payload = [1], Destination = "dest" },
			CancellationToken.None).ConfigureAwait(false);

		// Claim 1 — an untried message. Pins the count at 0, so the reclaim assertion below cannot be met
		// by a store that reports a constant.
		var firstClaim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		firstClaim.ShouldContain(m => m.Id == messageId, "the first claim must reserve the staged message");
		firstClaim.Single(m => m.Id == messageId).RetryCount
			.ShouldBe(0, "a message that has never been attempted carries no attempt history");

		await store.MarkFailedAsync(messageId, "transient error", FailedAttemptCount, CancellationToken.None)
			.ConfigureAwait(false);

		// Read the stored column directly: this is what the reload has available to restore, so a reclaim
		// reading 0 against this value is a reload defect and not a lost write.
		(await StoredAttemptsAsync(messageId).ConfigureAwait(false))
			.ShouldBe(FailedAttemptCount, "the failure must have recorded the attempt count on the row");

		// Claim 2 — polled, because the failure floor is stamped from the server clock and the moment it
		// lifts is not knowable from here.
		var reclaimed = await WaitForReclaimAsync(store, messageId, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
		reclaimed.ShouldNotBeNull("after the failure floor elapses the failed message must return to the pool");

		reclaimed!.RetryCount.ShouldBe(
			FailedAttemptCount,
			"the drain reload must restore the persisted attempt count; a message reloaded as untried can "
			+ "never reach the retry ceiling, so it retries for ever and never dead-letters");
	}

	private static async Task<OutboundMessage?> WaitForReclaimAsync(IOutboxStore store, string messageId, TimeSpan within)
	{
		OutboundMessage? found = null;

		_ = await WaitHelpers.WaitUntilAsync(
			async () =>
			{
				var claim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
				found = claim.FirstOrDefault(m => m.Id == messageId);
				return found is not null;
			},
			within,
			TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);

		return found;
	}

	private async Task<PostgresOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — the real-infra drain-reload lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			// Above the failed count, so the message stays a retryable failure in the main table rather than
			// dead-lettering before the reclaim under test.
			MaxAttempts = 5,
			// The failure-anchored visibility floor, at its smallest legal value, so the reclaim happens
			// inside the test window instead of behind the 30-second default.
			FailureBackoffFloorSeconds = 1,
		});

		return new PostgresOutboxStore(db, options, NullLogger<PostgresOutboxStore>.Instance);
	}

	private async Task<int> StoredAttemptsAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		return await connection.ExecuteScalarAsync<int>(
			$"SELECT attempts FROM {_fixture.SchemaName}.{_fixture.OutboxTableName} WHERE message_id = @Id",
			new { Id = messageId }).ConfigureAwait(false);
	}
}
