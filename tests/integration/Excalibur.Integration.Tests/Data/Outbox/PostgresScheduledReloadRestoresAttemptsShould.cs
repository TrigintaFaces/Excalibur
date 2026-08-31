// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-Postgres lock: the SCHEDULED reload must restore a message's persisted attempt count.
/// </summary>
/// <remarks>
/// <para>
/// Sibling of the drain-reload lock, same class and a different cost. The scheduled read rebuilt an
/// <see cref="OutboundMessage"/> from a row whose SELECT did not project the attempts column at all, so
/// the constructor default of zero survived. A scheduled message that keeps failing therefore restarted
/// its history on every poll and could never reach the dead-letter ceiling — retried for ever.
/// </para>
/// <para>
/// <b>The arms discriminate.</b> The stored column is read independently, so a reload returning zero is
/// distinguishable from a persistence layer that never recorded the failure. The first arm pins zero on a
/// genuinely untried scheduled message, so the second cannot be satisfied by a store that returns a
/// constant.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresScheduledReloadRestoresAttemptsShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const int FailedAttemptCount = 2;

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresScheduledReloadRestoresAttemptsShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ReloadAScheduledMessageCarryingItsPersistedAttemptCount()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "outbox-scheduled-restores-attempts";
		var dueAt = DateTimeOffset.UtcNow.AddMinutes(-1);

		await store.StageMessageAsync(
			new OutboundMessage
			{
				Id = messageId,
				MessageType = "T",
				Payload = [1],
				Destination = "dest",
				ScheduledAt = dueAt,
			},
			CancellationToken.None).ConfigureAwait(false);

		// A genuinely untried scheduled message reads zero. This pins the constant so the assertion below
		// cannot pass against a store that reports a fixed value.
		var untried = await store.GetAllTenantsScheduledMessagesAsync(
			DateTimeOffset.UtcNow, 10, CancellationToken.None).ConfigureAwait(false);
		untried.Single(m => m.Id == messageId).RetryCount
			.ShouldBe(0, "a scheduled message that has never been attempted carries no attempt history");

		await store.MarkFailedAsync(messageId, "transient error", FailedAttemptCount, CancellationToken.None)
			.ConfigureAwait(false);

		// Read the column directly: this is what the reload has available, so a reload reading 0 against
		// this value is a projection defect and not a lost write.
		(await StoredAttemptsAsync(messageId).ConfigureAwait(false))
			.ShouldBe(FailedAttemptCount, "the failure must have recorded the attempt count on the row");

		var reloaded = await store.GetAllTenantsScheduledMessagesAsync(
			DateTimeOffset.UtcNow, 10, CancellationToken.None).ConfigureAwait(false);

		var message = reloaded.SingleOrDefault(m => m.Id == messageId);
		message.ShouldNotBeNull("the failed scheduled message must still be returned by the scheduled read");

		message!.RetryCount.ShouldBe(
			FailedAttemptCount,
			"the scheduled reload must restore the persisted attempt count; a scheduled message reloaded "
			+ "as untried can never reach the retry ceiling, so it retries for ever and never dead-letters");
	}

	private async Task<PostgresOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — the real-infra scheduled-reload lock is never skipped.");
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
			// Above the failed count, so the message stays a retryable failure rather than dead-lettering
			// before the reload under test.
			MaxAttempts = 5,
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
