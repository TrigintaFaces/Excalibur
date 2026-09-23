// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Locks that a change the processor has not yet delivered survives a handler failure or a
/// cancellation, and is delivered - in order - on the next call.
/// </summary>
/// <remarks>
/// <para>
/// These arms run against the REAL store, and that is the whole point of them. Every pre-existing
/// failure-path arm for this processor substitutes a fake store, and a fake has no queue: it cannot
/// lose a change, so an arm built on one passes whether or not the remainder survives. The fake-based
/// arm that asserts MarkAsProcessed is not called on a handler failure passed throughout the period in
/// which changes were being silently discarded.
/// </para>
/// <para>
/// BatchSize is set LARGER than the backlog in every arm. That is the configuration that exposed the
/// loss - the whole backlog left the store in a single claim, before the first handler ran - so it is
/// the configuration an arm must use to be able to go red.
/// </para>
/// <para>
/// The in-flight change is consumed and reported through the propagating exception; it is not put
/// back. Only the changes never handed to a handler must survive. Requeueing the failing change would
/// turn a handler that always throws into permanent head-of-line blocking.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCdcProcessorRemainderSurvivesShould : UnitTestBase
{
	private const int Backlog = 5;
	private const int LargerThanTheBacklog = 10;

	/// <summary>
	/// SAFETY: a handler failure part-way through leaves every undelivered change in the store.
	/// </summary>
	[Fact]
	public async Task KeepTheUndeliveredChangesInTheStoreWhenAHandlerThrows()
	{
		var (store, changes) = Seed();
		using var processor = Create(store);

		var seen = 0;
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await processor.ProcessBatchAsync((_, _) =>
			{
				if (++seen == 3)
				{
					throw new InvalidOperationException("handler failed on the third change");
				}

				return Task.CompletedTask;
			}, CancellationToken.None).ConfigureAwait(false));

		// Changes 1 and 2 were delivered, change 3 was consumed and reported via the exception above,
		// and changes 4 and 5 were never handed to a handler. Those two must still be in the store.
		store.GetPendingCount().ShouldBe(
			changes.Length - 3,
			"the changes after the failing one were never delivered, so they must not have left the store");
	}

	/// <summary>
	/// LIVENESS + ORDER: the surviving changes are actually delivered on the next call, in the order
	/// they were added.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arm is satisfied by a store that keeps the changes and never gives
	/// them back. And a count alone is satisfied by a remainder restored in the wrong order, which is
	/// the specific way a requeue-based fix fails, so the sequence is asserted, not the length.
	/// </remarks>
	[Fact]
	public async Task DeliverTheSurvivingChangesInOrderOnTheNextCall()
	{
		var (store, changes) = Seed();
		using var processor = Create(store);

		var seen = 0;
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await processor.ProcessBatchAsync((_, _) =>
				++seen == 3 ? throw new InvalidOperationException("boom") : Task.CompletedTask,
				CancellationToken.None).ConfigureAwait(false));

		var delivered = new List<InMemoryCdcChange>();
		var processed = await processor.ProcessBatchAsync((change, _) =>
		{
			delivered.Add(change);
			return Task.CompletedTask;
		}, CancellationToken.None).ConfigureAwait(false);

		processed.ShouldBe(2);
		delivered.ShouldBe([changes[3], changes[4]], ignoreOrder: false);
		store.GetPendingCount().ShouldBe(0);
	}

	/// <summary>
	/// SAFETY: a cancellation part-way through leaves every undelivered change in the store.
	/// </summary>
	/// <remarks>
	/// The cancellation path lost the remainder by exactly the same mechanism as the exception path, so
	/// it needs its own arm - a fix that repaired only the catch block would leave this one red.
	/// </remarks>
	[Fact]
	public async Task KeepTheUndeliveredChangesInTheStoreWhenCancelled()
	{
		var (store, changes) = Seed();
		using var processor = Create(store);
		using var cts = new CancellationTokenSource();

		var seen = 0;
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await processor.ProcessBatchAsync((_, ct) =>
			{
				if (++seen == 2)
				{
					cts.Cancel();
				}

				ct.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			}, cts.Token).ConfigureAwait(false));

		// Change 1 was delivered, change 2 was consumed by the cancelled call, and changes 3..5 were
		// never handed to a handler.
		store.GetPendingCount().ShouldBe(
			changes.Length - 2,
			"the changes after the cancelled one were never delivered, so they must not have left the store");
	}

	private static (InMemoryCdcStore Store, InMemoryCdcChange[] Changes) Seed()
	{
		var store = new InMemoryCdcStore();
		var changes = new InMemoryCdcChange[Backlog];
		for (var i = 0; i < Backlog; i++)
		{
			changes[i] = InMemoryCdcChange.Insert(
				"dbo.Orders",
				new CdcDataChange { ColumnName = "Id", NewValue = i + 1 });
			store.AddChange(changes[i]);
		}

		return (store, changes);
	}

	private static InMemoryCdcProcessor Create(InMemoryCdcStore store) =>
		new(
			store,
			Options.Create(new InMemoryCdcOptions { BatchSize = LargerThanTheBacklog }),
			A.Fake<ILogger<InMemoryCdcProcessor>>());
}
