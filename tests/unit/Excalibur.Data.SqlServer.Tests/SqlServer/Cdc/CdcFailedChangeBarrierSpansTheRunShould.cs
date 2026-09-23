// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Threading.Channels;

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Excalibur.Domain;

using Microsoft.Extensions.Logging.Abstractions;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Binds the failed-change checkpoint barrier to the lifetime of the RUN rather than to one dequeued batch.
/// </summary>
/// <remarks>
/// When a change is handed to the fatal-error callback and that callback returns, the change has NOT been
/// applied — so the durable checkpoint for its table must not move past it, or the change is never
/// redelivered and is silently lost. The barrier enforcing that was allocated per dequeued batch, while the
/// durable checkpoint it protects outlives every batch. With a consumer batch size of one the gap is
/// immediate: the failing change occupies a batch alone, the barrier dies with that batch, and the very next
/// change on the same table writes a checkpoint past it.
/// <para>
/// The arms below are deliberately paired. The safety arms fail if the barrier is too SHORT; the liveness
/// arms fail if it is too LONG or too WIDE — a barrier that simply stopped all checkpointing forever, or
/// that barred every table once any table failed, would satisfy the safety arms alone.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcFailedChangeBarrierSpansTheRunShould : UnitTestBase
{
	private const string TableA = "dbo_orders";
	private const string TableB = "dbo_shipments";

	private sealed record Checkpoint(string TableName, byte[] Position);

	/// <summary>
	/// Builds an applier whose checkpoint writes land in <paramref name="written"/> instead of a database,
	/// with a consumer batch size of ONE so that every change is dequeued as its own batch — the shape in
	/// which a batch-scoped barrier stops protecting between one change and the next.
	/// </summary>
	private static CdcChangeApplier CreateApplier(
		List<Checkpoint> written,
		CdcFatalErrorHandler<DataChangeEvent>? onFatalError,
		ICdcIdempotencyFilter? idempotencyFilter = null)
	{
		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(1);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("test-connection");
		A.CallTo(() => dbConfig.DatabaseName).Returns("test-db");

		var stateStore = A.Fake<ISqlServerCdcStateStore>();
		A.CallTo(() => stateStore.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<long?>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				written.Add(new Checkpoint((string)call.Arguments[2]!, (byte[])call.Arguments[3]!));
				return Task.FromResult(1);
			});

		var policyFactory = A.Fake<IDataAccessPolicyFactory>();
		A.CallTo(() => policyFactory.GetComprehensivePolicy()).Returns(Policy.NoOpAsync());

		var checkpointManager = new CdcCheckpointManager(
			dbConfig,
			A.Fake<ICdcRepository>(),
			stateStore,
			NullLogger.Instance);

		return new CdcChangeApplier(
			dbConfig,
			policyFactory,
			checkpointManager,
			new OrderedEventProcessor(),
			NullLogger.Instance,
			onFatalError,
			idempotencyFilter);
	}

	private static DataChangeEvent Change(string tableName, byte lsn) =>
		new()
		{
			TableName = tableName,
			Lsn = [lsn],
			SeqVal = [lsn],
			CommitTime = DateTime.UtcNow,
			ChangeType = DataChangeType.Insert,
			Changes = [new DataChange { ColumnName = "Id", NewValue = 1, OldValue = null }],
		};

	private static async Task<int> DrainAsync(
		CdcChangeApplier applier,
		Func<DataChangeEvent, CancellationToken, Task> handler,
		params DataChangeEvent[] changes)
	{
		var channel = Channel.CreateUnbounded<DataChangeEvent>();
		foreach (var change in changes)
		{
			await channel.Writer.WriteAsync(change).ConfigureAwait(false);
		}

		channel.Writer.Complete();

		return await applier.ConsumerLoopAsync(
			channel.Reader,
			handler,
			isDisposed: static () => false,
			shouldWaitForProducer: static () => false,
			isProducerStopped: static () => true,
			CancellationToken.None).ConfigureAwait(false);
	}

	private static Task FailFirstChange(DataChangeEvent change) =>
		Task.FromException(new InvalidOperationException("handler rejected the change"));

	/// <summary>
	/// Reports the change at a chosen LSN as already processed, and every other change as new.
	/// </summary>
	/// <remarks>
	/// Hand-implemented rather than faked: the filter contract is internal, so a dynamic proxy cannot be
	/// generated for it — and a fixture that implements the interface directly is the shape that binds the
	/// interface's own requirement rather than an inherited convenience.
	/// </remarks>
	private sealed class SkipsOneChange(byte alreadyProcessedLsn) : ICdcIdempotencyFilter
	{
		public Task<bool> IsProcessedAsync(string tableName, byte[] lsn, byte[] seqVal, string consumerId, CancellationToken cancellationToken) =>
			Task.FromResult(lsn.Length > 0 && lsn[0] == alreadyProcessedLsn);

		public Task MarkProcessedAsync(string tableName, byte[] lsn, byte[] seqVal, string consumerId, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	/// <summary>
	/// SAFETY. The defect as filed: a later SUCCESS on the same table, in a later batch, must not carry the
	/// checkpoint past a change that was never applied.
	/// </summary>
	[Fact]
	public async Task NotCheckpointPastAFailedChangeWhenALaterBatchSucceeds()
	{
		var written = new List<Checkpoint>();
		var applier = CreateApplier(written, onFatalError: static (_, _) => Task.CompletedTask);

		_ = await DrainAsync(
			applier,
			(change, _) => change.Lsn[0] == 0x01 ? FailFirstChange(change) : Task.CompletedTask,
			Change(TableA, 0x01),
			Change(TableA, 0x02)).ConfigureAwait(false);

		written.ShouldNotContain(
			c => c.TableName == TableA,
			"the first change was never applied, so no checkpoint for its table may be written in this run — "
			+ "writing one at the second change leaves the first unreachable, because the next run resumes "
			+ "from the durable position and the first change is now behind it");
	}

	/// <summary>
	/// SAFETY. The same defect reached by the other route: the later change is not handled at all, only
	/// recognised as already-processed. An idempotency skip is treated as checkpoint-advancing, so it
	/// carries the checkpoint past the failure exactly as a success does.
	/// </summary>
	[Fact]
	public async Task NotCheckpointPastAFailedChangeWhenALaterBatchIsIdempotencySkipped()
	{
		var written = new List<Checkpoint>();

		var applier = CreateApplier(
			written,
			onFatalError: static (_, _) => Task.CompletedTask,
			idempotencyFilter: new SkipsOneChange(alreadyProcessedLsn: 0x02));

		_ = await DrainAsync(
			applier,
			(change, _) => change.Lsn[0] == 0x01 ? FailFirstChange(change) : Task.CompletedTask,
			Change(TableA, 0x01),
			Change(TableA, 0x02)).ConfigureAwait(false);

		written.ShouldNotContain(
			c => c.TableName == TableA,
			"a skip says this change needs no work, never that the EARLIER failed change was resolved");
	}

	/// <summary>
	/// LIVENESS. The barrier is per-table. A failure on one table must not stall the checkpoint of an
	/// unrelated table, or the fix trades a lost change for a stalled feed.
	/// </summary>
	[Fact]
	public async Task StillCheckpointAnUnrelatedTableAfterAnotherTableFails()
	{
		var written = new List<Checkpoint>();
		var applier = CreateApplier(written, onFatalError: static (_, _) => Task.CompletedTask);

		_ = await DrainAsync(
			applier,
			(change, _) => change.TableName == TableA ? FailFirstChange(change) : Task.CompletedTask,
			Change(TableA, 0x01),
			Change(TableB, 0x02)).ConfigureAwait(false);

		written.ShouldContain(
			c => c.TableName == TableB,
			"the barrier bars the table that failed, not the feed");
		written.ShouldNotContain(c => c.TableName == TableA);
	}

	/// <summary>
	/// LIVENESS control. With nothing failing, the run must still checkpoint normally — this arm passes both
	/// before and after the fix, so it is the positive control proving the safety arms read a live instrument
	/// rather than an applier that has stopped writing checkpoints altogether.
	/// </summary>
	[Fact]
	public async Task CheckpointEveryBatchWhenNothingFails()
	{
		var written = new List<Checkpoint>();
		var applier = CreateApplier(written, onFatalError: static (_, _) => Task.CompletedTask);

		var processed = await DrainAsync(
			applier,
			static (_, _) => Task.CompletedTask,
			Change(TableA, 0x01),
			Change(TableA, 0x02)).ConfigureAwait(false);

		processed.ShouldBe(2);
		written.Count.ShouldBe(2);
		written[^1].Position.ShouldBe(new byte[] { 0x02 });
	}
}
