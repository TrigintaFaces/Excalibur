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
/// Sprint 849 / Lane R3 (<c>gejhft</c>+<c>ffxglb</c>) — author≠impl behavioral lock for outbox backoff-apply.
/// </summary>
/// <remarks>
/// <para>
/// Pre-fix the outbox processor computed an exponential backoff delay, logged it, then marked the message
/// <c>Failed</c> with no way to carry the delay to the store — the claim re-delivered it on the very next poll
/// (decorative backoff → retry storm). The fix adds the segregated capability
/// <see cref="IBackoffSchedulableOutboxStore"/>: the processor passes an absolute <c>nextAttemptAt</c>, the store
/// persists it, and the claim gates on <c>WHERE NextAttemptAt IS NULL OR NextAttemptAt &lt;= @now</c>.
/// </para>
/// <para>
/// AC-R3.1 lock. The visibility assertions are deterministic because the test supplies <c>nextAttemptAt</c>
/// directly (no jitter/calculator), and the messages are never claimed before being scheduled (so the lease is
/// not a factor — <c>LeasedAt</c> stays null). The non-vacuity contrast is a sibling message marked
/// <c>Failed</c> WITHOUT backoff: both are status <c>Failed</c>, so the ONLY reason the scheduled one is excluded
/// is its <c>NextAttemptAt</c>.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "OutboxStore")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class OutboxBackoffScheduleIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _connectionString;
	private bool _dockerAvailable;

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

			await InitializeDatabaseAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Docker initialization failed: {ex.Message}");
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
	/// AC-R3.1 — a message marked failed-with-backoff is NOT re-claimed until <c>nextAttemptAt</c> elapses, then
	/// becomes claimable again. RED pre-fix: the <c>MarkFailedWithBackoffAsync</c> capability / <c>NextAttemptAt</c>
	/// gate did not exist, so the failed message was claimed immediately on the next poll.
	/// </summary>
	[Fact]
	public async Task NotReclaimAFailedMessageUntilItsNextAttemptElapses()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllMessagesAsync();
		var store = CreateOutboxStore();

		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None);

		// Schedule the next attempt a short, fixed window in the future.
		var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(3);
		await store.MarkFailedWithBackoffAsync(message.Id, "transient failure", 1, nextAttemptAt, CancellationToken.None);

		// Within the backoff window: the message MUST NOT be re-claimed.
		var withinWindow = (await store.GetUnsentMessagesAsync(50, CancellationToken.None))
			.Select(m => m.Id)
			.ToHashSet(StringComparer.Ordinal);
		withinWindow.ShouldNotContain(
			message.Id,
			"a failed-with-backoff message must not be re-claimed until its NextAttemptAt has elapsed");

		// After the window elapses: the message becomes claimable again (the gate is a delay, not a black hole).
		var becameClaimable = await PollUntilAsync(
			async () => (await store.GetUnsentMessagesAsync(50, CancellationToken.None))
				.Any(m => m.Id == message.Id),
			timeout: TimeSpan.FromSeconds(20),
			pollInterval: TimeSpan.FromMilliseconds(250));

		becameClaimable.ShouldBeTrue("the message must become claimable once NextAttemptAt has elapsed");
	}

	/// <summary>
	/// Non-vacuity contrast for AC-R3.1 — a sibling marked <c>Failed</c> WITHOUT a backoff schedule is immediately
	/// re-claimable, while the scheduled one is held. Both are status <c>Failed</c>, isolating <c>NextAttemptAt</c>
	/// as the load-bearing discriminator (a vacuous gate that excluded all failed messages would fail this).
	/// </summary>
	[Fact]
	public async Task StillReclaimAFailedMessageThatHasNoBackoffSchedule()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllMessagesAsync();
		var store = CreateOutboxStore();

		var scheduled = CreateTestMessage();
		await store.StageMessageAsync(scheduled, CancellationToken.None);
		await store.MarkFailedWithBackoffAsync(
			scheduled.Id, "scheduled failure", 1, DateTimeOffset.UtcNow.AddMinutes(10), CancellationToken.None);

		var immediate = CreateTestMessage();
		await store.StageMessageAsync(immediate, CancellationToken.None);
		await store.MarkFailedAsync(immediate.Id, "plain failure", 1, CancellationToken.None);

		var claimable = (await store.GetUnsentMessagesAsync(50, CancellationToken.None))
			.Select(m => m.Id)
			.ToHashSet(StringComparer.Ordinal);

		claimable.ShouldContain(
			immediate.Id, "a Failed message without a backoff schedule (NextAttemptAt null) remains claimable");
		claimable.ShouldNotContain(
			scheduled.Id, "the only reason the scheduled message is excluded is its NextAttemptAt — not its status");
	}

	private static async Task<bool> PollUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, TimeSpan pollInterval)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (await condition().ConfigureAwait(false))
			{
				return true;
			}

			await Task.Delay(pollInterval).ConfigureAwait(false);
		}

		return await condition().ConfigureAwait(false);
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
		var options = new SqlServerOutboxOptions
		{
			ConnectionString = _connectionString!,
			SchemaName = "dbo",
			OutboxTableName = "OutboxMessages",
			TransportsTableName = "OutboxTransports",
			CommandTimeoutSeconds = 30
		};

		return new SqlServerOutboxStore(Options.Create(options), NullLogger<SqlServerOutboxStore>.Instance);
	}

	private async Task ClearAllMessagesAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

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

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using var createOutbox = new SqlCommand(createOutboxTableSql, connection);
		_ = await createOutbox.ExecuteNonQueryAsync().ConfigureAwait(false);

		await using var createTransports = new SqlCommand(createTransportsTableSql, connection);
		_ = await createTransports.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
