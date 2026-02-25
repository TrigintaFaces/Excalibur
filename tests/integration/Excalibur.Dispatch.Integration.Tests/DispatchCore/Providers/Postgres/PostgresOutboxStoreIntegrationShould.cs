// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.Abstractions;

using OutboxMessage = Excalibur.Dispatch.Delivery.OutboxMessage;
using Excalibur.Data.Postgres.Outbox;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Integration tests for <see cref="PostgresOutboxStore"/> using TestContainers.
/// Tests real Postgres database operations for outbox message persistence.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 177 - Provider Testing Epic Phase 3.
/// bd-pdikd: Postgres OutboxStore Tests (5 tests).
/// </para>
/// <para>
/// These tests verify the PostgresOutboxStore implementation against a real Postgres
/// database using TestContainers. Tests cover save, reserve, delete, and ordering behavior.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "Outbox")]
[Trait("Provider", "Postgres")]
public sealed class PostgresOutboxStoreIntegrationShould : IntegrationTestBase
{
	private readonly PostgresFixture _pgFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresOutboxStoreIntegrationShould"/> class.
	/// </summary>
	/// <param name="pgFixture">The Postgres container fixture.</param>
	public PostgresOutboxStoreIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	/// <summary>
	/// Tests that a message can be staged in the outbox.
	/// </summary>
	[Fact]
	public async Task StageMessage()
	{
		// Arrange
		await InitializeOutboxTableAsync().ConfigureAwait(true);
		var (store, _) = CreateOutboxStore();
		var message = CreateTestOutboxMessage();

		// Act
		var count = await store.SaveMessagesAsync(new[] { message }, TestCancellationToken).ConfigureAwait(true);

		// Assert
		count.ShouldBe(1);

		// Verify in database
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		var dbCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox WHERE message_id = @id", new { id = message.MessageId }).ConfigureAwait(true);
		dbCount.ShouldBe(1);
	}

	/// <summary>
	/// Tests that staging a duplicate message ID throws.
	/// </summary>
	[Fact]
	public async Task StageMessageDuplicateIdThrows()
	{
		// Arrange
		await InitializeOutboxTableAsync().ConfigureAwait(true);
		var (store, _) = CreateOutboxStore();
		var message = CreateTestOutboundMessage();
		var duplicate = CreateTestOutboundMessage(message.Id);

		await store.StageMessageAsync(message, TestCancellationToken).ConfigureAwait(true);

		// Act & Assert - Postgres implementation wraps database exceptions in OperationFailedException
		_ = await Should.ThrowAsync<OperationFailedException>(async () =>
			await store.StageMessageAsync(duplicate, TestCancellationToken).ConfigureAwait(true));
	}

	/// <summary>
	/// Tests that unsent messages can be retrieved.
	/// </summary>
	[Fact]
	public async Task RetrieveUnsentMessages()
	{
		// Arrange
		await InitializeOutboxTableAsync().ConfigureAwait(true);
		var (store, _) = CreateOutboxStore();
		var messages = new[]
		{
			CreateTestOutboxMessage(),
			CreateTestOutboxMessage(),
			CreateTestOutboxMessage()
		};

		_ = await store.SaveMessagesAsync(messages, TestCancellationToken).ConfigureAwait(true);

		// Act
		var dispatcherId = $"test-dispatcher-{Guid.NewGuid()}";
		var reserved = await store.ReserveOutboxMessagesAsync(dispatcherId, 10, TestCancellationToken).ConfigureAwait(true);

		// Assert
		reserved.Count().ShouldBe(3);
	}

	/// <summary>
	/// Tests that a sent message can be marked and deleted.
	/// </summary>
	[Fact]
	public async Task MarkMessageAsSent()
	{
		// Arrange
		await InitializeOutboxTableAsync().ConfigureAwait(true);
		var (store, _) = CreateOutboxStore();
		var message = CreateTestOutboxMessage();

		_ = await store.SaveMessagesAsync(new[] { message }, TestCancellationToken).ConfigureAwait(true);

		// Act
		var deleted = await store.DeleteOutboxRecord(message.MessageId, TestCancellationToken).ConfigureAwait(true);

		// Assert
		deleted.ShouldBe(1);

		// Verify deleted from database
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		var dbCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox WHERE message_id = @id", new { id = message.MessageId }).ConfigureAwait(true);
		dbCount.ShouldBe(0);
	}

