// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infra lock on the event store's global ordering guarantee: the set of committed global stream
/// positions is, at every instant, a contiguous prefix with no gaps.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is being protected.</b> An IDENTITY column, and every sequence generator, hands its number out
/// when the row is inserted and lets that number escape the transaction. The value is allocated before
/// COMMIT and discarded if the transaction aborts. That has two consequences, and the second is the
/// dangerous one: positions contain permanent holes where an append aborted, and two concurrent appends
/// can COMMIT in the opposite order to the positions they hold. A subscriber that has already read the
/// higher position then never sees the lower one -- the event is committed and durable and permanently
/// invisible to every projection, with nothing downstream able to detect it.
/// </para>
/// <para>
/// The store therefore allocates positions by UPDATEing a counter row inside the append transaction. The
/// row's exclusive lock is released only at COMMIT, so no other append can allocate while one is in
/// flight, and the increment rolls back with the transaction, so an aborted append burns no value.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock.</b> The property under test IS the database's locking and
/// rollback behaviour. A mocked connection returns whatever it was told and can reproduce neither, so
/// only a real engine can establish this. NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// <para>
/// <b>Both arms (testing-patterns section 3).</b> SAFETY -- an aborted append must not consume a
/// position, and concurrent appends must leave no hole. LIVENESS -- appends still succeed and still
/// receive positions, so a store that refused every append (and is therefore trivially gapless) fails
/// these arms rather than passing them.
/// </para>
/// <para>
/// <b>RED-on-mutant.</b> Restore <c>Position BIGINT IDENTITY(1,1)</c> in the shipped schema and drop the
/// counter allocation: the aborted append burns its number, the next append's position jumps by two
/// instead of one, and <see cref="NotBurnAGlobalPositionWhenAnAppendAborts"/> goes RED. That arm is
/// deliberately single-threaded and deterministic, so the proof does not depend on winning a race.
/// </para>
/// <para>
/// Note on isolation level: these arms assert the property against COMMITTED state, so they do not
/// require READ_COMMITTED_SNAPSHOT. Reproducing the older read-skip defect would have required RCSI, so
/// that an in-flight hole was visible to a reader rather than blocking it -- but under counter-row
/// allocation an in-flight hole cannot exist, which is the point.
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventStoreGaplessPositionShould
{
	private const string AggregateType = "Order";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreGaplessPositionShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	[MessageName("Test.SqlServerEventStoreGaplessPosition.OrderPlaced")]
	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	private SqlServerEventStore Store() =>
		new(
			() => _fixture.CreateConnection(),
			NullLogger<SqlServerEventStore>.Instance,
			schema: _fixture.SchemaName,
			table: _fixture.TableName,
			tenantContext: UntenantedTestTenantContext.Instance);

	private static async Task<long> AppendOneAsync(SqlServerEventStore store, string aggregateId)
	{
		var result = await store
			.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { new OrderPlaced(aggregateId, 0) },
				-1,
				CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeTrue("the append must succeed for its position to mean anything");
		return result.FirstEventPosition.ShouldNotBeNull(
			"a successful append must report the global position it was allocated");
	}

	/// <summary>Appends, then aborts inside the transaction AFTER the position has been allocated.</summary>
	private static async Task AbortAnAppendAfterAllocationAsync(SqlServerEventStore store, string aggregateId)
	{
		// stageOutbox runs inside the append transaction, after the events -- and therefore the position --
		// have been written. Throwing here rolls the whole unit of work back, which is the only way to
		// exercise "a position was allocated and then abandoned" through the store's public seam.
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.AppendWithOutboxStagingAsync(
					aggregateId,
					AggregateType,
					new IDomainEvent[] { new OrderPlaced(aggregateId, 0) },
					-1,
					(IDbTransaction _, CancellationToken _) =>
						throw new InvalidOperationException("forced rollback after position allocation"),
					CancellationToken.None)
				.ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task NotBurnAGlobalPositionWhenAnAppendAborts()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"global stream ordering is a data-loss boundary -- this real-SQL Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = Store();

		// LIVENESS -- a plain append succeeds and yields a position.
		var first = await AppendOneAsync(store, "agg-" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);

		// An append that allocates a position and then aborts.
		await AbortAnAppendAfterAllocationAsync(store, "agg-" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);

		// SAFETY -- the aborted append consumed nothing, so the next position is exactly the next integer.
		// Under an IDENTITY column this would be first + 2: the abandoned value is burned, never reissued.
		var next = await AppendOneAsync(store, "agg-" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);

		next.ShouldBe(
			first + 1,
			"an aborted append must not consume a global position -- the counter increment rolls back with " +
			"its transaction, so committed positions stay contiguous");
	}

	[Fact]
	public async Task LeaveNoHoleWhenConcurrentAppendsInterleaveAndSomeAbort()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"global stream ordering is a data-loss boundary -- this real-SQL Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		const int Total = 20;

		var store = Store();

		// Interleave committing and aborting appends so the aborts land between allocations rather than
		// all at one end: a hole in the middle is the shape a tailing subscriber cannot recover from.
		var work = new List<Task>(Total);
		for (var i = 0; i < Total; i++)
		{
			var aggregateId = "agg-" + Guid.NewGuid().ToString("N");
			work.Add(i % 5 == 0
				? AbortAnAppendAfterAllocationAsync(store, aggregateId)
				: AppendOneAsync(store, aggregateId));
		}

		await Task.WhenAll(work).ConfigureAwait(false);

		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table names come from the test fixture, not from input
		await using var command = new SqlCommand(
			$"SELECT COUNT(*), ISNULL(MIN(Position), 0), ISNULL(MAX(Position), 0) " +
			$"FROM [{_fixture.SchemaName}].[{_fixture.TableName}]",
			connection);
#pragma warning restore CA2100

		await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();

		var count = reader.GetInt32(0);
		var min = reader.GetInt64(1);
		var max = reader.GetInt64(2);

		// LIVENESS -- the committing appends actually committed. Without this arm a store that threw on
		// every append would satisfy the gapless assertion trivially, since an empty table has no holes.
		count.ShouldBe(
			Total - (Total / 5),
			"every non-aborting append must have committed");

		// SAFETY -- committed positions span exactly as many integers as there are rows, so there are no
		// holes. Every aborted append above allocated a position first, so under an IDENTITY column this
		// span would exceed the row count by the number of aborts.
		(max - min + 1).ShouldBe(
			count,
			$"committed positions must be contiguous: {count} rows spanning [{min}, {max}] leaves " +
			$"{max - min + 1 - count} abandoned position(s) that a tailing subscriber would stall on");
	}
}
