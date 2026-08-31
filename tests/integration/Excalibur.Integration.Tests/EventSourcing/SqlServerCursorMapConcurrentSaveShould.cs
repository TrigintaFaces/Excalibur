// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.DeadLetter;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.EventSourcing;

/// <summary>
/// Real-SQL-Server concurrency lock for <see cref="SqlServerCursorMapStore.SaveCursorMapAsync"/>: two
/// projectors checkpointing the same cursor at once must both succeed.
/// </summary>
/// <remarks>
/// <para>
/// The save is a MERGE. Under READ COMMITTED, which is SQL Server's default, the read half of a MERGE
/// takes no lock that survives to the write half, so two sessions merging the same key can both evaluate
/// WHEN NOT MATCHED and both attempt the INSERT. The second gets a primary-key violation. The row is
/// never duplicated -- the shipped schema's primary key sees to that, and that is the safe direction to
/// fail -- but a checkpoint that should have been recorded is lost instead, and the projector either
/// reprocesses from an older position or surfaces the error to a host that has no idea what to do with it.
/// The MERGE therefore has to hold a range lock across both halves.
/// </para>
/// <para>
/// This cannot be established without a real server. The in-memory store has no MERGE and no isolation
/// level; a unit arm over it would pass whatever the SQL statement said.
/// </para>
/// <para>
/// Both arms are needed. The safety arm -- no save fails -- is satisfied by a store whose save silently
/// does nothing. The liveness arm reads the row back and requires it to hold one of the positions that
/// was actually written, so a save that swallowed its work, or a lock so broad it dropped writers, fails.
/// </para>
/// </remarks>
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCursorMapConcurrentSaveShould(SqlServerContainerFixture fixture)
{
	private const string ProjectionName = "ConcurrentSaveProjection";
	private const string TenantId = "tenant-concurrent";

	/// <summary>
	/// Savers per key. The race is between the read and the write half of one MERGE, so it needs several
	/// sessions arriving together rather than a long run.
	/// </summary>
	private const int ConcurrentSavers = 8;

	/// <summary>
	/// Distinct keys tried. The window is narrow, so one key is not enough to expose an unlocked MERGE
	/// reliably; a fresh key per round keeps every round a genuine first-insert race.
	/// </summary>
	private const int Rounds = 25;

	[Fact]
	public async Task RecordTheCheckpointWhenSeveralProjectorsSaveTheSameCursorAtOnce()
	{
		var connectionFactory = await CreateProvisionedFactoryAsync();
		var store = new SqlServerCursorMapStore(
			connectionFactory,
			NullLogger<SqlServerCursorMapStore>.Instance,
			new FixedCursorTenantContext(TenantId));

		var failures = new List<Exception>();

		for (var round = 0; round < Rounds; round++)
		{
			var streamId = $"stream-{Guid.NewGuid():N}";
			using var allSaversReady = new SemaphoreSlim(0, ConcurrentSavers);

			async Task SaveAsync(int position)
			{
				// Every saver waits on the same gate so they contend rather than queue behind each other.
				await allSaversReady.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

				try
				{
					await store.SaveCursorMapAsync(
						ProjectionName,
						new Dictionary<string, long>(StringComparer.Ordinal) { [streamId] = position },
						TestContext.Current.CancellationToken).ConfigureAwait(false);
				}
				catch (SqlException ex)
				{
					lock (failures)
					{
						failures.Add(ex);
					}
				}
			}

			var savers = Enumerable.Range(1, ConcurrentSavers).Select(SaveAsync).ToArray();
			_ = allSaversReady.Release(ConcurrentSavers);
			await Task.WhenAll(savers);

			// Liveness, checked every round: the contended save must have actually recorded something,
			// and it must be one of the positions a saver wrote.
			var stored = await ReadPositionAsync(connectionFactory, streamId);
			stored.ShouldNotBeNull(
				$"round {round}: {ConcurrentSavers} projectors checkpointed this cursor and no row exists, "
				+ "so every save was lost");
			stored.Value.ShouldBeInRange(
				1,
				ConcurrentSavers,
				$"round {round}: the stored position must be one a saver actually wrote");
		}

		failures.ShouldBeEmpty(
			$"{failures.Count} of {Rounds * ConcurrentSavers} concurrent saves failed. A MERGE that does "
			+ "not hold a range lock across its read and write halves lets two sessions both decide the "
			+ "row is absent, and the second insert violates the primary key -- so a checkpoint that "
			+ "should have been recorded is lost. First failure: "
			+ (failures.Count > 0 ? failures[0].Message : "none"));
	}

	private async Task<Func<SqlConnection>> CreateProvisionedFactoryAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue(
			fixture.InitializationError ?? "SQL Server container must be available - this lock is never skipped.");

		var connectionString = fixture.ConnectionString;

		await using (var connection = new SqlConnection(connectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			await using var command = connection.CreateCommand();

			// From the DDL published in the store's own XML docs, so this exercises the store's statements
			// against the shipped schema rather than one the test invented.
			command.CommandText =
				"IF OBJECT_ID('ProjectionCursorMaps', 'U') IS NULL "
				+ "CREATE TABLE ProjectionCursorMaps ("
				+ "    TenantId NVARCHAR(256) NOT NULL,"
				+ "    ProjectionName NVARCHAR(256) NOT NULL,"
				+ "    StreamId NVARCHAR(256) NOT NULL,"
				+ "    Position BIGINT NOT NULL,"
				+ "    CONSTRAINT PK_ProjectionCursorMaps PRIMARY KEY (TenantId, ProjectionName, StreamId));";
			_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		return () => new SqlConnection(connectionString);
	}

	private static async Task<long?> ReadPositionAsync(Func<SqlConnection> connectionFactory, string streamId)
	{
		await using var connection = connectionFactory();
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		await using var command = connection.CreateCommand();

		command.CommandText =
			"SELECT Position FROM ProjectionCursorMaps "
			+ "WHERE TenantId = @TenantId AND ProjectionName = @ProjectionName AND StreamId = @StreamId;";
		_ = command.Parameters.AddWithValue("@TenantId", TenantId);
		_ = command.Parameters.AddWithValue("@ProjectionName", ProjectionName);
		_ = command.Parameters.AddWithValue("@StreamId", streamId);

		var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
		return result is null or DBNull ? null : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
	}
}