	/// <summary>
	/// Tests that MarkSentAsync throws for a non-existent message.
	/// </summary>
	[Fact]
	public async Task MarkSentNonExistentThrows()
	{
		// Arrange
		await InitializeOutboxTableAsync().ConfigureAwait(true);
		var (store, _) = CreateOutboxStore();
		var nonExistentId = Guid.NewGuid().ToString();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.MarkSentAsync(nonExistentId, TestCancellationToken).ConfigureAwait(true));
	}

	/// <summary>
	/// Tests that a failed message can be moved to dead letter.
	/// </summary>
	[Fact]
	public async Task MarkMessageAsFailed()
	{
		// Arrange
		await InitializeOutboxTableAsync().ConfigureAwait(true);
		var (store, _) = CreateOutboxStore();
		var message = CreateTestOutboxMessage();

		_ = await store.SaveMessagesAsync(new[] { message }, TestCancellationToken).ConfigureAwait(true);

		// Increase attempts first
		_ = await store.IncreaseAttempts(message.MessageId, TestCancellationToken).ConfigureAwait(true);

		// Act - Move to dead letter
		var moved = await store.MoveToDeadLetter(message.MessageId, TestCancellationToken).ConfigureAwait(true);

		// Assert - ExecuteAsync returns total affected rows (1 INSERT + 1 DELETE = 2)
		moved.ShouldBe(2);

		// Verify moved to dead letter table
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);

		var outboxCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox WHERE message_id = @id", new { id = message.MessageId }).ConfigureAwait(true);
		var deadLetterCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox_dead_letters WHERE message_id = @id", new { id = message.MessageId }).ConfigureAwait(true);

		outboxCount.ShouldBe(0);
		deadLetterCount.ShouldBe(1);
	}

	/// <summary>
	/// Tests that message order is preserved (FIFO).
	/// </summary>
	[Fact]
	public async Task PreserveMessageOrder()
	{
		// Arrange
		await InitializeOutboxTableAsync().ConfigureAwait(true);
		var (store, _) = CreateOutboxStore();

		// Create messages with sequence numbers in metadata
		var messages = new List<IOutboxMessage>();
		for (var i = 1; i <= 5; i++)
		{
			var msg = new OutboxMessage(
				Guid.NewGuid().ToString(),
				"TestMessage",
				$"{{\"sequence\": {i}}}",
				$"{{\"data\": \"message-{i}\"}}",
				DateTimeOffset.UtcNow);
			messages.Add(msg);
			_ = await store.SaveMessagesAsync(new[] { msg }, TestCancellationToken).ConfigureAwait(true);
			await Task.Delay(10, TestCancellationToken).ConfigureAwait(true); // Ensure distinct timestamps
		}

		// Act
		var dispatcherId = $"test-dispatcher-{Guid.NewGuid()}";
		var reserved = (await store.ReserveOutboxMessagesAsync(dispatcherId, 10, TestCancellationToken).ConfigureAwait(true)).ToList();

		// Assert - Messages should be in FIFO order (sequence 1, 2, 3, 4, 5)
		reserved.Count.ShouldBe(5);
		for (var i = 0; i < reserved.Count; i++)
		{
			reserved[i].MessageMetadata.ShouldContain($"\"sequence\": {i + 1}");
		}
	}

	private static OutboundMessage CreateTestOutboundMessage(string? messageId = null)
	{
		var message = new OutboundMessage(
			"TestMessage",
			System.Text.Encoding.UTF8.GetBytes("{\"data\": \"test\"}"),
			"test-destination",
			new Dictionary<string, object>(StringComparer.Ordinal));

		if (!string.IsNullOrWhiteSpace(messageId))
		{
			message.Id = messageId;
		}

		return message;
	}

	private static OutboxMessage CreateTestOutboxMessage()
	{
		return new OutboxMessage(
			Guid.NewGuid().ToString(),
			"TestMessage",
			"{\"correlationId\": \"test-123\"}",
			"{\"data\": \"test payload\"}",
			DateTimeOffset.UtcNow);
	}

	private (PostgresOutboxStore store, IDb db) CreateOutboxStore()
	{
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).Returns(new NpgsqlConnection(_pgFixture.ConnectionString));

		var options = Microsoft.Extensions.Options.Options.Create(new PostgresOutboxStoreOptions
		{
			OutboxTableName = "outbox",
			DeadLetterTableName = "outbox_dead_letters",
			ReservationTimeout = 300,
			MaxAttempts = 3
		});
		var logger = NullLogger<PostgresOutboxStore>.Instance;
		var store = new PostgresOutboxStore(db, options, logger);
		return (store, db);
	}

	private async Task InitializeOutboxTableAsync()
	{
		const string createTablesSql = """
			CREATE TABLE IF NOT EXISTS outbox (
			    id SERIAL PRIMARY KEY,
			    message_id VARCHAR(100) NOT NULL UNIQUE,
			    message_type VARCHAR(500) NOT NULL,
			    message_metadata TEXT,
			    message_body TEXT NOT NULL,
			    occurred_on TIMESTAMPTZ NOT NULL DEFAULT NOW(),
			    attempts INT NOT NULL DEFAULT 0,
			    dispatcher_id VARCHAR(100),
			    dispatcher_timeout TIMESTAMPTZ
			);

			CREATE TABLE IF NOT EXISTS outbox_dead_letters (
			    id SERIAL PRIMARY KEY,
			    message_id VARCHAR(100) NOT NULL UNIQUE,
			    message_type VARCHAR(500) NOT NULL,
			    message_metadata TEXT,
			    message_body TEXT NOT NULL,
			    occurred_on TIMESTAMPTZ NOT NULL DEFAULT NOW(),
			    attempts INT NOT NULL DEFAULT 0,
			    error_message TEXT,
			    moved_on TIMESTAMPTZ NOT NULL DEFAULT NOW()
			);

			CREATE INDEX IF NOT EXISTS idx_outbox_unreserved ON outbox (occurred_on) WHERE dispatcher_id IS NULL;
			CREATE INDEX IF NOT EXISTS idx_outbox_dispatcher ON outbox (dispatcher_id) WHERE dispatcher_id IS NOT NULL;
			""";

		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(createTablesSql).ConfigureAwait(true);

		// Clean up any existing data for test isolation
		_ = await connection.ExecuteAsync("TRUNCATE TABLE outbox CASCADE; TRUNCATE TABLE outbox_dead_letters CASCADE;").ConfigureAwait(true);
	}
}
