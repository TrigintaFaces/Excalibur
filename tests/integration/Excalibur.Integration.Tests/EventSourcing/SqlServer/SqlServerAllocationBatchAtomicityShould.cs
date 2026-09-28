// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Tests.Shared.Infrastructure;
using System.Data;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// The allocate-and-insert batch is ATOMIC: a run-time error in its <c>INSERT</c> cannot leave the
/// position counter advanced.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this cannot be tested through the store.</b> A BATCH IS NOT A STATEMENT. With SQL Server's
/// default <c>XACT_ABORT OFF</c>, an error in one statement of a batch aborts only that statement and
/// leaves the earlier ones applied inside the still-open transaction. The batch here reserves a block of
/// global positions with an <c>UPDATE</c> and then writes the rows that consume it; if the <c>INSERT</c>
/// fails and the transaction is nonetheless committed, the reserved block is gone from the committed
/// sequence forever and every projection downstream stalls or silently skips it.
/// </para>
/// <para>
/// The store's own callers always roll back on that error, so the defect is UNREACHABLE through
/// <c>AppendAsync</c> — which is precisely what made it dangerous: the invariant held as a property of
/// the callers rather than of the statement, and a future caller that committed would have reintroduced
/// permanent event loss with nothing failing. Asserting it therefore means driving the request's own SQL
/// and committing where the store would not.
/// </para>
/// <para>
/// <b>The error is the EXPECTED one, not an exotic fault.</b> The <c>INSERT</c> is made to violate the
/// stream unique key, which is the ordinary outcome of a lost optimistic-concurrency race — the single
/// most likely error this batch will ever raise in production.
/// </para>
/// <para>
/// RED by construction: delete <c>SET XACT_ABORT ON</c> from
/// <see cref="AllocateAndInsertEventsRequest"/> and the commit below succeeds with the counter advanced.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "EventStore")]
public sealed class SqlServerAllocationBatchAtomicityShould : IAsyncLifetime
{
	private const string Schema = "dbo";
	private const string Table = "AllocBatchEvents";

	private MsSqlContainer? _container;
	private string? _connectionString;
	private readonly RequiredContainer _requiredContainer = new("SQL Server (Docker)");

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithBoundedMemory()
				.WithImage(TestContainerImages.SqlServer2022)
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_connectionString = _container.GetConnectionString();
			_requiredContainer.MarkStarted();

			await CreateSchemaAsync().ConfigureAwait(false);
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

	[Fact]
	public async Task NotAdvanceTheCounterWhenTheInsertInTheSameBatchFails()
	{
		_requiredContainer.Require();

		var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

		// Seed version 0 for this stream, so the second append below collides on the unique key.
		await RunBatchAsync(aggregateId, version: 0, commit: true).ConfigureAwait(false);

		var counterBefore = await ReadCounterAsync().ConfigureAwait(false);

		// SAFETY -- the same (AggregateId, AggregateType, Version) again. The UPDATE inside the batch
		// applies, then the INSERT violates the unique key. We then COMMIT anyway, which is the thing the
		// store never does and a future caller might.
		var committed = await RunBatchAsync(aggregateId, version: 0, commit: true).ConfigureAwait(false);

		committed.ShouldBeFalse(
			"the unique-key violation must doom the transaction, so the commit cannot succeed");

		(await ReadCounterAsync().ConfigureAwait(false)).ShouldBe(
			counterBefore,
			"a failed INSERT must not leave the position counter advanced. If it does, the reserved block "
			+ "is absent from the committed sequence permanently and every global-stream subscriber either "
			+ "stalls on the hole or silently skips the events after it. RED when SET XACT_ABORT ON is "
			+ "removed from the request.");
	}

	[Fact]
	public async Task StillAllocateAndInsertWhenTheBatchSucceeds()
	{
		_requiredContainer.Require();

		// LIVENESS -- proves the arm above is not passing because the batch never works. A store that
		// refused every append would satisfy "the counter never advances" trivially.
		var counterBefore = await ReadCounterAsync().ConfigureAwait(false);
		var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

		var committed = await RunBatchAsync(aggregateId, version: 0, commit: true).ConfigureAwait(false);

		committed.ShouldBeTrue("an uncontended append must commit");
		(await ReadCounterAsync().ConfigureAwait(false)).ShouldBe(
			counterBefore + 1, "a committed single-event append must advance the counter by exactly one");
		(await CountRowsAsync(aggregateId).ConfigureAwait(false)).ShouldBe(1, "the event row must persist");
	}

