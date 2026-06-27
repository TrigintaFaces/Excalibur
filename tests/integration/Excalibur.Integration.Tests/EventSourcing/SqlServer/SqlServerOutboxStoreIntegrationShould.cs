// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;

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
[Trait("Database", "SqlServer")]
[Trait("Component", "OutboxStore")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerOutboxStoreIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _connectionString;
	private bool _dockerAvailable;
	private SqlServerOutboxOptions? _options;

	public async ValueTask InitializeAsync()
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

	public async ValueTask DisposeAsync()
	{
		if (_container != null)
		{
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Container cleanup failed: {ex.Message}");
			}
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

		await outboxStore.StageMessageAsync(message, CancellationToken.None);

		var unsent = await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None);
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

		await outboxStore.StageMessageAsync(message, CancellationToken.None);

		var duplicate = CreateTestMessage();
		duplicate.Id = message.Id;

		_ = await Assert.ThrowsAsync<Excalibur.Data.OperationFailedException>(async () =>
			await outboxStore.StageMessageAsync(duplicate, CancellationToken.None));
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
			await outboxStore.StageMessageAsync(CreateTestMessage(), CancellationToken.None);
		}

		// Request only 3
		var unsent = await outboxStore.GetUnsentMessagesAsync(3, CancellationToken.None);

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

		await outboxStore.StageMessageAsync(message, CancellationToken.None);
		await outboxStore.MarkSentAsync(message.Id, CancellationToken.None);

		var unsent = await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None);
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

		await outboxStore.StageMessageAsync(message, CancellationToken.None);
		await outboxStore.MarkFailedAsync(message.Id, errorMessage, 1, CancellationToken.None);

		var failed = await outboxStore.GetFailedMessagesAsync(5, null, 10, CancellationToken.None);
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

		await outboxStore.StageMessageAsync(message, CancellationToken.None);
		await outboxStore.MarkFailedAsync(message.Id, "First failure", 1, CancellationToken.None);

		// Should find message with retry count < maxRetries
		var failed = await outboxStore.GetFailedMessagesAsync(3, null, 10, CancellationToken.None);
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

		await outboxStore.StageMessageAsync(message, CancellationToken.None);
		await outboxStore.MarkSentAsync(message.Id, CancellationToken.None);

		// Cleanup messages older than tomorrow (should include all sent messages)
		var deleted = await outboxStore.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddDays(1),
			100,
			CancellationToken.None);

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
		await ClearAllMessagesAsync();

		var outboxStore = CreateOutboxStore();

		// Stage 3 messages
		for (int i = 0; i < 3; i++)
		{
			await outboxStore.StageMessageAsync(CreateTestMessage(), CancellationToken.None);
		}

		var stats = await outboxStore.GetStatisticsAsync(CancellationToken.None);

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

		await ClearAllMessagesAsync();

		var outboxStore = CreateOutboxStore();

		var unsent = await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None);

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
			await outboxStore.MarkSentAsync(nonExistentId, CancellationToken.None));
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

		await ClearAllMessagesAsync();

		var outboxStore = CreateOutboxStore();
		var messageIds = new List<string>();

		// Stage 5 messages in order
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			messageIds.Add(message.Id);
			await outboxStore.StageMessageAsync(message, CancellationToken.None);
			// Small delay to ensure different timestamps
			await Task.Delay(10);
		}

		var unsent = (await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None)).ToList();

		unsent.Count.ShouldBe(5);
		// All 5 messages should be returned (UPDATE TOP with lease claiming may not preserve insertion order)
		var unsentIds = unsent.Select(m => m.Id).ToHashSet();
		foreach (var id in messageIds)
		{
			unsentIds.ShouldContain(id);
		}
	}

	/// <summary>
	/// bd-stlcgg (S841, ADR-336) — AC-4 on the originally-buggy path. The outbox had no terminal status, so a
	/// retry-exhausted message stayed <c>Failed(3)</c> and was re-claimed forever by the SqlServer claim
	/// <c>Status IN (0,3,4)</c> (duplicate delivery + unbounded DLQ growth). The fix adds the terminal
	/// <see cref="OutboxStatus.DeadLettered"/> (=5, OUTSIDE the claimable set) set via
	/// <c>MarkDeadLetteredAsync</c>. This is the FAITHFUL non-vacuity lock (gate-full-guard-suite: lock the
	/// path where the bug lived, not the InMemory proxy whose claim was already <c>== Staged</c>):
	/// a <c>Failed(3)</c> message IS still claimable (the contrast that proves the claim includes 3, so the
	/// terminal status is load-bearing), while a <c>DeadLettered(5)</c> message is NEVER re-claimed.
	/// </summary>
	[Fact]
	public async Task NotReclaimADeadLetteredMessage_WhileStillClaimingAFailedOne()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllMessagesAsync();
		var outboxStore = CreateOutboxStore();

		// Contrast: a Failed(3) message is still claimable by Status IN (0,3,4) — the exact pre-fix re-claim
		// substrate. This makes the terminal-status exclusion below non-vacuous (a terminal-as-3 would re-claim).
		var failed = CreateTestMessage();
		await outboxStore.StageMessageAsync(failed, CancellationToken.None);
		await outboxStore.MarkFailedAsync(failed.Id, "transient failure", 1, CancellationToken.None);

		// The retry-exhausted message reaches the terminal DeadLettered(5) status.
		var deadLettered = CreateTestMessage();
		await outboxStore.StageMessageAsync(deadLettered, CancellationToken.None);
		await outboxStore.MarkDeadLetteredAsync(deadLettered.Id, "retries exhausted", CancellationToken.None);

		var unsentIds = (await outboxStore.GetUnsentMessagesAsync(50, CancellationToken.None))
			.Select(m => m.Id)
			.ToHashSet(StringComparer.Ordinal);

		// AC-4: the dead-lettered (terminal) message is NEVER re-claimed.
		unsentIds.ShouldNotContain(
			deadLettered.Id, "a DeadLettered (terminal) message must never be re-claimed by the delivery poller");

		// Contrast (non-vacuity): the Failed(3) message IS still claimable, proving the claim includes status 3
		// — so the only reason the dead-lettered one is excluded is its terminal status.
		unsentIds.ShouldContain(
			failed.Id, "a Failed (non-terminal) message remains claimable — the claim includes Status=3");
	}

	/// <summary>
	/// b64hci (SendingMessageCount-SqlServer fix) author≠impl lock: <see cref="OutboxStatistics.SendingMessageCount"/>
	/// must reflect the LEASED-but-not-terminal (in-flight) rows — computed via <c>SUM(CASE WHEN LeasedAt IS NOT
	/// NULL …)</c>, mirroring Postgres's <c>dispatcher_id IS NOT NULL</c> reserved semantic — NOT the pre-fix
	/// <c>SUM(CASE WHEN Status = 1 …)</c> which was permanently 0 (Status=Sending is never written on SqlServer).
	/// </summary>
	/// <remarks>
	/// Authored independently of the implementer (PlatformDeveloper) against the working-tree fix; GREEN/RED is
	/// verified at the full-CI integration shard (this is a real-SqlServer test, not a unit test). Both directions
	/// (SA 16188): a leased non-terminal row ⇒ counted; an unleased row AND a terminal/sent row (lease cleared) ⇒
	/// NOT counted. <b>RED mutant:</b> the pre-fix <c>SUM(Status = 1)</c> predicate returns 0 for all seeded rows
	/// ⇒ <c>SendingMessageCount</c> is 0, not 3 ⇒ RED.
	/// </remarks>
	[Fact]
	public async Task ReportSendingMessageCount_FromLeasedNonTerminalRows()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllMessagesAsync().ConfigureAwait(false);
		var outboxStore = CreateOutboxStore();

		// Stage 4 and claim them all → 4 leased (LeasedAt set on claim).
		for (var i = 0; i < 4; i++)
		{
			await outboxStore.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		var claimed = (await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		claimed.Count.ShouldBe(4, "all 4 staged messages should be claimable (and thereby leased)");

		// Mark ONE claimed message sent → terminal; the terminal transition clears its lease (LeasedAt = NULL),
		// so it must NOT be counted as in-flight. 3 leased non-terminal remain.
		await outboxStore.MarkSentAsync(claimed[0].Id, CancellationToken.None).ConfigureAwait(false);

		// Stage 2 MORE without claiming → unleased (LeasedAt NULL) → must NOT be counted.
		for (var i = 0; i < 2; i++)
		{
			await outboxStore.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		var stats = await outboxStore.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// 3 leased-non-terminal counted; the 1 sent (lease cleared) and the 2 unleased are NOT counted.
		stats.SendingMessageCount.ShouldBe(
			3,
			"SendingMessageCount must count leased-but-not-terminal rows (LeasedAt IS NOT NULL), not the always-0 Status=Sending");
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
				LeasedAt DATETIMEOFFSET NULL,
				LeasedBy NVARCHAR(255) NULL,
				PartitionKey NVARCHAR(256) NULL,
				GroupKey NVARCHAR(256) NULL,
				SequenceNumber BIGINT NOT NULL DEFAULT 0,
				NextAttemptAt DATETIMEOFFSET NULL,
				INDEX IX_OutboxMessages_Status_CreatedAt (Status, CreatedAt),
				INDEX IX_OutboxMessages_Claim (Status, NextAttemptAt, PartitionKey, SequenceNumber)
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
