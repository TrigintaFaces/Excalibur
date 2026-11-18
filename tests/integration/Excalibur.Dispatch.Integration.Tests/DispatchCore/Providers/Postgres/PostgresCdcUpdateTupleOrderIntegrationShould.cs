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
/// Locks that an UPDATE on a <c>REPLICA IDENTITY FULL</c> table surfaces BOTH the old and the new column
/// values, which requires reading the replication tuples in wire order.
/// </summary>
/// <remarks>
/// <para>
/// A pgoutput update carries the old tuple before the new one, and Npgsql exposes both over a FORWARD-ONLY
/// stream: <c>FullUpdateMessage.NewRow</c> is a second-row enumerable constructed over the old-row tuple,
/// which it drains to reach its own. Reading the new row first therefore consumes the old row, and the old
/// read that follows has nothing left to enumerate. The same holds for <c>IndexUpdateMessage</c>, whose
/// <c>NewRow</c> is built over its <c>Key</c> tuple.
/// </para>
/// <para>
/// <b>Why this arm had to be written rather than assumed.</b> Four Postgres CDC integration suites already
/// set <c>REPLICA IDENTITY FULL</c>, and not one of them issues an UPDATE — measured, zero UPDATE statements
/// across all four. So the update handlers were reached by no test at all, and a reversed read order was
/// invisible to a fully green suite. An arm that only inserted would have passed either way.
/// </para>
/// <para>
/// <b>What makes it non-vacuous.</b> The assertion is on the OLD value, which exists only if the old tuple
/// was enumerated before the new one was requested. Swap the two reads in
/// <c>PostgresCdcProcessor.HandleFullUpdateAsync</c> and this arm reddens; the new value is asserted
/// alongside it as the liveness half, so an arm that received no update at all fails on the control rather
/// than appearing to pass.
/// </para>
/// <para>
/// Requires a real wal_level=logical Postgres and is never skipped.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Data.Postgres")]
public sealed class PostgresCdcUpdateTupleOrderIntegrationShould : IntegrationTestBase
{
	private static readonly TimeSpan StreamWindow = TimeSpan.FromSeconds(10);

	private readonly PostgresFixture _pgFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcUpdateTupleOrderIntegrationShould"/> class.
	/// </summary>
	/// <param name="pgFixture">The shared Postgres container fixture.</param>
	public PostgresCdcUpdateTupleOrderIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	/// <summary>
	/// An update on a REPLICA IDENTITY FULL table delivers the old value as well as the new one.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task DeliverBothTheOldAndTheNewValue_WhenAFullReplicaIdentityRowIsUpdated()
	{
		_pgFixture.DockerAvailable.ShouldBeTrue(
			"the update tuple order lives in the pgoutput message encoding, which no mock can exhibit, so "
			+ "this arm requires a real wal_level=logical container and is never skipped.");

		var connectionString = _pgFixture.ConnectionString;
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var tableName = $"cdc_upd_{suffix}";
		var publicationName = $"cdc_upd_pub_{suffix}";
		var slotName = $"cdc_upd_slot_{suffix}";
		var schemaName = $"cdc_upd_{suffix}";
		var processorId = $"pg-update-order-{suffix}";

		await using (var conn = new NpgsqlConnection(connectionString))
		{
			await conn.OpenAsync(TestCancellationToken);
			await conn.ExecuteAsync($"CREATE TABLE {tableName} (id int PRIMARY KEY, order_id text NOT NULL);");

			// FULL is what produces a FullUpdateMessage carrying an old tuple at all. With the default
			// replica identity the old row is absent and the handler under test is never exercised.
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
			// The slot must exist before the changes so it retains them.
			await using (var slotInit = NewProcessor())
			{
				await DriveForWindowAsync(slotInit, static (_, _) => Task.CompletedTask);
			}

			await using (var conn = new NpgsqlConnection(connectionString))
			{
				await conn.OpenAsync(TestCancellationToken);
				await conn.ExecuteAsync($"INSERT INTO {tableName} (id, order_id) VALUES (1, 'before');");
				await conn.ExecuteAsync($"UPDATE {tableName} SET order_id = 'after' WHERE id = 1;");
			}

			var oldValues = new ConcurrentQueue<string>();
			var newValues = new ConcurrentQueue<string>();

			Task Handler(PostgresDataChangeEvent change, CancellationToken ct)
			{
				if (change.ChangeType == PostgresDataChangeType.Update)
				{
					foreach (var column in change.Changes.Where(c => c.ColumnName == "order_id"))
					{
						if (column.OldValue?.ToString() is { Length: > 0 } old)
						{
							oldValues.Enqueue(old);
						}

						if (column.NewValue?.ToString() is { Length: > 0 } updated)
						{
							newValues.Enqueue(updated);
						}
					}
				}

				return Task.CompletedTask;
			}

			await using (var processor = NewProcessor())
			{
				await DriveForWindowAsync(processor, Handler);
			}

			// LIVENESS / CONTROL first: without this an arm that received no update at all would fail on the
			// old-value assertion and read as the tuple-order defect rather than as a stream that never ran.
			newValues.ShouldContain(
				"after",
				"control: the update must have been delivered at all. No new value means the replication "
				+ "stream did not run or the publication did not carry this table, which is a different "
				+ "failure from the one this arm is about.");

			// THE PROPERTY: the old tuple is only readable if it was enumerated BEFORE the new row was
			// requested. Reading the new row first drains it, and this comes back empty.
			oldValues.ShouldContain(
				"before",
				"the pre-update value was not delivered. Npgsql serves the old and new tuples over one "
				+ "forward-only stream and builds NewRow over the old-row tuple, so requesting the new row "
				+ "first consumes the old one and leaves nothing to enumerate. Read the old tuple first.");
		}
		finally
		{
			await CleanupAsync(connectionString, tableName, publicationName, slotName, schemaName);
			await stateStore.DisposeAsync();
		}
	}

	private async Task DriveForWindowAsync(
		PostgresCdcProcessor processor,
		Func<PostgresDataChangeEvent, CancellationToken, Task> handler)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
		cts.CancelAfter(StreamWindow);

		for (var attempt = 0; attempt < 3 && !cts.Token.IsCancellationRequested; attempt++)
		{
			try
			{
				_ = await processor.ProcessBatchAsync(handler, cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
			{
				// Unbounded stream: a batch with fewer than BatchSize changes ends via the token.
			}
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
			// Best-effort cleanup; never mask the test's own outcome.
		}
	}
}
