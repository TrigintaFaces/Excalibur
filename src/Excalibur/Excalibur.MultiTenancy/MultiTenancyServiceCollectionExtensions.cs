// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.TieredStorage;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for first-class multi-tenancy. A single <see cref="AddMultiTenancy"/> call selects a
/// tenant-isolation strategy and wires fail-closed tenant scoping consistently across the event store,
/// projections, and sagas.
/// </summary>
public static class MultiTenancyServiceCollectionExtensions
{
	/// <summary>
	/// Adds multi-tenancy with the configured <see cref="TenantIsolationStrategy"/>.
	/// </summary>
	/// <param name="services">The service collection. Persistence stores must already be registered.</param>
	/// <param name="configure">Configures the <see cref="MultiTenancyOptions"/> (at minimum, the strategy).</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The ambient tenant is required on every tenant-facing store operation: this method registers the ambient
	/// <see cref="ITenantContext"/> with <c>RequireTenant</c> enabled so an operation with no resolved tenant
	/// fails closed rather than running unscoped. The tenant itself is established for a logical operation by the
	/// host (for example, a request middleware that calls <c>TenantContextHolder.BeginScope(...)</c>).
	/// </para>
	/// <list type="bullet">
	///   <item>
	///     <description>
	///     <see cref="TenantIsolationStrategy.RowDiscriminator"/> — wraps each registered
	///     <see cref="IEventStore"/>, <see cref="IProjectionStore{TProjection}"/>, and <see cref="ISagaStore"/>
	///     with its fail-closed tenant-scoping decorator, and refuses to start when any registered contract
	///     declaring <see cref="TenantOwnedAttribute"/> presents neither
	///     <see cref="ITenantScopingCapability{TContract}"/> nor
	///     <see cref="ITenantPartitionedCapability{TContract}"/> - so a store the framework cannot
	///     confine is rejected here rather than returning another tenant rows at runtime. Fails fast
	///     when no tenant-owned store is registered at all.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///     <see cref="TenantIsolationStrategy.Sharding"/> — requires tenant-aware routing to have been enabled
	///     on the event-sourcing builder (<c>AddEventSourcing(es =&gt; es.EnableTenantSharding(...))</c>) and
	///     asserts it is present. Also routes <see cref="ISagaStore"/> to the correct tenant's shard when a
	///     saga provider is registered — sagas are optional under sharding, so a host with no saga provider
	///     is unaffected.
	///     </description>
	///   </item>
	/// </list>
	/// <para>Calling this method more than once is a no-op after the first (idempotent).</para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// The strategy is unset/invalid; or <see cref="TenantIsolationStrategy.RowDiscriminator"/> is selected but no
	/// decoratable store is registered; or <see cref="TenantIsolationStrategy.Sharding"/> is selected without
	/// tenant routing enabled.
	/// </exception>
	public static IServiceCollection AddMultiTenancy(
		this IServiceCollection services,
		Action<MultiTenancyOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Idempotence: a second call after the first is a no-op (avoids double-wrapping decorators, which are
		// registered via factories and so cannot be detected by implementation-type inspection alone).
		if (services.Any(static d => d.ServiceType == typeof(MultiTenancyMarker)))
		{
			return services;
		}

		// Materialize the strategy now so registration can branch on it, and register the options for runtime
		// validation (ValidateOnStart) as well.
		var options = new MultiTenancyOptions();
		configure(options);

		_ = services.AddOptions<MultiTenancyOptions>().Configure(configure).ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MultiTenancyOptions>, MultiTenancyOptionsValidator>());

		// Composition-time fail-fast (mirrors the startup validator, but earlier and with a clearer trace).
		if (options.Strategy == TenantIsolationStrategy.Unspecified || !Enum.IsDefined(options.Strategy))
		{
			throw new InvalidOperationException(
				$"{nameof(MultiTenancyOptions)}.{nameof(MultiTenancyOptions.Strategy)} must be set to a valid tenant "
				+ $"isolation strategy ({nameof(TenantIsolationStrategy.RowDiscriminator)} or "
				+ $"{nameof(TenantIsolationStrategy.Sharding)}).");
		}

		// Require an ambient tenant on every tenant-facing operation (fail closed on a missing tenant).
		_ = services.AddTenantContext(static o => o.RequireTenant = true);

		switch (options.Strategy)
		{
			case TenantIsolationStrategy.RowDiscriminator:
				ApplyRowDiscriminator(services);
				break;

			case TenantIsolationStrategy.Sharding:
				// Delegate to the single tenant-routing wiring point in Excalibur.EventSourcing (shared with
				// IEventSourcingBuilder.EnableTenantSharding, so the two seams do not fork). Visible here via
				// InternalsVisibleTo. The consumer still registers the shard map + provider store resolvers.
				TenantShardingServiceCollectionExtensions.RegisterTenantRoutingStores(services);
				break;

			default:
				// Unreachable: guarded above.
				break;
		}

		// Order-independent half of the tenant-capability gate, registered for EVERY multi-tenant host and
		// deliberately OUTSIDE the strategy switch above. The composition-time sweep asks the same question of
		// the same registrations, but only of those present at this instant, and only under row discrimination:
		// a store registered after this call is never seen, and a sharding host never reaches the sweep at all.
		// Re-asserting against the completed collection at host start makes the outcome independent of both.
		// Keep it here, not inside a branch, or each bypass it closes comes straight back.
		services.TryAddSingleton(_ => new TenantOwnedCapabilityStartupValidator(services, options.Strategy));
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TenantOwnedCapabilityStartupValidator>(
			static sp => sp.GetRequiredService<TenantOwnedCapabilityStartupValidator>()));
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, TenantOwnedCapabilityStartupValidator>(
			static sp => sp.GetRequiredService<TenantOwnedCapabilityStartupValidator>()));

		services.AddSingleton<MultiTenancyMarker>();
		return services;
	}

	private static void ApplyRowDiscriminator(IServiceCollection services)
	{
		// PRESENCE IS ASKED VIA HasStoreRegistrationFor, NEVER services.Any(d => d.ServiceType == T).
		// Read this before changing any gate below back to the plain predicate.
		//
		// A contract can hold a descriptor that PROMISES it without PROVIDING it: the core event-sourcing
		// and inbox registrations emit a non-keyed alias forwarding to the keyed "default" store, and they
		// must emit it unconditionally, because registering the store afterwards is a supported ordering.
		// The plain predicate counts that alias, so a host that registered an event store and no snapshot
		// store - snapshots being optional - was told its ISnapshotStore provider is not tenant-capable,
		// naming a store it never registered and could not fix.
		//
		// The narrowing is bounded by the type system, not by judgement: HasStoreRegistrationFor skips only
		// descriptors carrying the alias seam's internal forwarder marker, which no registration outside
		// that seam can present. A consumer's own store - keyed or not, factory or type or instance - is
		// still counted, so nothing the gate used to catch escapes it. And a forwarder resolves only when a
		// keyed "default" descriptor exists, which is itself counted, so skipping forwarders removes
		// exactly the descriptors that back no store.
		// Every specific-marker requirement, evaluated before anything is wrapped so a tenant-unaware provider
		// is refused with its registration still in view. The same list runs again against the finished
		// collection at host start, which is what makes the outcome independent of registration order.
		var gatedAny = AssertPerContractCapabilities(services);

		var decoratedAny = false;

		if (services.HasStoreRegistrationFor(typeof(IEventStore)))
		{
			// The capability assertion for this contract ran in AssertPerContractCapabilities above, so a
			// tenant-unaware provider has already been refused. Wrapping one would pass the decorator's
			// fail-closed presence check yet leak every tenant's data from the inner store.

			// Shape-robust key-targeted decoration. Providers register IEventStore as several descriptors — a
			// provider-keyed terminal, a keyed "default" alias, and a non-keyed forwarding alias — so the generic
			// Decorate<T> (which decorates exactly one and throws on multiple) cannot be used here: it would either
			// leave a provider terminal raw (a cross-tenant leak) or double-wrap a forwarder. DecorateKeyedStores
			// wraps every terminal exactly once and leaves the aliases forwarding onto it.
			//
			// Tiered storage inverts the usual topology: UseTieredStorage moves the raw hot store to a private
			// key (EventArchiveService.RawHotEventStoreKey) and makes the keyed "default" store the
			// TieredEventStoreDecorator over (raw hot, raw cold). Without special handling, DecorateKeyedStores
			// Rule 1 would wrap that private hot key, tenant-scoping the very hot store the archive service
			// resolves for its intentionally cross-tenant trim (breaking it). Reserving the private hot key makes
			// decoration target the consumer-facing "default" Tiered store instead (TenantScoped OUTER of Tiered),
			// leaving the raw hot key untouched for the archive.
			//
			// SCOPE — do NOT overstate this: the reservation fixes (a) the archive-service resolution (it resolves
			// the RAW hot, not a tenant-scoped store that would throw with no ambient tenant) and (b) fail-closed
			// tenant PRESENCE on the consumer read path (the outer decorator's RequireTenant throws before any tier
			// read when no tenant is ambient). It does NOT tenant-isolate the COLD read: TenantScopedEventStore
			// enforces presence then delegates WITHOUT a row predicate (isolation lives in the inner store's query),
			// and the cold leg (IColdEventStore, keyed by aggregate id, no tenant awareness) has none — so a caller
			// with a DIFFERENT ambient tenant can still read another tenant's archived (cold) events. This is a
			// KNOWN OPEN, proven cross-tenant isolation gap on the cold tier: the (row-discriminator MT + tiered +
			// non-tenant-aware cold) combination is NOT currently safe. It is not left fail-open — the tiered/cold
			// gate below fails fast at startup, so the combination is gated UNSUPPORTED until the cold path is
			// tenant-partitioned (tenant in the archive key + a tenant-aware cold read), tracked separately.
			var reservedEventStoreKeys = services.Any(static d =>
					d.ServiceType == typeof(IEventStore)
					&& d.IsKeyedService
					&& string.Equals(d.ServiceKey as string, EventArchiveService.RawHotEventStoreKey, StringComparison.Ordinal))
				? new object[] { EventArchiveService.RawHotEventStoreKey }
				: null;

			_ = services.DecorateKeyedStores<IEventStore>(
				static (inner, sp) => ActivatorUtilities.CreateInstance<TenantScopedEventStore>(sp, inner),
				reservedEventStoreKeys);
			decoratedAny = true;
		}

		// Tiered/cold storage gate — closes the KNOWN OPEN cross-tenant cold-read isolation gap by making the
		// unsafe combination fail fast rather than leak. When a cold tier (IColdEventStore) is registered, the
		// consumer read path becomes TenantScoped(Tiered(hot, cold)); but TenantScopedEventStore enforces tenant
		// PRESENCE then delegates WITHOUT a row predicate, and the cold leg is keyed by aggregate id with no
		// tenant awareness — so a caller with a different ambient tenant can read another tenant's archived (cold)
		// events. The tiered/cold path is NOT tenant-partitioned, so under row-discriminator multi-tenancy this
		// combination is not currently safe. A cold store that cannot scope tenants cannot present the
		// tenant-scoping capability marker (cold providers such as blob/object stores emit none), so this fails
		// fast at startup: the (row-discriminator MT + tiered + non-tenant-aware cold) triple is UNSUPPORTED
		// until the cold path is tenant-partitioned (a tenant in the archive key + a tenant-aware cold read),
		// which is tracked separately. Its own guard: it only THROWS (fail-fast) or no-ops for a future
		// tenant-aware cold store, so it neither decorates nor sets gatedAny.
		// ORDER-INDEPENDENCE — read this before touching either half. The descriptor predicate below is the
		// EARLY half of a two-part gate, and on its own it does not hold the property. `services.Any(...)` is a
		// statement about one instant in a MUTABLE list: it fires only when the cold store was registered
		// BEFORE AddMultiTenancy. A host that calls AddAwsS3ColdEventStore (or the AzureBlob/Gcs equivalent)
		// AFTER AddMultiTenancy registers IColdEventStore into the same collection a moment later, this
		// predicate has already run and evaluated false, and startup succeeds straight into the unsafe
		// configuration described above — reversing two calls changes whether a safety gate runs at all.
		// Misordering the PRIMARY stores fails loud; misordering the cold store used to fail SILENT.
		//
		// The invariant is therefore carried by ColdStoreTenantScopingValidator, which evaluates the same
		// assertion against the FINISHED container (IServiceProviderIsService over the built provider) and so
		// is order-independent by construction. It is registered unconditionally below — not inside this `if`,
		// which would reintroduce the very ordering dependence it exists to remove.
		//
		// This early predicate is kept because it is not vacuous and it is not redundant: when the ordering
		// does put the cold store first, it fails at the AddMultiTenancy call site with the registration in
		// view, which is a far better diagnostic than a failure at host start. Fail early when we can, fail
		// always via the guard.
		// Registered for every row-discriminator host, whether or not a cold tier is visible AT THIS INSTANT.
		// TryAddEnumerable keeps repeat AddMultiTenancy calls idempotent. The guard no-ops when no cold tier is
		// resolvable, so the cost to a host that never uses tiered storage is one type check at start.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ColdStoreTenantScopingValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, ColdStoreTenantScopingValidator>());

		if (services.HasStoreRegistrationFor(typeof(ISagaStore)))
		{
			// Same shape-robust rule as the event store: saga providers register a provider-keyed terminal plus a
			// keyed "default" forwarder, so wrap the terminal(s) and leave the aliases forwarding.
			_ = services.DecorateKeyedStores<ISagaStore>(
				static (inner, sp) => ActivatorUtilities.CreateInstance<TenantScopedSagaStore>(sp, inner));
			decoratedAny = true;
		}

		if (DecorateProjectionStores(services))
		{
			decoratedAny = true;
		}

		// The inbox and the outbox carry MESSAGES — the payloads most likely to hold another tenant's data —
		// and both are gated here but NEITHER is decorated. The asymmetry is deliberate and load-bearing:
		//
		//   * The outbox drain is intentionally CROSS-TENANT. OutboxProcessor establishes a per-message scope
		//     (BeginScope(message.TenantId)) as it drains, because one drain pass carries every tenant's
		//     messages. A tenant-scoped decorator would read the ambient tenant as null at drain time, claim
		//     the empty set, and stall the drain permanently — while still satisfying a safety-only test
		//     asserting "tenant B does not see tenant A's row." Inaction is the cheapest way to look safe.
		//
		//   * The inbox already applies the tenant predicate inside its provider stores, on the composite
		//     (TenantId, MessageId, HandlerType) key. Wrapping a store that already filters would add a second
		//     filter without repairing the first.
		//
		// What both contracts need is the registration-time capability assertion: a provider that does not
		// honor the ambient tenant must be rejected at startup rather than silently deduplicating one tenant's
		// message against another tenant's inbox row, or draining one tenant's outbox into another's transport.
		// Every specific-marker requirement for this strategy ran in AssertPerContractCapabilities at the top
		// of this method, and runs again against the finished collection at host start. Its result is carried
		// here because a deployment whose only tenant-owned stores are gated (not decorated) is a COVERED
		// deployment, not an empty one.
		gatedAny |= RequireEveryTenantOwnedContractPresentsACapability(services);

		if (!decoratedAny && !gatedAny)
		{
			// Enumerating contracts here would go stale the moment one is added, and the set is no longer
			// fixed: any contract declaring TenantOwnedAttribute counts. Describe the condition instead.
			throw new InvalidOperationException(
				$"{nameof(TenantIsolationStrategy.RowDiscriminator)} was selected but no tenant-owned store "
				+ "is registered, so there is nothing for it to scope. Register your "
				+ "persistence stores before calling AddMultiTenancy, or select a different tenant-isolation "
				+ "strategy.");
		}
	}

	/// <summary>
	/// Asserts, for every store contract present in <paramref name="services"/>, that its provider presented
	/// the specific tenancy capability that contract requires.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Extracted so there is exactly ONE list of per-contract requirements and two callers of it:
	/// <c>ApplyRowDiscriminator</c>, which runs it at composition time because a failure there carries the
	/// registration in view and is a far better diagnostic; and the startup validator, which runs it again
	/// against the completed collection. A service collection is a mutable list, so the composition-time run
	/// alone makes the gate a statement about one instant -- a store registered afterwards is never in the
	/// enumeration and reaches a started host unchecked. Reversing two registration calls must not change
	/// whether a safety gate runs.
	/// </para>
	/// <para>
	/// The attribute sweep is a floor and does not replace this: it accepts EITHER capability, whereas WHICH
	/// marker a contract must present is a property of that contract. For the outbox the two are not
	/// interchangeable -- an outbox attesting ambient scoping is making a claim that would be a defect if it
	/// were true -- so the floor cannot carry that contract at all, and only replaying this list can.
	/// </para>
	/// <para>
	/// It mutates nothing. Every condition is a descriptor predicate and every body either throws or does
	/// not, which is what makes it safe to evaluate a second time against a collection that has already been
	/// decorated.
	/// </para>
	/// </remarks>
	/// <param name="services"> The collection to assert over. </param>
	/// <returns>
	/// <see langword="true"/> when at least one gated-but-undecorated contract was present, which
	/// <c>ApplyRowDiscriminator</c> uses to tell a covered deployment from an empty one.
	/// </returns>
	internal static bool AssertPerContractCapabilities(IServiceCollection services)
	{
		// Decorated contracts first, in the order the decoration blocks used to assert them, so a host with
		// more than one fault still fails on the same contract it failed on before.
		if (services.HasStoreRegistrationFor(typeof(IEventStore)))
		{
			RequireTenantScopingCapability<IEventStore>(services, nameof(IEventStore));
		}

		// The cold tier gates and neither decorates nor counts toward coverage: a cold store that cannot scope
		// tenants cannot present the marker, so this exists to refuse the (row-discriminator + tiered +
		// non-tenant-aware cold) triple rather than to protect a store it wraps.
		if (services.HasStoreRegistrationFor(typeof(IColdEventStore)))
		{
			RequireTenantScopingCapability<IColdEventStore>(services, nameof(IColdEventStore));
		}

		if (services.HasStoreRegistrationFor(typeof(ISagaStore)))
		{
			RequireTenantScopingCapability<ISagaStore>(services, nameof(ISagaStore));
		}

		if (HasDecoratableProjectionStore(services))
		{
			RequireTenantScopingCapability<IProjectionStore<object>>(services, $"{nameof(IProjectionStore<>)}");
		}

		var gatedAny = false;

		if (services.HasStoreRegistrationFor(typeof(IInboxStore)))
		{
			RequireTenantScopingCapability<IInboxStore>(services, nameof(IInboxStore));
			gatedAny = true;
		}

		// The outbox is gated on a DIFFERENT capability from every other contract here, and the difference is
		// the whole point. ITenantScopingCapability attests that a store applies the AMBIENT tenant to every
		// operation. An outbox store that did that would read the ambient tenant as absent at drain time and
		// claim the empty set - the permanent stall described above - so ambient scoping is not merely absent
		// from these stores, it is behaviour that would be a defect if present. Requiring the ambient marker
		// here therefore demanded an attestation no correct outbox can truthfully make, and the three relational
		// providers made it anyway, through a seam that handed them a tenant context they discarded.
		// ITenantPartitionedCapability attests the mechanism a correct outbox does implement: the tenant
		// discriminator is persisted on each row and handed back on drain, so the owning tenant is
		// re-established from the row rather than from ambient state.
		if (services.HasStoreRegistrationFor(typeof(IOutboxStore)))
		{
			RequireTenantPartitionedCapability<IOutboxStore>(services, nameof(IOutboxStore));
			gatedAny = true;
		}

		// Erasure is gated and NOT decorated, for a reason unique to it: it runs from a background service with
		// no ambient tenant. A decorator would read the tenant as absent and either refuse every erasure or
		// widen it — and the widened form is the live defect: the erase request drops its tenant predicate when
		// the discriminator is null, tombstoning every tenant's copy of an aggregate in response to a single
		// tenant's right-to-erasure request.
		//
		// IEventStoreErasure is a CAPABILITY OF THE EVENT STORE, never a service type: all five providers
		// implement it on the store class, and the erasure contributor discovers it with `eventStore as
		// IEventStoreErasure`. Scanning the container for `ServiceType == typeof(IEventStoreErasure)` therefore
		// matches nothing, ever, and no provider registration could change that — the condition is a category
		// error, not a missing marker. Gate on the erasure FEATURE instead: opting into event-store erasure
		// registers IAggregateDataSubjectMapping, which is a real service type and is what a host that can
		// erase actually has.
		if (services.HasStoreRegistrationFor(typeof(IAggregateDataSubjectMapping)))
		{
			RequireTenantScopingCapability<IEventStoreErasure>(services, nameof(IEventStoreErasure));
			gatedAny = true;
		}

		// Erasure requests and legal holds are gated and NOT decorated, on the same reasoning as the inbox:
		// each provider store applies the ambient tenant predicate itself, on the row's own tenant column, so
		// a decorator would add a second filter without repairing the first. It would also be unable to tell
		// the tenant-facing surface from the estate-wide one — the erasure scheduler's due-request drain, the
		// certificate retention sweep and the legal-hold expiry sweep all run from background services with
		// no ambient tenant, and scoping those would not fail safe: an unswept expired hold blocks a tenant's
		// erasure indefinitely, and an undrained request is a right-to-erasure that never executes.
		//
		// What both contracts need is the registration-time assertion: a provider that does not thread the
		// ambient tenant must be rejected at startup rather than returning another tenant's erasure history,
		// or another tenant's legal holds, to whoever asks.
		if (services.HasStoreRegistrationFor(typeof(IErasureStore)))
		{
			RequireTenantScopingCapability<IErasureStore>(services, nameof(IErasureStore));
			gatedAny = true;
		}

		if (services.HasStoreRegistrationFor(typeof(ILegalHoldStore)))
		{
			RequireTenantScopingCapability<ILegalHoldStore>(services, nameof(ILegalHoldStore));
			gatedAny = true;
		}

		// Explicit gate blocks for three contracts the attribute sweep below already covered. They add no new
		// rejection -- HasStoreRegistrationFor skips the keyed-"default" forwarding aliases exactly as the
		// sweep does, so the set of refused hosts is unchanged -- but they name the contract in the failure,
		// and "ISnapshotStore is registered but its provider is not tenant-scoping-capable" is actionable
		// where the sweep's generic message is not. Manifesting them without gating them here would fail the
		// boundary guard, which requires the manifest to be a subset of the gated set.
		if (services.HasStoreRegistrationFor(typeof(ISnapshotStore)))
		{
			RequireTenantScopingCapability<ISnapshotStore>(services, nameof(ISnapshotStore));
			gatedAny = true;
		}

		if (services.HasStoreRegistrationFor(typeof(IDeadLetterQueue)))
		{
			RequireTenantScopingCapability<IDeadLetterQueue>(services, nameof(IDeadLetterQueue));
			gatedAny = true;
		}

		// The poison-message dead-letter STORE, distinct from the queue above and gated for a sharper
		// reason: its entries carry the failed message body, so an estate-wide read hands one tenant
		// another tenant's message content verbatim. Scoping is the correct assertion because every shipped
		// provider -- in-memory, PostgreSQL and SQL Server -- stamps the ambient term on write and binds it
		// on read, taking the tenant from context rather than from a filter the caller supplies. The
		// requirement bites only on a consumer's own store: all three shipped providers register through
		// the tenant-aware seam, which supplies the context and emits the attestation as one act, so the
		// attestation cannot be present for a store that never received the context.
		if (services.HasStoreRegistrationFor(typeof(IDeadLetterStore)))
		{
			RequireTenantScopingCapability<IDeadLetterStore>(services, nameof(IDeadLetterStore));
			gatedAny = true;
		}

		// Audit rows are tenant-derived, and all three shipped providers bind the ambient tenant term on
		// every query they build -- a scope taken from context, never a filter the caller supplies, so a
		// caller cannot widen it by omitting AuditQuery.TenantId. Scoping capability is therefore the
		// correct assertion: the partitioned one states the tenant is re-established from the row and never
		// inferred from ambient state, which is the opposite of what these stores do. A consumer supplying
		// its own audit store through AddAuditLogging<TAuditStore>() attests for it the same way any other
		// provider does, and is refused here if it does not.
		if (services.HasStoreRegistrationFor(typeof(IAuditStore)))
		{
			RequireTenantScopingCapability<IAuditStore>(services, nameof(IAuditStore));
			gatedAny = true;
		}

		// Consent records and subject-access-request tracking. Gated and NOT decorated, for the same reason as
		// erasure and legal hold: each provider binds the ambient tenant term inside its own store -- the Mongo
		// store composes it into the upsert key -- so a wrapper would add a second filter without repairing the
		// first.
		//
		// This block adds a rejection the attribute sweep below does not make, which is why it is worth its
		// lines. IComplianceStore carries [TenantOwned], so the sweep already refuses a provider presenting NO
		// capability -- but the sweep accepts EITHER capability, and only one of them is true here. Both shipped
		// providers read the ambient tenant, so a provider presenting the row-partitioned marker would pass the
		// sweep while attesting that it re-establishes the tenant from the row and never infers it from ambient
		// state -- the opposite of what these stores do. Demanding the scoping capability specifically is the
		// assertion that matches the mechanism, and it names the contract in the failure.
		if (services.HasStoreRegistrationFor(typeof(IComplianceStore)))
		{
			RequireTenantScopingCapability<IComplianceStore>(services, nameof(IComplianceStore));
			gatedAny = true;
		}

		// Runtime exhaustiveness IS asserted below, and its oracle is deliberately NOT a list this file keeps.
		//
		// An earlier revision built a HashSet of "gated" contracts by calling gated.Add(typeof(T)) alongside each
		// RequireTenantScopingCapability<T> call, then threw when TenantOwnedContracts.All was not a subset of it.
		// That assertion was TAUTOLOGICAL. The Add and the Require were independent statements: deleting the
		// Require left the Add in place, the set stayed complete, and the check passed while the contract went
		// ungated. It proved that a line had been TYPED, not that a gate EXISTED -- a check satisfied by the
		// checked thing doing less.
		//
		// The sweep below does not share that defect, because neither half of its oracle is maintained here. It
		// asks the REGISTRATION what is present, and it asks each CONTRACT whether it is tenant-owned -- the
		// [TenantOwned] attribute at the point of declaration. No edit to this method can hide a registered
		// tenant-owned contract from it, and a contract nobody remembered to name above is still caught. That is
		// the failure this coverage check actually suffered: a manifest cannot detect its own omission, because
		// the manifest IS the expected set.
		//
		// It is a FLOOR, not a replacement for the gates above. It accepts EITHER capability, asserting only that
		// the provider attested something. WHICH marker a contract must present is a property of that contract
		// and stays with the specific gates above -- widening those into one accept-either check is precisely the
		// failure mode they were split apart to remove.
		//
		// Its result feeds gatedAny: a deployment whose only tenant-owned stores are ones the explicit gates
		// above never name is a COVERED deployment, not an empty one. Without this, registering (say) only an
		// audit store -- correctly marked and correctly capable -- would fall through to the "no tenant-owned
		// store is registered" throw below and reject a configuration that is entirely valid.

		return gatedAny;
	}

	/// <summary>
	/// Asserts that a store contract present in the service collection was registered by a provider that
	/// declared itself tenant-scoping-capable (see <see cref="ITenantScopingCapability{TContract}"/>).
	/// Throws when the store is present but the capability marker is absent, so a tenant-unaware provider
	/// (for example, one that does not thread <see cref="ITenantContext"/>) is rejected at registration
	/// rather than silently returning cross-tenant data at runtime.
	/// </summary>
	private static void RequireTenantScopingCapability<TContract>(IServiceCollection services, string contractName)
	{
		if (!services.Any(static d => d.ServiceType == typeof(ITenantScopingCapability<TContract>)))
		{
			// The message states what the REJECTED registration lacks and what to do about it. It must not
			// say the capability "is not yet supported": tenant-aware providers exist and ship, so a consumer
			// whose provider IS capable would read that the framework cannot do what it can do, and go build
			// a workaround for a limitation that does not exist. Describe the contract, not a roadmap.
			throw new InvalidOperationException(
				$"{contractName} is registered but its provider is not tenant-scoping-capable, which "
				+ $"{nameof(TenantIsolationStrategy.RowDiscriminator)} requires. A provider presents it by "
				+ "registering its store through the tenant-scoped store registration seam, which supplies the "
				+ "ambient tenant to the store and emits the capability in the same act. Register this store "
				+ "through a tenant-aware provider registration, or select a different tenant-isolation "
				+ "strategy.");
		}
	}

	/// <summary>
	/// Fails closed when a contract whose tenancy is carried on the row - the outbox - is registered by a
	/// provider that does not present <see cref="ITenantPartitionedCapability{TContract}"/>.
	/// </summary>
	/// <remarks>
	/// Deliberately a separate gate from <c>RequireTenantScopingCapability</c> rather than a second accepted
	/// marker on the same one. The two capabilities attest different mechanisms, and for this contract only
	/// one of them can be true: a store applying the ambient discriminator would stall the cross-tenant
	/// drain. Accepting either marker here would let a store attest ambient scoping it cannot correctly
	/// perform, which is the state this gate was split to remove.
	/// </remarks>
	private static void RequireTenantPartitionedCapability<TContract>(IServiceCollection services, string contractName)
	{
		if (!services.Any(static d => d.ServiceType == typeof(ITenantPartitionedCapability<TContract>)))
		{
			throw new InvalidOperationException(
				$"{contractName} is registered but its provider does not persist the tenant discriminator on "
				+ $"each row, which {nameof(TenantIsolationStrategy.RowDiscriminator)} requires. This contract's "
				+ "reads are deliberately estate-wide - one drain pass carries every tenant's messages and the "
				+ "owning tenant is re-established from the row - so it is NOT scoped to the ambient tenant, and "
				+ "a provider presents the capability by registering its store through the tenant-partitioned "
				+ "store registration seam, which emits the capability in the same act. Register this store "
				+ "through a provider that carries the tenant on the row, or select a different tenant-isolation "
				+ "strategy.");
		}
	}

	/// <summary>
	/// Fails closed when any registered contract declared <see cref="TenantOwnedAttribute"/> presents neither
	/// <see cref="ITenantScopingCapability{TContract}"/> nor <see cref="ITenantPartitionedCapability{TContract}"/>.
	/// </summary>
	/// <remarks>
	/// Open-world by construction: coverage is derived from the registration and from the contract's own
	/// declaration, never from a list maintained beside the gates. A tenant-owned contract added to the
	/// framework, or declared by a consumer, is covered the moment it is declared.
	/// </remarks>
	/// <returns>
	/// <see langword="true"/> when at least one tenant-owned contract was registered, so the caller can treat
	/// such a deployment as covered rather than as one with no tenant-owned store at all.
	/// </returns>
	internal static bool RequireEveryTenantOwnedContractPresentsACapability(IServiceCollection services)
	{
		var sawTenantOwnedContract = false;

		// Forwarding aliases are skipped for the same reason the explicit gates above skip them: a
		// keyed-default alias promises a contract without providing a store for it, so counting one as a
		// registration makes this sweep demand a capability of a store the host never registered. That is
		// not hypothetical - the core event-sourcing registration emits an ISnapshotStore alias
		// unconditionally, and snapshots are optional, so an event store with no snapshot store was
		// refused here. The skip cannot weaken the sweep: an alias is resolvable only alongside a keyed
		// "default" descriptor, which this loop still sees, and no registration outside the alias seam can
		// present the forwarder marker.
		foreach (var serviceType in services
			.Where(static d => !d.IsKeyedDefaultForwardingAlias())
			.Select(static d => d.ServiceType)
			.Distinct())
		{
			var contract = ResolveTenantOwnedContract(serviceType);

			if (contract is null)
			{
				continue;
			}

			sawTenantOwnedContract = true;

			if (PresentsEitherCapability(services, contract))
			{
				continue;
			}

			// Names the offending contract, and separately the registration it was found on. Both, because they
			// are not always the same type and only one of them is actionable: a provider that registers its
			// concrete store type puts THAT type in the collection, while the requirement belongs to the
			// tenant-owned interface it implements. Naming only the registration tells a consumer a class name
			// and leaves them to guess which capability to satisfy; naming only the contract leaves them to
			// find which of their registrations produced it. "Some store is unscoped" is not actionable when
			// the container holds a hundred registrations, and neither is half of this.
			var registrationNote = serviceType == contract
				? string.Empty
				: $" It was found on the registration of {serviceType.Name}, which implements it.";

			throw new InvalidOperationException(
				$"{contract.Name} holds tenant-owned rows but its provider presents no tenant capability, "
				+ "which multi-tenancy requires. A provider presents one by "
				+ "registering the store through a tenant-aware registration seam, which supplies the ambient "
				+ "tenant and emits the capability in the same act: the tenant-scoped seam for a store whose reads "
				+ "are confined to the ambient tenant, or the tenant-partitioned seam for one whose reads are "
				+ "deliberately estate-wide and re-establish the owning tenant from the row. Register this store "
				+ "through such a seam, or select a different tenant-isolation strategy."
				+ registrationNote);
		}

		return sawTenantOwnedContract;
	}

	/// <summary>
	/// Returns the capability contract to look for when <paramref name="serviceType"/> is tenant-owned, or
	/// <see langword="null"/> when it is not.
	/// </summary>
	/// <remarks>
	/// Checks the service type and the interfaces it implements, so a consumer's own interface extending a
	/// tenant-owned contract is covered rather than slipping through on the attribute not being inherited.
	/// Generic contracts collapse to the family discriminator (the definition closed over <see cref="object"/>),
	/// which is the shape the capability markers are registered under.
	/// </remarks>
	// Both suppressions are scoped to this one startup-time sweep and are earned, not convenient.
	//
	// IL2070: the types examined here come from the service collection, so the container already holds a
	// reference to each one and its interface metadata is reachable for the same reason the registration is.
	// Annotating the parameter instead pushes the requirement into a LINQ chain, where IEnumerator.Current
	// cannot carry it -- which relocates the warning rather than answering it.
	//
	// IL2055: the constructed type is a comparison key. It is never instantiated and no member of it is
	// accessed, so nothing about it has to survive trimming, and the registration side constructs the same
	// key the same way.
	//
	// If either assumption were wrong the failure mode is a sweep that under-reports at startup, not a
	// crash in a request path.
	[UnconditionalSuppressMessage("AOT", "IL2070:UnrecognizedReflectionPattern",
		Justification = "Types come from the service collection, which roots them and their interface metadata.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:MakeGenericType", Justification = "The constructed type is only a comparison key; it is never instantiated and no member is accessed. The type argument is a reference type, so the runtime reuses the shared canonical instantiation and generates no new code.")]
	[UnconditionalSuppressMessage("AOT", "IL2055:MakeGenericType",
		Justification = "The constructed type is only a comparison key; it is never instantiated and no member is accessed.")]
	private static Type? ResolveTenantOwnedContract(Type serviceType)
	{
		foreach (var candidate in new[] { serviceType }.Concat(serviceType.GetInterfaces()))
		{
			var definition = candidate.IsGenericType && !candidate.IsGenericTypeDefinition
				? candidate.GetGenericTypeDefinition()
				: candidate;

			if (!definition.IsDefined(typeof(TenantOwnedAttribute), inherit: false))
			{
				continue;
			}

			return definition.IsGenericTypeDefinition
				? definition.MakeGenericType(typeof(object))
				: definition;
		}

		return null;
	}

	/// <summary>
	/// Reports whether the collection carries either capability marker for <paramref name="contract"/>.
	/// </summary>
	/// <remarks>
	/// Accepting either is correct HERE and only here: this is the floor asserting that the provider attested a
	/// mechanism at all. The per-contract gates above continue to demand the specific correct marker.
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL3050:MakeGenericType", Justification = "The constructed types are comparison keys matched against registered service types; neither is instantiated and no member of either is accessed. Both type arguments are reference types, so the runtime reuses the shared canonical instantiation and generates no new code.")]
	private static bool PresentsEitherCapability(IServiceCollection services, Type contract)
	{
		var scoping = typeof(ITenantScopingCapability<>).MakeGenericType(contract);
		var partitioned = typeof(ITenantPartitionedCapability<>).MakeGenericType(contract);

		return services.Any(d => d.ServiceType == scoping || d.ServiceType == partitioned);
	}

	/// <summary>
	/// Wraps every registered closed-generic <see cref="IProjectionStore{TProjection}"/> with
	/// <see cref="TenantScopedProjectionStore{TProjection}"/>, preserving the original registration's lifetime.
	/// </summary>
	private static bool DecorateProjectionStores(IServiceCollection services)
	{
		// A projection store is present only when a closed-generic IProjectionStore<T> that is not already
		// tenant-scoped is registered. Require the projection-store-family capability marker before wrapping
		// anything, so a tenant-unaware projection provider is rejected at registration rather than leaking
		// cross-tenant rows through the fail-closed decorator.
		var decoratedAny = false;

		for (var i = services.Count - 1; i >= 0; i--)
		{
			var descriptor = services[i];
			var serviceType = descriptor.ServiceType;

			if (!serviceType.IsGenericType
				|| serviceType.ContainsGenericParameters
				|| serviceType.GetGenericTypeDefinition() != typeof(IProjectionStore<>))
			{
				continue;
			}

			var implementationType = descriptor.GetImplementationType();

			// Idempotence: never wrap an already-scoped projection store.
			if (implementationType is { IsGenericType: true }
				&& implementationType.GetGenericTypeDefinition() == typeof(TenantScopedProjectionStore<>))
			{
				continue;
			}

			var projectionType = serviceType.GetGenericArguments()[0];

			// DISP003 suppressed, not satisfied. The analyzer wants an AOT annotation on this method;
			// it would propagate through ApplyRowDiscriminator to AddMultiTenancy, which is the public
			// entry point consumers call. Consumer-facing APIs do not carry AOT annotations here, so
			// satisfying the analyzer would annotate the public surface to quiet a diagnostic about a
			// private decoration helper.
			//
			// The risk is real and worth stating plainly: under native AOT, TenantScopedProjectionStore<T>
			// must have been generated for each projection type a consumer registers. The closed type is
			// derived from a service the consumer ALREADY registered, so the instantiation is not
			// open-ended -- but it is not guaranteed pre-generated either, and a trimmed app can fail
			// here at startup rather than at first use.
#pragma warning disable DISP003 // decorator closed over an already-registered projection type; see note
			var decoratorType = typeof(TenantScopedProjectionStore<>).MakeGenericType(projectionType);
#pragma warning restore DISP003

			ServiceDescriptor scoped;
			if (implementationType is not null)
			{
				scoped = ServiceDescriptor.Describe(
					serviceType,
					sp => ActivatorUtilities.CreateInstance(
						sp,
						decoratorType,
						ActivatorUtilities.CreateInstance(sp, implementationType)),
					descriptor.Lifetime);
			}
			else if (descriptor.GetImplementationFactory() is { } factory)
			{
				scoped = ServiceDescriptor.Describe(
					serviceType,
					sp => ActivatorUtilities.CreateInstance(sp, decoratorType, factory(sp)),
					descriptor.Lifetime);
			}
			else if (descriptor.GetImplementationInstance() is { } instance)
			{
				scoped = ServiceDescriptor.Describe(
					serviceType,
					sp => ActivatorUtilities.CreateInstance(sp, decoratorType, instance),
					descriptor.Lifetime);
			}
			else
			{
				continue;
			}

			services.RemoveAt(i);
			services.Insert(i, scoped);
			decoratedAny = true;
		}

		return decoratedAny;
	}

	/// <summary>
	/// Returns <see langword="true"/> when at least one closed-generic <see cref="IProjectionStore{TProjection}"/>
	/// that is not already tenant-scoped is registered — i.e. there is a projection store that would be wrapped.
	/// </summary>
	private static bool HasDecoratableProjectionStore(IServiceCollection services)
	{
		for (var i = 0; i < services.Count; i++)
		{
			var serviceType = services[i].ServiceType;

			if (!serviceType.IsGenericType
				|| serviceType.ContainsGenericParameters
				|| serviceType.GetGenericTypeDefinition() != typeof(IProjectionStore<>))
			{
				continue;
			}

			var implementationType = services[i].GetImplementationType();
			if (implementationType is { IsGenericType: true }
				&& implementationType.GetGenericTypeDefinition() == typeof(TenantScopedProjectionStore<>))
			{
				continue;
			}

			return true;
		}

		return false;
	}

	/// <summary>Marker registered once <see cref="AddMultiTenancy"/> has run, to keep the call idempotent.</summary>
	private sealed class MultiTenancyMarker;
}
