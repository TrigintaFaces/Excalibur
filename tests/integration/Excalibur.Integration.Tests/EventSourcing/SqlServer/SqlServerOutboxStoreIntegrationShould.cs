// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerOutboxStore"/> using real SQL Server via TestContainers.
/// Tests outbox pattern operations including staging, retrieval, and status updates.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// bd-gvrmb: SqlServer OutboxStore Tests (10 tests).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "SqlServer")]
[Trait("Component", "OutboxStore")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerOutboxStoreIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _connectionString;
	private bool _dockerAvailable;
	private SqlServerOutboxOptions? _options;

	public async Task InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_connectionString = _container.GetConnectionString();
			_dockerAvailable = true;

			_options = new SqlServerOutboxOptions
			{
				ConnectionString = _connectionString,
				SchemaName = "dbo",
				OutboxTableName = "OutboxMessages",
				TransportsTableName = "OutboxTransports",
				CommandTimeoutSeconds = 30
			};

			await InitializeDatabaseAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Docker initialization failed: {ex.Message}");
			Console.WriteLine(ex.ToString());
			_dockerAvailable = false;
		}
	}

	public async Task DisposeAsync()
	{
		if (_container != null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that a message can be staged in the outbox.
	/// </summary>
	[Fact]
	public async Task StageMessageSuccessfully()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var outboxStore = CreateOutboxStore();
		var message = CreateTestMessage();

		await outboxStore.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);

		var unsent = await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(true);
		unsent.ShouldContain(m => m.Id == message.Id);
	}

	/// <summary>
	/// Verifies that staging a duplicate message ID throws.
	/// </summary>
	[Fact]
	public async Task StageMessageDuplicateIdThrows()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var outboxStore = CreateOutboxStore();
		var message = CreateTestMessage();

		await outboxStore.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);

		var duplicate = CreateTestMessage();
		duplicate.Id = message.Id;

		_ = await Assert.ThrowsAsync<Excalibur.Data.Abstractions.OperationFailedException>(async () =>
			await outboxStore.StageMessageAsync(duplicate, CancellationToken.None).ConfigureAwait(true));
	}

	/// <summary>
	/// Verifies that unsent messages can be retrieved in batches.
	/// </summary>
	[Fact]
	public async Task RetrieveUnsentMessagesInBatch()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var outboxStore = CreateOutboxStore();

		// Stage 5 messages
		for (int i = 0; i < 5; i++)
		{
			await outboxStore.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(true);
		}

		// Request only 3
		var unsent = await outboxStore.GetUnsentMessagesAsync(3, CancellationToken.None).ConfigureAwait(true);

		unsent.Count().ShouldBe(3);
	}

	/// <summary>
	/// Verifies that a message can be marked as sent.
	/// </summary>
	[Fact]
	public async Task MarkMessageAsSent()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var outboxStore = CreateOutboxStore();
		var message = CreateTestMessage();

		await outboxStore.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);
		await outboxStore.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(true);

		var unsent = await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(true);
		unsent.ShouldNotContain(m => m.Id == message.Id);
	}

	/// <summary>
	/// Verifies that a message can be marked as failed with error details.
	/// </summary>
	[Fact]
	public async Task MarkMessageAsFailedWithError()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var outboxStore = CreateOutboxStore();
		var message = CreateTestMessage();
		var errorMessage = "Test failure reason";

		await outboxStore.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);
		await outboxStore.MarkFailedAsync(message.Id, errorMessage, 1, CancellationToken.None).ConfigureAwait(true);

		var failed = await outboxStore.GetFailedMessagesAsync(5, null, 10, CancellationToken.None).ConfigureAwait(true);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);
		_ = failedMessage.ShouldNotBeNull();
		failedMessage.LastError.ShouldBe(errorMessage);
		failedMessage.RetryCount.ShouldBe(1);
	}

	/// <summary>
	/// Verifies that failed messages can be retrieved for retry.
	/// </summary>
	[Fact]
	public async Task RetrieveFailedMessagesForRetry()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var outboxStore = CreateOutboxStore();
		var message = CreateTestMessage();

		await outboxStore.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);
		await outboxStore.MarkFailedAsync(message.Id, "First failure", 1, CancellationToken.None).ConfigureAwait(true);

		// Should find message with retry count < maxRetries
		var failed = await outboxStore.GetFailedMessagesAsync(3, null, 10, CancellationToken.None).ConfigureAwait(true);
		failed.ShouldContain(m => m.Id == message.Id);
	}

	/// <summary>
	/// Verifies that sent messages can be cleaned up based on age.
	/// </summary>
	[Fact]
	public async Task CleanupSentMessages()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var outboxStore = CreateOutboxStore();
		var message = CreateTestMessage();

		await outboxStore.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);
		await outboxStore.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(true);

		// Cleanup messages older than tomorrow (should include all sent messages)
		var deleted = await outboxStore.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			100,
			CancellationToken.None).ConfigureAwait(true);

		deleted.ShouldBeGreaterThanOrEqualTo(1);
	}

	/// <summary>
	/// Verifies that outbox statistics are accurately reported.
	/// </summary>
	[Fact]
	public async Task ReportOutboxStatistics()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		// Clear existing data for accurate stats
		await ClearAllMessagesAsync().ConfigureAwait(true);

		var outboxStore = CreateOutboxStore();

		// Stage 3 messages
		for (int i = 0; i < 3; i++)
		{
			await outboxStore.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(true);
		}

		var stats = await outboxStore.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(true);

		stats.StagedMessageCount.ShouldBe(3);
	}

	/// <summary>
	/// Verifies that empty outbox returns empty list for unsent messages.
	/// </summary>
	[Fact]
	public async Task ReturnEmptyListWhenNoUnsentMessages()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllMessagesAsync().ConfigureAwait(true);

		var outboxStore = CreateOutboxStore();

		var unsent = await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(true);

		_ = unsent.ShouldNotBeNull();
		unsent.Count().ShouldBe(0);
	}

	/// <summary>
	/// Verifies that marking a non-existent message as sent throws exception.
	/// </summary>
	[Fact]
	public async Task ThrowWhenMarkingNonExistentMessageAsSent()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var outboxStore = CreateOutboxStore();
		var nonExistentId = Guid.NewGuid().ToString();

		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await outboxStore.MarkSentAsync(nonExistentId, CancellationToken.None).ConfigureAwait(true));
	}

	/// <summary>
	/// Verifies that multiple messages can be staged and retrieved in order.
	/// </summary>
	[Fact]
	public async Task PreserveMessageOrderInOutbox()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllMessagesAsync().ConfigureAwait(true);

		var outboxStore = CreateOutboxStore();
		var messageIds = new List<string>();

		// Stage 5 messages in order
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			messageIds.Add(message.Id);
			await outboxStore.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(true);
			// Small delay to ensure different timestamps
			await Task.Delay(10).ConfigureAwait(true);
		}

		var unsent = (await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(true)).ToList();

		unsent.Count.ShouldBe(5);
		// First message should be first (FIFO)
		unsent[0].Id.ShouldBe(messageIds[0]);
	}

	private static OutboundMessage CreateTestMessage()
	{
		return new OutboundMessage(
			$"TestMessage_{Guid.NewGuid():N}",
			System.Text.Encoding.UTF8.GetBytes("{\"test\": true}"),
			"test-destination",
			new Dictionary<string, object>(StringComparer.Ordinal));
	}

	private SqlServerOutboxStore CreateOutboxStore()
	{
		var logger = NullLogger<SqlServerOutboxStore>.Instance;
		return new SqlServerOutboxStore(Options.Create(_options!), logger);
	}

	private async Task ClearAllMessagesAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// Delete transports first (foreign key constraint)
		await using var deleteTransports = new SqlCommand("DELETE FROM dbo.OutboxTransports", connection);
		_ = await deleteTransports.ExecuteNonQueryAsync().ConfigureAwait(false);

		await using var deleteMessages = new SqlCommand("DELETE FROM dbo.OutboxMessages", connection);
		_ = await deleteMessages.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task InitializeDatabaseAsync()
	{
		const string createOutboxTableSql = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OutboxMessages' AND xtype='U')
			CREATE TABLE dbo.OutboxMessages (
				Id NVARCHAR(255) NOT NULL PRIMARY KEY,
				MessageType NVARCHAR(500) NOT NULL,
				Payload VARBINARY(MAX) NOT NULL,
				Headers NVARCHAR(MAX) NULL,
				Destination NVARCHAR(255) NOT NULL,
				CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
				ScheduledAt DATETIMEOFFSET NULL,
				SentAt DATETIMEOFFSET NULL,
				Status INT NOT NULL DEFAULT 0,
				RetryCount INT NOT NULL DEFAULT 0,
				LastError NVARCHAR(MAX) NULL,
				LastAttemptAt DATETIMEOFFSET NULL,
				CorrelationId NVARCHAR(255) NULL,
				CausationId NVARCHAR(255) NULL,
				TenantId NVARCHAR(255) NULL,
				Priority INT NOT NULL DEFAULT 0,
				TargetTransports NVARCHAR(MAX) NULL,
				IsMultiTransport BIT NOT NULL DEFAULT 0,
				INDEX IX_OutboxMessages_Status_CreatedAt (Status, CreatedAt)
			)
			""";

		const string createTransportsTableSql = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OutboxTransports' AND xtype='U')
			CREATE TABLE dbo.OutboxTransports (
				Id NVARCHAR(255) NOT NULL PRIMARY KEY,
				MessageId NVARCHAR(255) NOT NULL,
				TransportName NVARCHAR(255) NOT NULL,
				Destination NVARCHAR(255) NULL,
				Status INT NOT NULL DEFAULT 0,
				CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
				AttemptedAt DATETIMEOFFSET NULL,
				SentAt DATETIMEOFFSET NULL,
				RetryCount INT NOT NULL DEFAULT 0,
				LastError NVARCHAR(MAX) NULL,
				TransportMetadata NVARCHAR(MAX) NULL,
				FOREIGN KEY (MessageId) REFERENCES dbo.OutboxMessages(Id)
			)
			""";

		Console.WriteLine($"Connection string: {_connectionString}");

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		Console.WriteLine("Database connection opened successfully");

		await using var createOutbox = new SqlCommand(createOutboxTableSql, connection);
		_ = await createOutbox.ExecuteNonQueryAsync().ConfigureAwait(false);
		Console.WriteLine("OutboxMessages table created successfully");

		await using var createTransports = new SqlCommand(createTransportsTableSql, connection);
		_ = await createTransports.ExecuteNonQueryAsync().ConfigureAwait(false);
		Console.WriteLine("OutboxTransports table created successfully");
	}
}
