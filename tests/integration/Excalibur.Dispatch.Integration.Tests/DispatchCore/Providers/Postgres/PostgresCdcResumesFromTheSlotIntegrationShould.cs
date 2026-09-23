// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Dapper;

using Excalibur.Cdc.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Locks that BOTH Postgres replication loops resume from the replication slot, never from the state-store row.
/// </summary>
/// <remarks>
/// <para>
/// PostgreSQL starts a logical replication stream at the requested LSN or the slot's confirmed_flush_lsn,
/// <b>whichever is greater</b>. So passing the state-store row as the start LSN can only ever move the start
/// FORWARD of the slot, and when the row is ahead of a change that was never delivered, that change is skipped
/// and never redelivered. The slot's confirmed_flush_lsn advances only from the flush position this processor
/// reports, which it sets only after a transaction's changes were handed off, so the slot is the one position
/// that cannot run ahead of delivery.
/// </para>
/// <para>
/// Each arm plants a state-store row AHEAD of an undelivered change and asserts the change is still delivered.
/// The row is planted directly because the property under test is "the row is not a resume authority": whatever
/// puts the row ahead, it must not be able to cost a change. A second change, inserted after the plant, is the
/// control that the stream was actually running; without it an arm that delivered nothing would fail on the
/// wrong assertion and look like the defect.
/// </para>
/// <para>
/// Requires a real wal_level=logical Postgres and is never skipped.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Data.Postgres")]
public sealed class PostgresCdcResumesFromTheSlotIntegrationShould : IntegrationTestBase
{
	private static readonly TimeSpan StreamWindow = TimeSpan.FromSeconds(5);

	private readonly PostgresFixture _pgFixture;

	public PostgresCdcResumesFromTheSlotIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	/// <summary>
	/// The CONTINUOUS loop (StartAsync) delivers a change that sits behind a state-store row planted ahead of it.
	/// </summary>
	[Fact]
	public Task DeliverAChangeBehindAPlantedRow_OnTheContinuousLoop() =>
		RunAsync(static async (processor, handler, token) =>
		{
			try
			{
				await processor.StartAsync(handler, token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				// The continuous loop runs until cancelled; the window elapsing is the normal end.
			}
		});

	/// <summary>
	/// The BATCH loop (ProcessBatchAsync) delivers the same change. Locked alongside the continuous loop so the two
	/// are bound to one resume source: a change that reintroduced a row-driven start on either reddens.
	/// </summary>
	[Fact]
	public Task DeliverAChangeBehindAPlantedRow_OnTheBatchLoop() =>
		RunAsync(static async (processor, handler, token) =>
		{
			for (var attempt = 0; attempt < 3 && !token.IsCancellationRequested; attempt++)
			{
				await BatchOnceAsync(processor, handler, token).ConfigureAwait(false);
			}
		});

	private async Task RunAsync(
		Func<PostgresCdcProcessor, Func<PostgresDataChangeEvent, CancellationToken, Task>, CancellationToken, Task> drive)
	{
		_pgFixture.DockerAvailable.ShouldBeTrue(
			"the resume-authority lock requires a real wal_level=logical container and is never skipped.");

		var connectionString = _pgFixture.ConnectionString;
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var tableName = $"cdc_resume_{suffix}";
		var publicationName = $"cdc_pub_{suffix}";
		var slotName = $"cdc_slot_{suffix}";
		var schemaName = $"cdc_{suffix}";
		const string processorId = "pg-resume-authority";

		await using (var conn = new NpgsqlConnection(connectionString))
		{
			await conn.OpenAsync(TestCancellationToken);
			await conn.ExecuteAsync($"CREATE TABLE {tableName} (id int PRIMARY KEY, order_id text NOT NULL);");
			await conn.ExecuteAsync($"ALTER TABLE {tableName} REPLICA IDENTITY FULL;");
			await conn.ExecuteAsync($"CREATE PUBLICATION {publicationName} FOR TABLE {tableName};");
		}

		var stateStore = new PostgresCdcStateStore(
			connectionString,
			MsOptions.Create(new PostgresCdcStateStoreOptions { SchemaName = schemaName, TableName = "state" }));

		PostgresCdcProcessor NewProcessor() => new(
			MsOptions.Create(new PostgresCdcOptions
			{
				ConnectionString = connectionString,
				PublicationName = publicationName,
				ReplicationSlotName = slotName,
				ProcessorId = processorId,
				TableNames = [tableName],
				BatchSize = 10,
				Replication = new PostgresCdcReplicationOptions { AutoCreateSlot = true },
			}),
			stateStore,
			NullLogger<PostgresCdcProcessor>.Instance);

		try
		{
			// The slot must exist BEFORE the first change so it retains it. Creating it delivers nothing.
			await using (var slotInit = NewProcessor())
			{
				await DriveForWindowAsync(slotInit, static (_, _) => Task.CompletedTask, BatchOnceAsync);
			}

			// A change that is never delivered before the plant.
			await InsertOrderAsync(connectionString, tableName, 1, "behind-the-row");

			// Plant the state-store row AHEAD of that change. pg_current_wal_lsn() read straight after the insert is
			// NOT reliably past the transaction's commit (measured: it can equal the position the change is later
			// delivered at, and a plant there skips nothing, so the arm could not fail). Switching to a new WAL
			// segment and planting its start puts the row unambiguously after every record the change wrote.
			string walNow;
			await using (var conn = new NpgsqlConnection(connectionString))
			{
				await conn.OpenAsync(TestCancellationToken);
				_ = await conn.ExecuteScalarAsync<string>("SELECT pg_switch_wal()::text;");
				walNow = await conn.ExecuteScalarAsync<string>("SELECT pg_current_wal_insert_lsn()::text;") ?? string.Empty;
			}

			await stateStore.SavePositionAsync(processorId, slotName, new PostgresCdcPosition(walNow), TestCancellationToken);

			// CONTROL: a change after the plant, which every resume source would deliver.
			await InsertOrderAsync(connectionString, tableName, 2, "after-the-row");

			var seen = new ConcurrentQueue<string>();
			Task Handler(PostgresDataChangeEvent change, CancellationToken ct)
			{
				if (change.ChangeType == PostgresDataChangeType.Insert
					&& change.Changes.FirstOrDefault(c => c.ColumnName == "order_id")?.NewValue?.ToString() is { } id)
				{
					seen.Enqueue(id);
				}

				return Task.CompletedTask;
			}

			await using (var processor = NewProcessor())
			{
				await DriveForWindowAsync(processor, Handler, drive);
			}

			seen.ShouldContain(
				"after-the-row",
				"control: the stream must have run and delivered the change inserted after the plant");
			seen.ShouldContain(
				"behind-the-row",
				"the change behind the planted row was never delivered; resuming from the row instead of the slot skipped it");
		}
		finally
		{
			await CleanupAsync(connectionString, tableName, publicationName, slotName, schemaName);
			await stateStore.DisposeAsync();
		}
	}

	private static async Task BatchOnceAsync(
		PostgresCdcProcessor processor,
		Func<PostgresDataChangeEvent, CancellationToken, Task> handler,
		CancellationToken token)
	{
		try
		{
			_ = await processor.ProcessBatchAsync(handler, token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			// Unbounded stream: a batch with fewer than BatchSize changes ends via the token.
		}
	}

	private async Task DriveForWindowAsync(
		PostgresCdcProcessor processor,
		Func<PostgresDataChangeEvent, CancellationToken, Task> handler,
		Func<PostgresCdcProcessor, Func<PostgresDataChangeEvent, CancellationToken, Task>, CancellationToken, Task> drive)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
		cts.CancelAfter(StreamWindow);
		await drive(processor, handler, cts.Token).ConfigureAwait(false);
	}

	private async Task InsertOrderAsync(string connectionString, string tableName, int id, string orderId)
	{
		await using var conn = new NpgsqlConnection(connectionString);
		await conn.OpenAsync(TestCancellationToken);
		await conn.ExecuteAsync($"INSERT INTO {tableName} (id, order_id) VALUES (@id, @orderId);", new { id, orderId });
	}

	private async Task CleanupAsync(
		string connectionString, string tableName, string publicationName, string slotName, string schemaName)
	{
		try
		{
			await using var conn = new NpgsqlConnection(connectionString);
			await conn.OpenAsync(TestCancellationToken);
			await conn.ExecuteAsync($"DROP PUBLICATION IF EXISTS {publicationName};");
			await conn.ExecuteAsync(
				"SELECT pg_drop_replication_slot(slot_name) FROM pg_replication_slots WHERE slot_name = @slotName;",
				new { slotName });
			await conn.ExecuteAsync($"DROP TABLE IF EXISTS {tableName};");
			await conn.ExecuteAsync($"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE;");
		}
		catch
		{
			// Best-effort cleanup; never mask the test's own outcome.
		}
	}
}