	/// <summary>
	/// Issues the real <see cref="AllocateAndInsertEventsRequest"/> and optionally commits, returning
	/// whether the commit succeeded. The production request is used rather than a copy of its SQL, so the
	/// assertion cannot drift away from the statement it is protecting.
	/// </summary>
	private async Task<bool> RunBatchAsync(string aggregateId, long version, bool commit)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var transaction = (SqlTransaction)await connection
			.BeginTransactionAsync(IsolationLevel.ReadCommitted).ConfigureAwait(false);

		var row = new EventInsertRow(
			EventId: Guid.NewGuid().ToString("N"),
			AggregateId: aggregateId,
			AggregateType: "TestAggregate",
			EventType: "TestEvent",
			EventData: [1, 2, 3],
			Metadata: null,
			Version: version,
			Timestamp: DateTimeOffset.UtcNow);

		try
		{
			_ = await connection.ResolveAsync(
					new AllocateAndInsertEventsRequest(
						[row],
						totalEventCount: 1,
						transaction,
						TenantScope.Untenanted,
						CancellationToken.None,
						Schema,
						Table,
						Table + "Position"))
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is SqlException or OperationFailedException)
		{
			// Expected on the colliding arm. Fall through and still attempt the commit, because whether
			// the commit can succeed after a failed statement IS the property under test.
		}

		if (!commit)
		{
			return false;
		}

		try
		{
			await transaction.CommitAsync().ConfigureAwait(false);
			return true;
		}
		catch (Exception ex) when (ex is SqlException or InvalidOperationException)
		{
			// A doomed or already-rolled-back transaction. Either is the correct outcome.
			return false;
		}
	}

	private async Task<long> ReadCounterAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand(
			$"SELECT Value FROM [{Schema}].[{Table}Position] WHERE Id = 1;", connection);
		return Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false));
	}

	private async Task<int> CountRowsAsync(string aggregateId)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand(
			$"SELECT COUNT(*) FROM [{Schema}].[{Table}] WHERE AggregateId = @a;", connection);
		_ = command.Parameters.AddWithValue("@a", aggregateId);
		return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
	}

	private async Task CreateSchemaAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		const string Sql = $"""
			IF OBJECT_ID(N'[{Schema}].[{Table}]', 'U') IS NULL
			CREATE TABLE [{Schema}].[{Table}] (
				[Position]      BIGINT NOT NULL CONSTRAINT [PK_{Table}] PRIMARY KEY CLUSTERED,
				[EventId]       NVARCHAR(255) NOT NULL,
				[AggregateId]   NVARCHAR(255) NOT NULL,
				[AggregateType] NVARCHAR(255) NOT NULL,
				[EventType]     NVARCHAR(255) NOT NULL,
				[EventData]     VARBINARY(MAX) NULL,
				[Metadata]      VARBINARY(MAX) NULL,
				[Version]       BIGINT NOT NULL,
				[Timestamp]     DATETIMEOFFSET NOT NULL,
				[ArchivedAt]    DATETIMEOFFSET NULL,
				[TenantId]      NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
					CONSTRAINT [DF_{Table}_TenantId] DEFAULT '__untenanted__',
				CONSTRAINT [UQ_{Table}_Stream] UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId])
			);

			IF OBJECT_ID(N'[{Schema}].[{Table}Position]', 'U') IS NULL
			CREATE TABLE [{Schema}].[{Table}Position] (
				[Id]    TINYINT NOT NULL CONSTRAINT [PK_{Table}Position] PRIMARY KEY,
				[Value] BIGINT  NOT NULL,
				CONSTRAINT [CK_{Table}Position_Singleton] CHECK ([Id] = 1)
			);

			IF NOT EXISTS (SELECT 1 FROM [{Schema}].[{Table}Position] WHERE [Id] = 1)
			INSERT INTO [{Schema}].[{Table}Position] ([Id], [Value]) VALUES (1, 0);
			""";

#pragma warning disable CA2100 // Schema and table are compile-time constants in this test
		await using var command = new SqlCommand(Sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
