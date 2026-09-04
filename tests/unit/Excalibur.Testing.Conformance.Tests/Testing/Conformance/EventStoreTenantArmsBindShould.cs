// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization.Metadata;

using System.Collections.Concurrent;
using System.Text;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves that the tenancy arms in <see cref="EventStoreConformanceTestKit"/> actually BIND -- that each
/// one goes RED against a store carrying the defect it names, and GREEN against one that does not.
/// </summary>
/// <remarks>
/// <para>
/// A conformance arm asserts that a real store does not leak. It cannot, by itself, tell anyone whether it
/// WOULD have noticed a store that did. That second question needs stores which provably carry each defect,
/// so they are built here, in the test project, out of dictionaries.
/// </para>
/// <para>
/// The fakes implement <see cref="IEventStore"/> DIRECTLY, inheriting no first-party base. A fixture built
/// on <c>DelegatingEventStore</c> would re-test that base rather than the interface's own requirement, and
/// would pass for an interface member every real implementor gets wrong.
/// </para>
/// <para>
/// The modes differ in ONE decision each -- whether the partition is part of the key, whether reads answer
/// at all, whether the reserved untenanted term matches its own rows. That is the whole experiment: same
/// kit, same arms, one changed decision, opposite verdicts. An arm that produced the same verdict for every
/// mode would be decorative.
/// </para>
/// <para>
/// The verdict matrix each test below pins, one cell at a time:
/// </para>
/// <code>
///                        Safety   OwnEvents   Versioning   Untenanted
/// Partitioned            GREEN    GREEN       GREEN        GREEN
/// Blind                  RED      green       RED          green
/// AnswersNothing         green    RED         RED          RED
/// UntenantedBlackhole    green    green       green        RED
/// </code>
/// <para>
/// The lower-case greens are deliberate and load-bearing. A blind store passes the liveness arms -- it
/// answers everyone, including the right caller -- which is exactly why the safety and versioning arms
/// cannot be dropped in their favour. A store that answers nothing passes the safety arm perfectly, which
/// is why the liveness arms cannot be dropped either. Neither half detects the other's defect.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreTenantArmsBindShould
{
	#region Safety arm

	/// <summary>
	/// SAFETY-DETECTION: the isolation arm must FAIL against a store keyed without the tenant.
	/// </summary>
	/// <remarks>
	/// The fake reproduces the shipped defect verbatim -- a stream keyed on aggregate id and type alone,
	/// returning whichever tenant's events carry that key. If this test ever goes green, the arm has
	/// stopped detecting cross-tenant disclosure and every store it certifies is uncertified.
	/// </remarks>
	[Fact]
	public async Task Red_Safety_WhenTheStoreIsKeyedWithoutTheTenant()
	{
		var probe = new ArmProbe(FakeMode.Blind);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunSafetyArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"Tenant isolation violated",
			Case.Sensitive,
			"the arm must fail with the isolation diagnostic, not some incidental error -- a failure for an "
			+ "unrelated reason would prove the arm throws, not that it DETECTS the leak");
	}

	/// <summary>
	/// LIVENESS: the same arm must PASS against a store that puts the tenant in the key.
	/// </summary>
	[Fact]
	public Task Green_Safety_WhenTheStoreIsPartitionedByTenant() =>
		new ArmProbe(FakeMode.Partitioned).RunSafetyArmAsync();

	/// <summary>
	/// The safety arm CANNOT detect an inert store, and this pins that limitation rather than leaving it
	/// to be assumed.
	/// </summary>
	/// <remarks>
	/// A store that answers nothing discloses nothing, so it satisfies this arm perfectly. That is the
	/// entire reason the liveness arm below is mandatory and not a nicety: if this pairing were ever
	/// collapsed into a single "isolation" arm, an inert store would certify clean.
	/// </remarks>
	[Fact]
	public Task Green_Safety_EvenWhenTheStoreAnswersNothing() =>
		new ArmProbe(FakeMode.AnswersNothing).RunSafetyArmAsync();

	#endregion

	#region Liveness arm

	/// <summary>
	/// LIVENESS-DETECTION: the own-events arm must FAIL against a store that answers nothing.
	/// </summary>
	/// <remarks>
	/// This is the arm the whole pairing exists for. Confining by returning nothing is the cheapest way to
	/// pass every safety assertion ever written, and it is indistinguishable from correct isolation unless
	/// something asserts that the right caller still gets its rows.
	/// </remarks>
	[Fact]
	public async Task Red_Liveness_WhenTheStoreAnswersNothing()
	{
		var probe = new ArmProbe(FakeMode.AnswersNothing);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunOwnEventsArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"inert or lossy",
			Case.Sensitive,
			"the arm must fail with the inert-store diagnostic rather than for an incidental reason");
	}

	/// <summary>
	/// LIVENESS-DETECTION: the own-events arm must also FAIL against a store that returns a PARTIAL
	/// history.
	/// </summary>
	/// <remarks>
	/// Asserting merely "not empty" would pass this store. A partial history is not a lesser version of the
	/// same problem -- it rebuilds every aggregate to a wrong state silently, where an empty one at least
	/// fails loudly at the first read.
	/// </remarks>
	[Fact]
	public async Task Red_Liveness_WhenTheStoreDropsPartOfTheHistory()
	{
		var probe = new ArmProbe(FakeMode.DropsOneEvent);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunOwnEventsArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("inert or lossy", Case.Sensitive);
	}

	/// <summary>
	/// LIVENESS: the same arm must PASS against a store that returns the tenant's whole history.
	/// </summary>
	[Fact]
	public Task Green_Liveness_WhenTheStoreReturnsItsOwnEvents() =>
		new ArmProbe(FakeMode.Partitioned).RunOwnEventsArmAsync();

	#endregion

	#region Per-partition versioning arm

	/// <summary>
	/// VERSIONING-DETECTION: the arm must FAIL against a store whose version counter is shared across
	/// tenants.
	/// </summary>
	/// <remarks>
	/// The defect this arm exists for is invisible to both isolation arms: a store may filter reads by
	/// tenant correctly and still keep one counter per aggregate identifier, at which point the second
	/// tenant to use an identifier is refused its own aggregate forever. The blind fake exhibits it because
	/// a shared key implies a shared counter.
	/// </remarks>
	[Fact]
	public async Task Red_Versioning_WhenTheCounterIsSharedAcrossTenants()
	{
		var probe = new ArmProbe(FakeMode.Blind);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunVersioningArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"Version counter is shared across tenants",
			Case.Sensitive,
			"the arm must name the shared counter -- that is the diagnosis a provider author acts on");
	}

	/// <summary>
	/// LIVENESS: the same arm must PASS when each partition versions its own streams.
	/// </summary>
	[Fact]
	public Task Green_Versioning_WhenEachPartitionVersionsIndependently() =>
		new ArmProbe(FakeMode.Partitioned).RunVersioningArmAsync();

	#endregion

	#region Untenanted partition arm

	/// <summary>
	/// UNTENANTED-DETECTION: the arm must FAIL against a store whose scoping matches nothing for the
	/// reserved untenanted term.
	/// </summary>
	/// <remarks>
	/// This is the defect that breaks every consumer who never opted into multi-tenancy, and no isolation
	/// assertion reports it: such a store is perfectly isolated and perfectly empty. The fake writes under
	/// the untenanted partition and declines to read it back, which is what a predicate treating the
	/// reserved term as "no tenant, therefore no match" does in production.
	/// </remarks>
	[Fact]
	public async Task Red_Untenanted_WhenTheReservedPartitionMatchesNothing()
	{
		var probe = new ArmProbe(FakeMode.UntenantedBlackhole);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunUntenantedArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("untenanted partition", Case.Sensitive);
	}

	/// <summary>
	/// LIVENESS: the same arm must PASS when the untenanted partition round-trips.
	/// </summary>
	[Fact]
	public Task Green_Untenanted_WhenTheReservedPartitionRoundTrips() =>
		new ArmProbe(FakeMode.Partitioned).RunUntenantedArmAsync();

	#endregion

	#region Harness's own guard

	/// <summary>
	/// The kit must REFUSE to certify a provider whose registration replaces the ambient tenant context.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the guard on the guard. Every tenancy arm works by switching the tenant on a context the kit
	/// owns and registers. If a provider's registration overrides that context with one of its own, the kit
	/// still resolves a store and every arm still runs — but the store now reads a tenant the kit cannot
	/// change, so both "partitions" are the same partition, no read ever crosses a boundary, and the
	/// isolation arms pass while exercising no isolation whatsoever.
	/// </para>
	/// <para>
	/// That failure is silent and total: a green conformance run over a store that was never tenant-tested.
	/// It is the same defect the arms themselves exist to catch, one level up in the harness, so it gets the
	/// same treatment — an explicit refusal, and a test proving the refusal fires.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Refuse_WhenTheProviderReplacesTheAmbientTenantContext()
	{
		var probe = new ContextHijackingProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunSafetyArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"replaced the ambient ITenantContext",
			Case.Sensitive,
			"the kit must name the hijack specifically -- failing for any other reason would leave the "
			+ "silent-pass hole open while looking like it was covered");
	}

	/// <summary>
	/// A provider that registers its own tenant context unconditionally, displacing the kit's.
	/// </summary>
	private sealed class ContextHijackingProbe : EventStoreConformanceTestKit
	{
		private readonly ConcurrentDictionary<string, List<StoredEvent>> _streams =
			new(StringComparer.Ordinal);

		protected override void ConfigureProvider(
			IServiceCollection services,
			IJsonTypeInfoResolver? eventTypeInfoResolver)
		{
			// A plain Add, not TryAdd -- last registration wins, so the kit's context is displaced. This is
			// the realistic shape of the mistake: a provider "helpfully" supplying a default.
			_ = services.AddSingleton<ITenantContext>(new FixedContext());
			_ = services.AddSingleton<IEventStore>(
				sp => new FakeEventStore(FakeMode.Partitioned, sp.GetRequiredService<ITenantContext>(), _streams));
		}

		public Task RunSafetyArmAsync() => TenantScopedLoad_MustNotSeeAnotherTenantsEvents();

		private sealed class FixedContext : ITenantContext
		{
			public string? TenantId => "provider-supplied-tenant";

			public bool HasTenant => true;
		}
	}

	#endregion

	#region Harness

	/// <summary>The single decision each fake varies.</summary>
	private enum FakeMode
	{
		/// <summary>Tenant is part of the stream key. The conformant shape.</summary>
		Partitioned,

		/// <summary>Stream keyed on aggregate id and type alone -- the shipped defect.</summary>
		Blind,

		/// <summary>Partitioned writes, but every read returns empty.</summary>
		AnswersNothing,

		/// <summary>Partitioned, but every read silently omits the last event.</summary>
		DropsOneEvent,

		/// <summary>Partitioned, but the reserved untenanted term matches no rows on read.</summary>
		UntenantedBlackhole,
	}

	/// <summary>
	/// Drives the real kit arms against a supplied fake. Subclassing is the only way in: the arms are
	/// members of the kit, and calling them THROUGH the kit is the point -- a reimplemented copy of an arm
	/// would prove things about the copy.
	/// </summary>
	private sealed class ArmProbe(FakeMode mode) : EventStoreConformanceTestKit
	{
		// ONE backing set shared by every store this probe hands out, so that even a kit which called
		// CreateStore more than once could not satisfy an isolation arm by instance separation.
		private readonly ConcurrentDictionary<string, List<StoredEvent>> _streams =
			new(StringComparer.Ordinal);

		// Registered through a real container, exactly as a provider's own extension would: the fake is
		// resolved rather than handed over, so this harness exercises the same resolution path the arms
		// use against real providers.
		protected override void ConfigureProvider(
			IServiceCollection services,
			IJsonTypeInfoResolver? eventTypeInfoResolver) =>
			services.AddSingleton<IEventStore>(
				sp => new FakeEventStore(mode, sp.GetRequiredService<ITenantContext>(), _streams));

		public Task RunSafetyArmAsync() => TenantScopedLoad_MustNotSeeAnotherTenantsEvents();

		public Task RunOwnEventsArmAsync() => TenantScopedLoad_MustSeeItsOwnEvents();

		public Task RunVersioningArmAsync() => TenantPartitions_MustVersionTheSameAggregateIndependently();

		public Task RunUntenantedArmAsync() => UntenantedPartition_MustRoundTripItsOwnEvents();
	}

	/// <summary>
	/// A minimal event store whose partitioning decision is fixed by construction.
	/// </summary>
	private sealed class FakeEventStore(
		FakeMode mode,
		ITenantContext tenantContext,
		ConcurrentDictionary<string, List<StoredEvent>> streams) : IEventStore
	{
		private readonly Lock _lock = new();

		private string Tenant => tenantContext.TenantId ?? TenantScope.UntenantedSentinel;

		/// <summary>
		/// THE ONE EXPRESSION UNDER EXPERIMENT. Under <see cref="FakeMode.Blind"/> the tenant is absent
		/// from the key, so both partitions address the same stream AND the same version counter.
		/// </summary>
		private string KeyFor(string aggregateId, string aggregateType) =>
			mode == FakeMode.Blind
				? $"{aggregateType}|{aggregateId}"
				: $"{Tenant}|{aggregateType}|{aggregateId}";

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId,
			string aggregateType,
			CancellationToken cancellationToken) =>
			LoadAsync(aggregateId, aggregateType, -1, cancellationToken);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId,
			string aggregateType,
			long fromVersion,
			CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
			ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

			if (mode == FakeMode.AnswersNothing)
			{
				return ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
			}

			if (mode == FakeMode.UntenantedBlackhole
				&& string.Equals(Tenant, TenantScope.UntenantedSentinel, StringComparison.Ordinal))
			{
				return ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
			}

			lock (_lock)
			{
				if (!streams.TryGetValue(KeyFor(aggregateId, aggregateType), out var stream))
				{
					return ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
				}

				var visible = stream.Where(e => e.Version > fromVersion).ToList();

				if (mode == FakeMode.DropsOneEvent && visible.Count > 0)
				{
					visible.RemoveAt(visible.Count - 1);
				}

				return ValueTask.FromResult<IReadOnlyList<StoredEvent>>(visible);
			}
		}

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
			ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
			ArgumentNullException.ThrowIfNull(events);

			var batch = events.ToList();

			lock (_lock)
			{
				var stream = streams.GetOrAdd(KeyFor(aggregateId, aggregateType), _ => []);
				var current = stream.Count == 0 ? 0 : stream[^1].Version;

				// -1 means "this stream does not exist yet". Under Blind that claim is false for the second
				// tenant, because the first tenant's events are in the same stream -- which is precisely the
				// conflict the versioning arm reports.
				var normalisedExpected = expectedVersion < 0 ? 0 : expectedVersion;
				if (normalisedExpected != current)
				{
					return ValueTask.FromResult(
						AppendResult.CreateConcurrencyConflict(expectedVersion, current));
				}

				foreach (var domainEvent in batch)
				{
					current++;
					stream.Add(new StoredEvent(
						domainEvent.EventId,
						aggregateId,
						aggregateType,
						MessageNameHelper.GetName(domainEvent.GetType()),
						Encoding.UTF8.GetBytes(domainEvent.EventId),
						null,
						current,
						domainEvent.OccurredAt));
				}

				return ValueTask.FromResult(AppendResult.CreateSuccess(current, null));
			}
		}
	}

	#endregion
}
