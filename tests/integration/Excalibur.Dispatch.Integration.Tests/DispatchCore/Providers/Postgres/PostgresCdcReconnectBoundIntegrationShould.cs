// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Dapper;

using Excalibur.Cdc;
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
/// On a real server, a change whose handler keeps failing stops the processor once the configured limit is
/// reached, even when the transaction before it is confirmed again on every reconnect.
/// </summary>
/// <remarks>
/// <para>
/// The interleaving this binds exists only on a real server: the processor records a confirmed position
/// locally, but the slot's confirmed position reaches the server on a later status update, so a reconnect can
/// re-send a transaction that was already confirmed. If re-confirming it counted as progress, the failure
/// count would reset on every attempt and the limit would never be reached, while the earlier transaction is
/// delivered again each time. A mock cannot keep the slot behind the local position, so this arm cannot be a
/// unit test.
/// </para>
/// <para>
/// Whether the server actually re-sends the earlier transaction is recorded, not assumed: when it does not,
/// this arm still proves the limit stops the processor, and says nothing about the re-confirmation rule.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait("Database", "Postgres")]
[Trait("SubComponent", "ReconnectBound")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PostgresCdcReconnectBoundIntegrationShould : IntegrationTestBase
{
	private static readonly TimeSpan BatchWindow = TimeSpan.FromSeconds(5);

	private readonly PostgresFixture _pgFixture;

	public PostgresCdcReconnectBoundIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	[Fact]
	public async Task StopAtTheLimit_WhenTheSameChangeKeepsFailing_EvenIfTheTransactionBeforeItIsConfirmedAgain()
	{
		_pgFixture.DockerAvailable.ShouldBeTrue(
			"the reconnect-bound lock needs a real wal_level=logical server and is never skipped.");

		var connectionString = _pgFixture.ConnectionString;
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var tableName = $"cdc_bound_{suffix}";
		var publicationName = $"cdc_pub_{suffix}";
		var slotName = $"cdc_slot_{suffix}";
		var schemaName = $"cdc_{suffix}";

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
				ProcessorId = "pg-reconnect-bound",
				TableNames = [tableName],
				BatchSize = 10,
				PollingInterval = TimeSpan.FromMilliseconds(100),
				Replication = new PostgresCdcReplicationOptions { AutoCreateSlot = true },
			}),
			stateStore,
			NullLogger<PostgresCdcProcessor>.Instance,
			MsOptions.Create(new CdcFatalErrorOptions<PostgresDataChangeEvent>
			{
				MaxConsecutiveTransientFailures = 3,
				// Longer than a real reconnect-and-replay takes, so every failing attempt counts; the backoff before the
				// third failure is still only 100 + 200 ms.
				MaxReconnectDelay = TimeSpan.FromSeconds(10),
			}));

		try
		{
			// Create the slot before the inserts so it retains them.
			await using (var slotInit = NewProcessor())
			{
				await RunBoundedBatchAsync(slotInit);
			}

			// Two transactions: A is always handled; B's handler always fails with an unrecognised (transient) error.
			await InsertOrderAsync(connectionString, tableName, 1, "order-A");
			await InsertOrderAsync(connectionString, tableName, 2, "order-B");

			var deliveriesOfA = 0;
			var attemptsOnB = 0;
			await using var processor = NewProcessor();
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(90));

			var thrown = await Should.ThrowAsync<CdcRetryExhaustedException>(() => processor.StartAsync(
				(change, ct) =>
				{
					var orderId = change.Changes.FirstOrDefault(c => c.ColumnName == "order_id")?.NewValue?.ToString();
					if (orderId == "order-A")
					{
						_ = Interlocked.Increment(ref deliveriesOfA);
						return Task.CompletedTask;
					}

					_ = Interlocked.Increment(ref attemptsOnB);
					throw new TimeoutException("the downstream system did not answer");
				},
				timeout.Token));

			timeout.IsCancellationRequested.ShouldBeFalse("the processor must stop by itself at the limit, not by timeout");
			thrown.ConsecutiveFailures.ShouldBe(3);
			_ = thrown.InnerException.ShouldBeOfType<TimeoutException>();
			attemptsOnB.ShouldBe(3, "one failing attempt per reconnect, stopping on the third");
			deliveriesOfA.ShouldBeGreaterThanOrEqualTo(1);

			// Recorded so a reader can tell which interleaving the server produced on this run.
			Console.WriteLine($"[reconnect-bound] order-A delivered {deliveriesOfA} time(s) across 3 attempts on order-B.");
		}
		finally
		{
			await CleanupAsync(connectionString, tableName, publicationName, slotName, schemaName);
			await stateStore.DisposeAsync();
		}
	}

	private async Task InsertOrderAsync(string connectionString, string tableName, int id, string orderId)
	{
		await using var conn = new NpgsqlConnection(connectionString);
		await conn.OpenAsync(TestCancellationToken);
		await conn.ExecuteAsync($"INSERT INTO {tableName} (id, order_id) VALUES (@id, @orderId);", new { id, orderId });
	}

	private async Task RunBoundedBatchAsync(PostgresCdcProcessor processor)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
		cts.CancelAfter(BatchWindow);
		try
		{
			_ = await processor.ProcessBatchAsync((_, _) => Task.CompletedTask, cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!TestCancellationToken.IsCancellationRequested)
		{
			// The batch window elapsed with nothing to read, which is expected while creating the slot.
		}
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
			// Best-effort cleanup; never mask the arm's own outcome.
		}
	}
}
