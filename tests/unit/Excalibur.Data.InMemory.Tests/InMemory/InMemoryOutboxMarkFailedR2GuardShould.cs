// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

/// <summary>
/// lz7us9 — the Lamport R2 reservation-ownership guard for <see cref="InMemoryOutboxStore"/>, verified with a
/// SINGLE in-process instance (SA guardrail, msg 34416). The shared-kit real-infra R2 arm
/// (<c>MarkFailed_ByANonOwningDispatcher_DoesNotStealTheLease_R2</c>) self-skips for InMemory because it needs
/// two lease owners sharing one backing store, and two InMemory instances share no state. Rather than let that
/// skip hide the guard, this focused test exercises it directly: the guard is
/// <c>lease exists &amp;&amp; LeasedBy != _processorId ⇒ MarkFailedAsync is a no-op</c>
/// (<c>InMemoryOutboxStore.MarkFailedAsync</c>, the <c>dispatcher_id IN (NULL, @caller)</c> analogue).
/// </summary>
/// <remarks>
/// <para>
/// A foreign lease is injected via reflection on the private <c>_leases</c> field — the test needs to place a
/// row under a DIFFERENT owner than the store's per-instance <c>_processorId</c>, which is the only state that
/// distinguishes owner from non-owner, and there is no public seam to set it (production visibility is NOT
/// widened for the test, per the testing-patterns "Accessing Internals From Tests" rule; reflection is the
/// sanctioned alternative).
/// </para>
/// <para>
/// NON-VACUOUS. The safety arm is RED against an unconditional-unreserve store (one whose <c>MarkFailedAsync</c>
/// frees the lease + records the failure regardless of owner): there the foreign lease would be stolen and the
/// message recorded failed, so both safety assertions flip. The liveness arm is RED against a
/// reject-everything guard: the OWNER's fail would be dropped and never recorded. Both arms required
/// (testing-patterns §3): a guard asserted only on its safety half is satisfied by a store that refuses every
/// mark.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class InMemoryOutboxMarkFailedR2GuardShould
{
	private static InMemoryOutboxStore CreateStore(int floorSeconds) =>
		new(
			Options.Create(new InMemoryOutboxOptions { FailureBackoffFloorSeconds = floorSeconds }),
			NullLogger<InMemoryOutboxStore>.Instance);

	private static OutboundMessage NewMessage() =>
		new("TestMessageType", new byte[] { 1, 2, 3 }, "test-destination");

	private static ConcurrentDictionary<string, (DateTimeOffset LeasedAt, string LeasedBy)> LeasesOf(InMemoryOutboxStore store)
	{
		var field = typeof(InMemoryOutboxStore).GetField("_leases", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("InMemoryOutboxStore._leases field not found — the R2 guard seam moved.");
		return (ConcurrentDictionary<string, (DateTimeOffset, string)>)field.GetValue(store)!;
	}

	private static string ProcessorIdOf(InMemoryOutboxStore store)
	{
		var field = typeof(InMemoryOutboxStore).GetField("_processorId", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("InMemoryOutboxStore._processorId field not found — the R2 guard seam moved.");
		return (string)field.GetValue(store)!;
	}

	// SAFETY (R2): a MarkFailed by a NON-owner (the row is leased by a foreign dispatcher id) is a no-op — it
	// must NOT free the owner's lease and must NOT record the failure. RED against unconditional-unreserve.
	[Fact]
	public async Task NonOwningMarkFailed_DoesNotStealTheLease_NorRecordTheFailure()
	{
		// Long floor so nothing about re-claimability is time-dependent within the test.
		using var store = CreateStore(floorSeconds: 120);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);

		// Give the row a FOREIGN owner — a dispatcher id distinct from this store's own _processorId.
		var foreignOwner = "foreign-dispatcher:" + Guid.NewGuid().ToString("N");
		ProcessorIdOf(store).ShouldNotBe(foreignOwner, "the injected owner must differ from the store's own id");
		LeasesOf(store)[msg.Id] = (DateTimeOffset.UtcNow, foreignOwner);

		// Act: this store (a NON-owner of the row) fails it.
		await store.MarkFailedAsync(msg.Id, "stolen-lease", 1, CancellationToken.None);

		// SAFETY 1 — the foreign lease survives untouched (the non-owner did NOT steal it). RED against an
		// unconditional-unreserve store, which would have removed the lease entry here.
		LeasesOf(store).TryGetValue(msg.Id, out var lease).ShouldBeTrue(
			"a NON-owning MarkFailed must NOT free the owner's reservation — the lease entry must survive");
		lease.LeasedBy.ShouldBe(foreignOwner, "the surviving lease must still be owned by the foreign dispatcher");

		// SAFETY 2 — the failure was NOT recorded: the guard made the whole mark a no-op, so the message is not
		// transitioned to Failed. RED against a store that records the failure regardless of ownership.
		(await store.GetAllTenantsFailedMessagesAsync(maxRetries: 100, olderThan: null, batchSize: 10, CancellationToken.None))
			.ShouldNotContain(m => m.Id == msg.Id,
				"a non-owning MarkFailed is a no-op — it must not record the message as failed");
	}

	// LIVENESS (R2): the guard blocks ONLY non-owners. When the caller IS the lease owner, MarkFailed proceeds
	// and the failure IS recorded. RED against a reject-everything guard.
	[Fact]
	public async Task OwningMarkFailed_IsRecorded()
	{
		using var store = CreateStore(floorSeconds: 120);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);

		// Lease the row under THIS store's OWN _processorId — the caller of MarkFailed is now the owner.
		LeasesOf(store)[msg.Id] = (DateTimeOffset.UtcNow, ProcessorIdOf(store));

		await store.MarkFailedAsync(msg.Id, "owner-fail", 1, CancellationToken.None);

		(await store.GetAllTenantsFailedMessagesAsync(maxRetries: 100, olderThan: null, batchSize: 10, CancellationToken.None))
			.ShouldContain(m => m.Id == msg.Id,
				"the reservation OWNER must be able to MarkFailed — R2 guards against non-owners, never the owner");
	}
}
