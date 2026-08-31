using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Compliance;
using Excalibur.Dispatch;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Locks the half of tamper-evidence that examining each record in isolation cannot reach: that a record
/// removed from the middle of a trail is detected, that the left edge of a verified range is anchored, and
/// that an intact trail spanning more than one chain partition is not reported as tampered.
/// </summary>
/// <remarks>
/// The deletion arms are the ones that fail against a store verifying each record against its own stored
/// claim about its predecessor. That claim survives the removal of the record it names, so every survivor
/// still verifies and the trail reports clean. The interleaving arm is the paired liveness assertion: a
/// store that reported violations on everything would satisfy the deletion arms while being useless.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryAuditStoreChainDeletionShould : IDisposable
{
	private readonly InMemoryAuditStore _sut = new(AuditIntegrityTestStrategy.Create(), TestTenantHosts.UntenantedAuditHost());

	public void Dispose() => _sut.Dispose();

	[Fact]
	public async Task Detect_a_record_deleted_from_the_middle_of_an_intact_trail()
	{
		var now = DateTimeOffset.UtcNow;
		await StoreEventsAsync(_sut, now, tenantId: null, "evt-1", "evt-2", "evt-3");

		// Sanity: without this the deletion assertion below could pass on a store that reports violations
		// unconditionally, which would be no detection at all.
		var before = await _sut.VerifyChainIntegrityAsync(
			now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);
		before.Outcome.ShouldBe(
			AuditIntegrityOutcome.Verified,
			"Pre-deletion chain must verify or the deletion assertion is vacuous.");

		// Remove the middle record entirely, leaving evt-1 and evt-3 untouched. Every surviving record still
		// carries a self-consistent claim about its own predecessor, so a store checking records in isolation
		// sees nothing wrong. Only comparing against the record actually present exposes the gap.
		DeleteStoredEvent("evt-2");

		var result = await _sut.VerifyChainIntegrityAsync(
			now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);

		result.Outcome.ShouldBe(
			AuditIntegrityOutcome.ViolationsDetected,
			"Deleting a record from the middle of the trail MUST be detected; every survivor verifying against "
			+ "its own stored claim is exactly the blindness this arm exists to catch.");
		result.EventsVerified.ShouldBe(2);
		result.FirstViolationEventId.ShouldBe(
			"evt-3",
			"evt-3 was written to follow evt-2; with evt-2 gone it is the first record whose predecessor no "
			+ "longer matches what it was chained to.");
		result.ViolationDescription.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Detect_records_deleted_from_the_front_of_the_verified_range()
	{
		var now = DateTimeOffset.UtcNow;
		await StoreEventsAsync(_sut, now, tenantId: null, "evt-1", "evt-2", "evt-3");

		// A window that deliberately starts after the first record. The range is a slice of the chain, so its
		// first record is NOT the genesis record and must be bound to what precedes it.
		var rangeStart = now.AddSeconds(1).AddMilliseconds(-1);
		var rangeEnd = now.AddMinutes(1);

		var before = await _sut.VerifyChainIntegrityAsync(rangeStart, rangeEnd, CancellationToken.None);
		before.Outcome.ShouldBe(
			AuditIntegrityOutcome.Verified,
			"A range slice of an intact chain must verify, or this arm proves nothing about deletion.");
		before.EventsVerified.ShouldBe(2);

		// Delete the record immediately preceding the window. Nothing inside the window changed: evt-2 and
		// evt-3 still chain to each other perfectly. Only the anchor binding evt-2 to evt-1 is now violated.
		DeleteStoredEvent("evt-1");

		var result = await _sut.VerifyChainIntegrityAsync(rangeStart, rangeEnd, CancellationToken.None);

		result.Outcome.ShouldBe(
			AuditIntegrityOutcome.ViolationsDetected,
			"Deleting the record immediately before the window MUST be detected; without anchoring the left "
			+ "edge, a truncated range is indistinguishable from one that legitimately starts at genesis.");
		result.FirstViolationEventId.ShouldBe("evt-2");
	}

	[Fact]
	public async Task Detect_a_record_whose_content_changed_while_both_hash_columns_were_left_intact()
	{
		var now = DateTimeOffset.UtcNow;
		await StoreEventsAsync(_sut, now, tenantId: null, "evt-1", "evt-2", "evt-3");

		var before = await _sut.VerifyChainIntegrityAsync(
			now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);
		before.Outcome.ShouldBe(
			AuditIntegrityOutcome.Verified,
			"Pre-tamper chain must verify or the tamper assertion is vacuous.");

		// Rewrite a covered field while leaving BOTH hash columns exactly as written. Linkage still agrees
		// end to end — each record's stored prior tag still equals its predecessor's stored tag — so a store
		// that only compares stored hashes to stored hashes reports this trail clean.
		MutateStoredEvent("evt-2", stored => stored with { Action = "Read-REWRITTEN" });

		var result = await _sut.VerifyChainIntegrityAsync(
			now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);

		result.Outcome.ShouldBe(
			AuditIntegrityOutcome.ViolationsDetected,
			"Rewriting a covered field without touching either hash column MUST be detected; linkage alone "
			+ "compares stored hashes to stored hashes and never recomputes anything from live content.");
		result.FirstViolationEventId.ShouldBe("evt-2");
		result.ViolationDescription.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Verify_an_intact_trail_whose_writes_interleave_two_tenants()
	{
		var now = DateTimeOffset.UtcNow;
		var tenantA = new FixedTenantContext("tenant-a");
		using var storeA = new InMemoryAuditStore(AuditIntegrityTestStrategy.Create(), tenantA);

		// Interleaved writes across two tenants. Each tenant is its own chain, so consecutive records within
		// one tenant are NOT adjacent in write order. A verifier that walks records in global order rather
		// than per partition compares each record against a neighbour from the other tenant's chain and
		// reports tampering on a trail nothing has touched.
		for (var i = 0; i < 4; i++)
		{
			await storeA.StoreAsync(NewEvent($"a-{i}", now.AddSeconds(i * 2), "tenant-a"), CancellationToken.None);
			await storeA.StoreAsync(NewEvent($"b-{i}", now.AddSeconds((i * 2) + 1), "tenant-b"), CancellationToken.None);
		}

		var result = await storeA.VerifyChainIntegrityAsync(
			now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);

		result.Outcome.ShouldBe(
			AuditIntegrityOutcome.Verified,
			"An untouched trail must verify even when two tenants' writes interleave; reporting violations "
			+ "here makes the verifier useless as evidence, because it cries wolf on healthy data.");
		result.EventsVerified.ShouldBe(4, "The ambient tenant's own partition holds four records.");
		result.CompromisedChainCount.ShouldBe(0);
	}

	private static AuditEvent NewEvent(string eventId, DateTimeOffset timestamp, string? tenantId) => new()
	{
		EventId = eventId,
		EventType = AuditEventType.DataAccess,
		Action = "Read",
		Outcome = AuditOutcome.Success,
		Timestamp = timestamp,
		ActorId = "user-1",
		TenantId = tenantId
	};

	private static async Task StoreEventsAsync(
		InMemoryAuditStore store,
		DateTimeOffset origin,
		string? tenantId,
		params string[] eventIds)
	{
		for (var i = 0; i < eventIds.Length; i++)
		{
			await store.StoreAsync(NewEvent(eventIds[i], origin.AddSeconds(i), tenantId), CancellationToken.None);
		}
	}

	// Removes a stored record from BOTH of the store's indices, reproducing a row deleted from the trail.
	// Reflection rather than widened production visibility, per the internal-first rule. Throws rather than
	// passing vacuously if the record is not reachable: a deletion that removed nothing would let this arm
	// pass against a store that detects nothing.
	private void DeleteStoredEvent(string eventId)
	{
		var byId = ReadPrivateField<ConcurrentDictionary<string, AuditEvent>>("_eventsById");
		var byTenant = ReadPrivateField<ConcurrentDictionary<string, List<AuditEvent>>>("_eventsByTenant");

		_ = byId.TryRemove(eventId, out _);

		var removals = 0;
		foreach (var partition in byTenant.Values)
		{
			lock (partition)
			{
				removals += partition.RemoveAll(e => string.Equals(e.EventId, eventId, StringComparison.Ordinal));
			}
		}

		removals.ShouldBe(
			1,
			$"Expected to delete stored event '{eventId}' from exactly one partition. The store's storage "
			+ "shape changed and this arm is no longer removing what the verifier reads.");
	}

	// Replaces a stored record with a mutated copy in both indices. AuditEvent is a record, so `with` yields
	// a new instance: replacing only the by-id entry would leave the partition the verifier walks holding the
	// pristine original, and the arm would fail while proving nothing about tampering.
	private void MutateStoredEvent(string eventId, Func<AuditEvent, AuditEvent> mutate)
	{
		var byId = ReadPrivateField<ConcurrentDictionary<string, AuditEvent>>("_eventsById");
		var byTenant = ReadPrivateField<ConcurrentDictionary<string, List<AuditEvent>>>("_eventsByTenant");

		if (!byId.TryGetValue(eventId, out var stored))
		{
			throw new InvalidOperationException(
				$"Stored event '{eventId}' not found in _eventsById; refusing to pass vacuously.");
		}

		var mutated = mutate(stored);
		mutated.EventHash.ShouldBe(stored.EventHash, "The stored tag must be left exactly as written.");
		mutated.PreviousEventHash.ShouldBe(stored.PreviousEventHash, "The stored prior tag must be left exactly as written.");
		mutated.ShouldNotBe(stored, "The mutation must actually change the record.");

		byId[eventId] = mutated;

		var replacements = 0;
		foreach (var partition in byTenant.Values)
		{
			lock (partition)
			{
				for (var i = 0; i < partition.Count; i++)
				{
					if (string.Equals(partition[i].EventId, eventId, StringComparison.Ordinal))
					{
						partition[i] = mutated;
						replacements++;
					}
				}
			}
		}

		replacements.ShouldBe(
			1,
			$"Expected stored event '{eventId}' in exactly one partition; the store's storage shape changed "
			+ "and this arm no longer reaches what the verifier reads.");
	}

	private T ReadPrivateField<T>(string fieldName)
		where T : class
	{
		var field = typeof(InMemoryAuditStore).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException(
				$"InMemoryAuditStore.{fieldName} not found; the store's storage shape changed — update this arm.");

		return (T?)field.GetValue(_sut)
			?? throw new InvalidOperationException($"{fieldName} was null.");
	}

	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
