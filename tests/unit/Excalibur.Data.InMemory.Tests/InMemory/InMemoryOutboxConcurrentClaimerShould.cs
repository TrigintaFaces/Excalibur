// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Author≠impl concurrent-claimer regression lock for 6icxgg — <see cref="InMemoryOutboxStore"/>'s
/// <c>GetUnsentMessagesAsync</c> is an <b>atomic disjoint lease-claim</b>: two pollers in the same process
/// draining the same staged set can never claim the same message. Same contract as the SQL Server / MongoDB
/// / Redis atomic-claim siblings (verify-against-real-infra: those run on real infra; this proves the
/// in-process store's own select-and-lease atomicity), different backend.
/// </summary>
/// <remarks>
/// The store selects-and-leases eligible messages under a single lock, so two concurrent
/// <c>GetUnsentMessagesAsync</c> callers partition the staged set — never overlap. Deterministic (no
/// wall-clock, no Docker): two pollers race via <see cref="Task.WhenAll(System.Threading.Tasks.Task[])"/>
/// over one store instance.
/// <para>
/// <b>RED-on-mutant:</b> remove the <c>lock (_claimLock)</c> around select-and-lease (select the candidate
/// batch, then record leases in a second step) ⇒ both pollers select the same eligible messages before
/// either records its leases ⇒ <see cref="TwoPollers_PartitionTheStagedSet_WithNoDoubleClaim"/> observes
/// overlapping ids and goes RED.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class InMemoryOutboxConcurrentClaimerShould : IDisposable
{
	private readonly InMemoryOutboxStore _store;

	public InMemoryOutboxConcurrentClaimerShould()
	{
		var options = Options.Create(new InMemoryOutboxOptions
		{
			MaxMessages = 100_000,
			// A long lease so neither poller's claim goes stale mid-test (no crash-recovery reclaim races).
			LeaseTimeoutSeconds = 3600,
		});
		_store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);
	}

	public void Dispose() => _store.Dispose();

	[Fact]
	public async Task TwoPollers_PartitionTheStagedSet_WithNoDoubleClaim()
	{
		// Stage a set large enough that a single poller's batch cannot claim all of it — forcing a genuine
		// race where both pollers see eligible messages at once.
		const int total = 40;
		const int batchSize = 20;
		for (var i = 0; i < total; i++)
		{
			await _store.StageMessageAsync(
				new OutboundMessage("test.message", [(byte)i], "dest"),
				CancellationToken.None);
		}

		// Two pollers claim concurrently from the SAME staged set.
		var claims = await Task.WhenAll(
			Task.Run(async () =>
				(await _store.GetUnsentMessagesAsync(batchSize, CancellationToken.None)).Select(m => m.Id).ToList()),
			Task.Run(async () =>
				(await _store.GetUnsentMessagesAsync(batchSize, CancellationToken.None)).Select(m => m.Id).ToList()));

		var a = claims[0];
		var b = claims[1];

		// Disjoint: no id claimed by BOTH pollers (the atomic-claim guarantee).
		var overlap = a.Intersect(b, StringComparer.Ordinal).ToList();
		overlap.ShouldBeEmpty(
			$"concurrent lease-claims must be disjoint — {overlap.Count} id(s) were claimed by both pollers");

		// No id appears twice across the union, and the union has no duplicates within either claim.
		var union = a.Concat(b).ToList();
		union.Count.ShouldBe(
			union.Distinct(StringComparer.Ordinal).Count(),
			"no message id may appear twice across the two concurrent claims");
	}

	[Fact]
	public async Task AClaimedMessage_IsNotReClaimed_ByASubsequentPoll()
	{
		await _store.StageMessageAsync(
			new OutboundMessage("test.message", [1], "dest"),
			CancellationToken.None);

		var first = (await _store.GetUnsentMessagesAsync(10, CancellationToken.None)).Select(m => m.Id).ToList();
		first.Count.ShouldBe(1, "the single staged message is claimed by the first poll");

		// A second poll while the lease is still valid must NOT re-hand-out the already-leased message.
		var second = (await _store.GetUnsentMessagesAsync(10, CancellationToken.None)).Select(m => m.Id).ToList();
		second.ShouldBeEmpty(
			"a message under a still-valid lease must not be re-claimed by a subsequent poll (no double delivery)");
	}
}
