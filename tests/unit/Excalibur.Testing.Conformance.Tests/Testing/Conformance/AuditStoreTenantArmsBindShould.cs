// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves that the cross-tenant arms in <see cref="AuditStoreConformanceTestKit"/> actually BIND --
/// that they go RED against a store which leaks, rather than passing over one.
/// </summary>
/// <remarks>
/// <para>
/// THIS TEST EXISTS BECAUSE A GREEN WAS MISREAD. The by-id tenant arm was reported as "RED to GREEN
/// proven" across a fix. It was not. Two runs were cited and neither one bound the arm: the first ran
/// over a working tree in which the store was ALREADY fixed, and the second ran over a committed tree in
/// which the ARM ITSELF DID NOT EXIST -- 824 passing tests over a store whose by-id read was a bare flat
/// dictionary lookup. A pass over a tree missing the test is byte-identical to a pass over a tree where
/// the test passed, and the only visible trace was a total count moving by one.
/// </para>
/// <para>
/// A conformance arm asserts that a real store does not leak. It cannot, by itself, tell anyone whether
/// it WOULD have noticed a store that did. That second question needs a store which provably leaks, and
/// no such store may be manufactured by mutating the production one -- that file carries another agent's
/// uncommitted work, and reverting it to plant a mutant destroys what is not in git. So the leak is built
/// here instead, in the test project, out of a dictionary.
/// </para>
/// <para>
/// The two fakes differ in ONE expression -- whether the by-id lookup consults the partition on the way
/// out. That is the whole experiment: same kit, same arm, same fixture, one changed line, opposite
/// verdicts. If both fakes produced the same verdict the arm would be decorative.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditStoreTenantArmsBindShould
{
	/// <summary>
	/// SAFETY-DETECTION: the by-id arm must FAIL against a store that leaks across tenants.
	/// </summary>
	/// <remarks>
	/// This is the arm's own non-vacuity proof. The fake reproduces the exact production defect -- a
	/// lookup keyed on the event identifier alone, returning whichever tenant's row carries it -- and the
	/// arm must refuse it. If this test ever goes green, the arm has stopped detecting cross-tenant
	/// disclosure and every store it certifies is uncertified.
	/// </remarks>
	[Fact]
	public async Task Red_WhenTheStoreLeaksAcrossTenants()
	{
		var probe = new ArmProbe(new FakeAuditStore(scopeById: false));

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunGetByIdTenantArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"CROSS-TENANT DISCLOSURE",
			Case.Sensitive,
			"the arm must fail with the disclosure diagnostic, not some incidental error -- a failure for "
			+ "an unrelated reason would prove the arm throws, not that it DETECTS the leak");
	}

	/// <summary>
	/// LIVENESS: the same arm must PASS against a store that scopes the same lookup.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above and not optional. An arm that threw for every store would satisfy the
	/// detection half perfectly while being incapable of certifying anything -- the same inaction defect
	/// the arm's own liveness half guards against, one level up.
	/// </remarks>
	[Fact]
	public async Task Green_WhenTheStoreScopesTheSameLookup()
	{
		var probe = new ArmProbe(new FakeAuditStore(scopeById: true));

		await probe.RunGetByIdTenantArmAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Drives the real kit arm against a supplied store. Subclassing is the only way in: the arm is a
	/// protected virtual member of the kit, and calling it through the kit is the point -- a reimplemented
	/// copy of the arm would prove things about the copy.
	/// </summary>
	private sealed class ArmProbe(IAuditStore store) : AuditStoreConformanceTestKit
	{
		protected override IAuditStore CreateStore() => store;

		public Task RunGetByIdTenantArmAsync() => GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt();

		// This probe drives exactly one arm, named above. The tamper hooks are unreachable from it, and are
		// implemented as refusals rather than no-ops so that wiring a tamper arm to this probe fails loudly
		// instead of passing against a fixture that cannot be tampered with.
		protected override Task DeleteRecordOutOfBandAsync(
			IAuditStore store,
			string eventId,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("This probe runs only the by-id tenant arm.");

		protected override Task RewriteRecordActionOutOfBandAsync(
			IAuditStore store,
			string eventId,
			string newAction,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("This probe runs only the by-id tenant arm.");
	}

	/// <summary>
	/// A minimal audit store whose by-id read is either scoped or flat, by construction.
	/// </summary>
	private sealed class FakeAuditStore(bool scopeById) : IAuditStore
	{
		private const string UntenantedPartitionKey = "_default_";

		private readonly ConcurrentDictionary<string, AuditEvent> _eventsById = new(StringComparer.Ordinal);

		/// <summary>
		/// The ambient partition an un-tenanted caller resolves to -- the same posture the kit exercises,
		/// which constructs the store with no tenant context.
		/// </summary>
		private static string AmbientPartition => UntenantedPartitionKey;

		private static string PartitionOf(AuditEvent auditEvent) => auditEvent.TenantId ?? UntenantedPartitionKey;

		public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(auditEvent);

			_ = _eventsById.TryAdd(auditEvent.EventId, auditEvent);

			return Task.FromResult(new AuditEventId
			{
				EventId = auditEvent.EventId,
				EventHash = "fake-hash",
				SequenceNumber = _eventsById.Count,
				RecordedAt = DateTimeOffset.UtcNow,
			});
		}

		/// <summary>
		/// THE ONE EXPRESSION UNDER EXPERIMENT. With <c>scopeById: false</c> this is the production defect
		/// verbatim: the identifier alone returns the row, whatever partition owns it.
		/// </summary>
		public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

			if (!_eventsById.TryGetValue(eventId, out var auditEvent))
			{
				return Task.FromResult<AuditEvent?>(null);
			}

			if (!scopeById)
			{
				return Task.FromResult<AuditEvent?>(auditEvent);
			}

			return Task.FromResult(
				string.Equals(PartitionOf(auditEvent), AmbientPartition, StringComparison.Ordinal)
					? auditEvent
					: null);
		}

		public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(query);

			IReadOnlyList<AuditEvent> scoped = _eventsById.Values
				.Where(e => string.Equals(PartitionOf(e), AmbientPartition, StringComparison.Ordinal))
				.ToList();

			return Task.FromResult(scoped);
		}

		public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(query);

			return Task.FromResult((long)_eventsById.Values
				.Count(e => string.Equals(PartitionOf(e), AmbientPartition, StringComparison.Ordinal)));
		}

		public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
			DateTimeOffset startDate,
			DateTimeOffset endDate,
			CancellationToken cancellationToken)
		{
			var verified = _eventsById.Values
				.Count(e => string.Equals(PartitionOf(e), AmbientPartition, StringComparison.Ordinal));

			// An empty partition establishes nothing about chain integrity and must not be reported as a
			// verified one -- the same distinction the real stores are held to. AuditIntegrityResult.Verified
			// rejects a zero count outright, so even this fake cannot express the claim.
			return Task.FromResult(
				verified == 0
					? AuditIntegrityResult.NoEventsInScope(startDate, endDate)
					: AuditIntegrityResult.Verified(verified, startDate, endDate, isHashChained: true));
		}

		public Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
		{
			var partition = tenantId ?? UntenantedPartitionKey;

			return Task.FromResult(_eventsById.Values
				.Where(e => string.Equals(PartitionOf(e), partition, StringComparison.Ordinal))
				.OrderBy(e => e.Timestamp)
				.LastOrDefault());
		}

		public Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> PurgeTenantAsync(
			DateTimeOffset cutoff,
			KeyedTenantPartition tenant,
			CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public object? GetService(Type serviceType) => null;
	}
}
