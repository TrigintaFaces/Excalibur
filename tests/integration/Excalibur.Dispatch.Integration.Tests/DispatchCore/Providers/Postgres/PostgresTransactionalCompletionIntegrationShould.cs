// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Dapper;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.Abstractions;
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
/// Integration tests for transactional outbox+inbox completion on Postgres.
/// Verifies fallback behavior when the store does not support atomic completion.
/// </summary>
/// <remarks>
/// Sprint 381 - CQRS write-side audit remediation.
/// Excalibur.Dispatch-fsrbr: Validate transactional outbox/inbox behavior for SQL Server and Postgres.
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "TransactionalCompletion")]
[Trait("Provider", "Postgres")]
public sealed class PostgresTransactionalCompletionIntegrationShould : IntegrationTestBase
{
	private readonly PostgresFixture _pgFixture;

	public PostgresTransactionalCompletionIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	[Fact]
	public async Task TryMarkSentAndReceived_ReturnsFalse_AndDoesNotWriteInbox()
	{
		await InitializeTablesAsync().ConfigureAwait(true);

		IOutboxStore store = CreateOutboxStore();
		var message = CreateTestMessage();
		await store.StageMessageAsync(message, TestCancellationToken).ConfigureAwait(true);

		var inboxEntry = CreateTestInboxEntry(message.Id);

		var result = await store.TryMarkSentAndReceivedAsync(
				message.Id,
				inboxEntry,
				TestCancellationToken)
			.ConfigureAwait(true);

		result.ShouldBeFalse("Postgres outbox store does not support transactional completion");

		var outboxCount = await CountOutboxEntriesAsync(message.Id).ConfigureAwait(true);
		outboxCount.ShouldBe(1, "Message should remain in outbox when transactional completion is unavailable");

		var inboxCount = await CountInboxEntriesAsync(message.Id).ConfigureAwait(true);
		inboxCount.ShouldBe(0, "No inbox entry should be created when transactional completion is unavailable");
	}

	private static OutboundMessage CreateTestMessage()
	{
		return new OutboundMessage(
			$"TestMessage_{Guid.NewGuid():N}",
			System.Text.Encoding.UTF8.GetBytes("{\"test\":true}"),
			"test-destination",
			new Dictionary<string, object>(StringComparer.Ordinal));
	}

	private static InboxEntry CreateTestInboxEntry(string messageId)
	{
		return new InboxEntry(
			messageId,
			"TestHandler",
			"TestMessageType",
			System.Text.Encoding.UTF8.GetBytes("{\"test\":true}"),
			new Dictionary<string, object>(StringComparer.Ordinal))
		{
			CorrelationId = Guid.NewGuid().ToString(),
			Source = "test-source",
			Status = InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow
		};
	}

	private PostgresOutboxStore CreateOutboxStore()
	{
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).Returns(new NpgsqlConnection(_pgFixture.ConnectionString));

		var options = Microsoft.Extensions.Options.Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = "public",
			OutboxTableName = "outbox",
			DeadLetterTableName = "outbox_dead_letters",
			ReservationTimeout = 300,
			MaxAttempts = 3
		});

		return new PostgresOutboxStore(db, options, NullLogger<PostgresOutboxStore>.Instance);
	}

	private async Task InitializeTablesAsync()
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

            CREATE INDEX IF NOT EXISTS idx_outbox_unreserved ON outbox (occurred_on)
                WHERE dispatcher_id IS NULL;
            CREATE INDEX IF NOT EXISTS idx_outbox_dispatcher ON outbox (dispatcher_id)
                WHERE dispatcher_id IS NOT NULL;

            CREATE TABLE IF NOT EXISTS inbox_messages (
                message_id VARCHAR(255) NOT NULL,
                handler_type VARCHAR(500) NOT NULL,
                message_type VARCHAR(500) NOT NULL,
                payload BYTEA NOT NULL,
                metadata JSONB NULL,
                received_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                processed_at TIMESTAMPTZ NULL,
                status INT NOT NULL DEFAULT 0,
                retry_count INT NOT NULL DEFAULT 0,
                last_error TEXT NULL,
                last_attempt_at TIMESTAMPTZ NULL,
                correlation_id VARCHAR(255) NULL,
                tenant_id VARCHAR(255) NULL,
                source VARCHAR(255) NULL,
                PRIMARY KEY (message_id, handler_type)
            );

            CREATE INDEX IF NOT EXISTS idx_inbox_status ON inbox_messages (status);
            CREATE INDEX IF NOT EXISTS idx_inbox_received ON inbox_messages (received_at);
            """;

		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(createTablesSql).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(
				"TRUNCATE TABLE inbox_messages CASCADE; TRUNCATE TABLE outbox CASCADE; TRUNCATE TABLE outbox_dead_letters CASCADE;")
			.ConfigureAwait(true);
	}

	private async Task<int> CountOutboxEntriesAsync(string messageId)
	{
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		return await connection.ExecuteScalarAsync<int>(
				"SELECT COUNT(*) FROM outbox WHERE message_id = @MessageId",
				new { MessageId = messageId })
			.ConfigureAwait(true);
	}

	private async Task<int> CountInboxEntriesAsync(string messageId)
	{
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		return await connection.ExecuteScalarAsync<int>(
				"SELECT COUNT(*) FROM inbox_messages WHERE message_id = @MessageId",
				new { MessageId = messageId })
			.ConfigureAwait(true);
	}
}
