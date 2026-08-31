// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Tests.Shared.Fixtures;

using Excalibur.Dispatch;

using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Sprint 849 / Lane R2 (<c>o045q4</c>) — author≠impl behavioral lock for SQL outbox ordering persistence.
/// </summary>
/// <remarks>
/// <para>
/// The advertised contract (<see cref="OutboundMessage.PartitionKey"/>/<see cref="OutboundMessage.GroupKey"/>/
/// <see cref="OutboundMessage.SequenceNumber"/>, documented on the Abstractions type) promises that messages
/// sharing a <c>PartitionKey</c> are delivered in ascending <c>SequenceNumber</c>. Pre-fix the SQL outbox store
/// neither persisted those columns nor applied an <c>ORDER BY</c> on the claim, so the guarantee was decorative.
/// </para>
/// <para>
/// These are the RED-on-pre-fix locks for AC-R2.1/AC-R2.2/AC-R2.3:
/// <list type="bullet">
/// <item>AC-R2.1 — round-trip persist→read of all three ordering columns (pre-fix: <c>MapRowToMessage</c> never
/// read them → <c>null</c>/<c>0</c>).</item>
/// <item>AC-R2.2 — same-partition out-of-order staging is claimed in ascending <c>SequenceNumber</c> (pre-fix:
/// no <c>ORDER BY</c> on the claim).</item>
/// <item>AC-R2.3 — partition ordering is restored on re-claim after a batch failure.</item>
/// </list>
/// Real SQL Server via TestContainers (run serially under <c>-m:1</c>), mirroring the existing
/// <c>SqlServerOutboxStoreIntegrationShould</c> infrastructure.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "OutboxStore")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class OutboxOrderingPersistenceIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _connectionString;
	private readonly RequiredContainer _requiredContainer = new("SQL Server (Docker)");

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithBoundedMemory()
				.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_connectionString = _container.GetConnectionString();
			_requiredContainer.MarkStarted();

			await InitializeDatabaseAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw _requiredContainer.Failed(ex);
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
	/// AC-R2.1 — the three ordering columns survive a persist→read round-trip. RED pre-fix: the store mapped
	/// none of them back, so a claimed message reported <c>PartitionKey == null</c> / <c>SequenceNumber == 0</c>.
	/// </summary>
	[Fact]
	public async Task PersistAndRoundTripTheOrderingColumns()
	{
		_requiredContainer.Require();

		await ClearAllMessagesAsync();
		var store = CreateOutboxStore();

		var message = CreateTestMessage();
		message.PartitionKey = "round-trip-partition";
		message.GroupKey = "round-trip-group";
		message.SequenceNumber = 4242;

		await store.StageMessageAsync(message, CancellationToken.None);

		var claimed = (await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.Single(m => m.Id == message.Id);

		claimed.PartitionKey.ShouldBe("round-trip-partition");
		claimed.GroupKey.ShouldBe("round-trip-group");
		claimed.SequenceNumber.ShouldBe(4242);
	}

	/// <summary>
	/// AC-R2.2 — same-partition messages staged out of order are claimed in ascending <c>SequenceNumber</c>.
	/// RED pre-fix: the claim had no <c>ORDER BY</c>, so the returned order was arbitrary (insertion/clustered).
	/// </summary>
	[Fact]
	public async Task ClaimSamePartitionMessagesInAscendingSequenceOrder()
	{
		_requiredContainer.Require();

		await ClearAllMessagesAsync();
		var store = CreateOutboxStore();

		const string partition = "ordered-partition";

		// Stage three messages for the SAME partition with sequence numbers in DESCENDING insertion order
		// (30, 10, 20) so insertion order != sequence order — the ORDER BY is the only thing that can fix it.
		foreach (var seq in new long[] { 30, 10, 20 })
		{
			var m = CreateTestMessage();
			m.PartitionKey = partition;
			m.SequenceNumber = seq;
			await store.StageMessageAsync(m, CancellationToken.None);
		}

		var claimedForPartition = (await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.Where(m => m.PartitionKey == partition)
			.Select(m => m.SequenceNumber)
			.ToList();

		claimedForPartition.ShouldBe(new long[] { 10, 20, 30 });
	}

	/// <summary>
	/// AC-R2.3 — partition ordering is restored when failed messages are re-claimed. The first claim leases the
	/// batch; the messages are then failed with an ALREADY-ELAPSED next-attempt schedule; with the lease expired
	/// they are re-claimed, and must come back in ascending <c>SequenceNumber</c>. RED pre-fix: no claim ordering.
	/// <para>
	/// The failure is recorded through <c>MarkFailedWithBackoffAsync</c> with an elapsed <c>nextAttemptAt</c>
	/// rather than the plain <c>MarkFailedAsync</c>. The plain path applies the R1 failure-backoff FLOOR
	/// (<c>Processing.FailureBackoffFloorSeconds</c>, default 30s), which is a SECOND re-claim gate independent
	/// of the lease — so <c>leaseTimeoutSeconds: 0</c> alone does not make the rows re-claimable and this test
	/// would observe an empty batch. The elapsed schedule does not BYPASS that gate — no configuration can,
	/// because the store always supplies a floor and the two are always composed. It satisfies the gate: the
	/// column is written as the later of the caller's delay and F, which with the floor at zero resolves to
	/// the server's current instant, so the rows are due on the next claim with no wall-clock wait. What
	/// isolates the property under test (claim ORDERING) is that both rows clear the gate at the same
	/// instant, leaving <c>SequenceNumber</c> as the only thing that can order them.
	/// </para>
	/// </summary>
	[Fact]
	public async Task RestorePartitionOrderingOnReclaimAfterFailure()
	{
		_requiredContainer.Require();

		await ClearAllMessagesAsync();

		// LeaseTimeoutSeconds = 0 so the first claim's lease is immediately expired and the failed messages
		// are re-claimable without a wall-clock wait (deterministic).
		var store = CreateOutboxStore(leaseTimeoutSeconds: 0);

		const string partition = "reclaim-partition";
		var staged = new List<string>();
		foreach (var seq in new long[] { 3, 1, 2 })
		{
			var m = CreateTestMessage();
			m.PartitionKey = partition;
			m.SequenceNumber = seq;
			await store.StageMessageAsync(m, CancellationToken.None);
			staged.Add(m.Id);
		}

		// First claim (leases the rows), then simulate a batch delivery failure.
		var firstClaim = (await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.Where(m => m.PartitionKey == partition)
			.ToList();
		firstClaim.Count.ShouldBe(3);
		// The floor is stamped from the TEST HOST's clock and judged by the DATABASE SERVER's, and those are
		// two different clocks: a containerised SQL Server's clock stalls under load and then catches up.
		// Measured on this machine by a sibling suite, sampling a container's clock across fifty host-side
		// 2.5s waits, six advanced by only 345-541ms. One second of margin is inside that error, so a row
		// whose floor is "one second ago" on the host can still read as future-dated on the server and be
		// withheld from the re-claim -- which is exactly the intermittent this arm produced ([1,2] instead
		// of [1,2,3]). Minutes of margin cost nothing here because this arm asserts claim ORDERING, not the
		// due-gate boundary; the sibling scheduled-delivery arm in this same file already uses AddMinutes(-1)
		// for the same reason. Widening the margin does not soften the assertion: a row that is genuinely
		// not due, or that is claimed out of sequence, still fails.
		var elapsed = DateTimeOffset.UtcNow.AddMinutes(-5);
		foreach (var id in staged)
		{
			await store.MarkFailedWithBackoffAsync(
				id, "simulated batch failure", 1, elapsed, CancellationToken.None);
		}

		// Re-claim: ordering must be restored (ascending sequence) for the failed partition.
		var reclaimed = (await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.Where(m => m.PartitionKey == partition)
			.Select(m => m.SequenceNumber)
			.ToList();

		reclaimed.ShouldBe(new long[] { 1, 2, 3 });
	}

	/// <summary>
	/// AC-R2.1 (full extent) — the ordering columns also round-trip through the failed-retry and scheduled
	/// delivery read paths, not only the primary claim. Both <c>GetAllTenantsFailedMessagesAsync</c> and
	/// <c>GetAllTenantsScheduledMessagesAsync</c> hydrate the SAME <c>OutboxMessageRow</c> and feed (re-)delivery, so a
	/// round-trip that dropped the columns there would be a silent ordering inconsistency on a delivery path
	/// (SA ruling, OPCOM 15393 — KEEP in-lane). RED pre-fix: those request SELECTs did not project the columns,
	/// so the rows hydrated with <c>PartitionKey == null</c> / <c>SequenceNumber == 0</c>.
	/// </summary>
	[Fact]
	public async Task RoundTripOrderingColumnsThroughTheFailedAndScheduledReadPaths()
	{
		_requiredContainer.Require();

		await ClearAllMessagesAsync();
		var store = CreateOutboxStore();

		// Failed-retry read path: stage with ordering metadata, fail it, then read it back via GetFailed.
		var failed = CreateTestMessage();
		failed.PartitionKey = "failed-partition";
		failed.GroupKey = "failed-group";
		failed.SequenceNumber = 7;
		await store.StageMessageAsync(failed, CancellationToken.None);
		await store.MarkFailedAsync(failed.Id, "boom", 1, CancellationToken.None);

		var failedRead = (await store.GetAllTenantsFailedMessagesAsync(5, null, 50, CancellationToken.None))
			.Single(m => m.Id == failed.Id);
		failedRead.PartitionKey.ShouldBe("failed-partition");
		failedRead.GroupKey.ShouldBe("failed-group");
		failedRead.SequenceNumber.ShouldBe(7);

		// Scheduled delivery read path: a staged message with a past ScheduledAt is returned by GetScheduled.
		var scheduled = CreateTestMessage();
		scheduled.PartitionKey = "scheduled-partition";
		scheduled.GroupKey = "scheduled-group";
		scheduled.SequenceNumber = 9;
		scheduled.ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1);
		await store.StageMessageAsync(scheduled, CancellationToken.None);

		var scheduledRead = (await store.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.UtcNow, 50, CancellationToken.None))
			.Single(m => m.Id == scheduled.Id);
		scheduledRead.PartitionKey.ShouldBe("scheduled-partition");
		scheduledRead.GroupKey.ShouldBe("scheduled-group");
		scheduledRead.SequenceNumber.ShouldBe(9);
	}

	private static OutboundMessage CreateTestMessage()
	{
		return new OutboundMessage(
			$"TestMessage_{Guid.NewGuid():N}",
			System.Text.Encoding.UTF8.GetBytes("{\"test\": true}"),
			"test-destination",
			new Dictionary<string, object>(StringComparer.Ordinal));
	}

	private SqlServerOutboxStore CreateOutboxStore(int leaseTimeoutSeconds = 120)
	{
		var options = new SqlServerOutboxOptions
		{
			ConnectionString = _connectionString!,
			Tables =
			{
				SchemaName = "dbo",
				OutboxTableName = "OutboxMessages",
				TransportsTableName = "OutboxTransports",
			},
			Processing =
			{
				CommandTimeoutSeconds = 30,
				LeaseTimeoutSeconds = leaseTimeoutSeconds,
				// These arms exercise the NextAttemptAt gate, so the floor is set to zero rather than
				// left at its default, which would otherwise hold the message for 30s and mask what
				// they assert. Note what zero does and does not do: it does NOT take a no-floor code
				// path. The store always supplies this value, so the composed branch is always the one
				// taken, and the column receives the later of the caller's delay and F. At F = 0 that
				// resolves to "the later of the caller's delay and now", so an already-elapsed schedule
				// yields exactly now on the server clock -- due immediately, which is what these arms
				// need. Zero is also outside the option's documented range, and is used here only
				// because a direct store construction bypasses options validation.
				// The floor itself is covered by SqlServerOutboxBackoffFloorClampShould.
				FailureBackoffFloorSeconds = 0,
			},
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
				-- Read and written by every claim/mark path in SqlServerOutboxStore. Absent here, the
				-- suite failed with: Invalid column name 'FencingToken'.
				FencingToken BIGINT NULL,
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

		// SqlServerOutboxStore joins dbo.OutboxFence on every claim to enforce the fencing high-water
		// mark. Absent here, the suite failed with: Invalid object name 'dbo.OutboxFence'. Shape matches
		// the shipped 001_CreateOutboxSchema.sql and SqlServerOutboxStoreContainerFixture.
		const string createFenceTableSql = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OutboxFence' AND xtype='U')
			CREATE TABLE [dbo].[OutboxFence] (
				OutboxTable NVARCHAR(512) NOT NULL PRIMARY KEY,
				HighWaterToken BIGINT NOT NULL
			)
			""";

		await using var createFence = new SqlCommand(createFenceTableSql, connection);
		_ = await createFence.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
