// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.TieredStorage;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
	///     with its fail-closed tenant-scoping decorator. Fails fast when none of those stores is registered.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///     <see cref="TenantIsolationStrategy.Sharding"/> — requires tenant-aware routing to have been enabled
	///     on the event-sourcing builder (<c>AddEventSourcing(es =&gt; es.EnableTenantSharding(...))</c>) and
	///     asserts it is present.
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

		services.AddSingleton<MultiTenancyMarker>();
		return services;
	}

	private static void ApplyRowDiscriminator(IServiceCollection services)
	{
		var decoratedAny = false;


		if (services.Any(static d => d.ServiceType == typeof(IEventStore)))
		{
			// A store is present: only wrap it when its provider proved (at registration) that it honors
			// the tenant discriminator. Wrapping a tenant-unaware store would pass the decorator's
			// fail-closed presence check yet leak every tenant's data from the inner store.
			RequireTenantScopingCapability<IEventStore>(services, nameof(IEventStore));

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
		if (services.Any(static d => d.ServiceType == typeof(IColdEventStore)))
		{
			RequireTenantScopingCapability<IColdEventStore>(services, nameof(IColdEventStore));
		}

		// Registered for every row-discriminator host, whether or not a cold tier is visible AT THIS INSTANT.
		// TryAddEnumerable keeps repeat AddMultiTenancy calls idempotent. The guard no-ops when no cold tier is
		// resolvable, so the cost to a host that never uses tiered storage is one type check at start.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ColdStoreTenantScopingValidator>());

		if (services.Any(static d => d.ServiceType == typeof(ISagaStore)))
		{
			RequireTenantScopingCapability<ISagaStore>(services, nameof(ISagaStore));

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
		var gatedAny = false;

		if (services.Any(static d => d.ServiceType == typeof(IInboxStore)))
		{
			RequireTenantScopingCapability<IInboxStore>(services, nameof(IInboxStore));
			gatedAny = true;
		}

		if (services.Any(static d => d.ServiceType == typeof(IOutboxStore)))
		{
			RequireTenantScopingCapability<IOutboxStore>(services, nameof(IOutboxStore));
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
		if (services.Any(static d => d.ServiceType == typeof(IAggregateDataSubjectMapping)))
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
		if (services.Any(static d => d.ServiceType == typeof(IErasureStore)))
		{
			RequireTenantScopingCapability<IErasureStore>(services, nameof(IErasureStore));
			gatedAny = true;
		}

		if (services.Any(static d => d.ServiceType == typeof(ILegalHoldStore)))
		{
			RequireTenantScopingCapability<ILegalHoldStore>(services, nameof(ILegalHoldStore));
			gatedAny = true;
		}

		// There is NO runtime exhaustiveness assertion here, deliberately, and the reason is worth the comment.
		//
		// An earlier revision of this method built a HashSet of "gated" contracts by calling gated.Add(typeof(T))
		// alongside each RequireTenantScopingCapability<T> call, then threw when TenantOwnedContracts.All was not
		// a subset of it. That assertion was TAUTOLOGICAL. The Add and the Require were independent statements:
		// deleting the Require left the Add in place, the set stayed complete, and the check passed while the
		// contract went ungated. It proved that a line had been TYPED, not that a gate EXISTED — a check
		// satisfied by the checked thing doing less.
		//
		// "Every contract in the manifest has a RequireTenantScopingCapability<T> call site in this method" is a
		// STATIC property of this source file. It cannot be established by a runtime counter that this same file
		// maintains, because the oracle would be the artifact under test. It belongs in a boundary guard that
		// scans this file for those call sites and compares them against TenantOwnedContracts.All — an oracle
		// that survives any edit made here.
		//
		// The manifest's own completeness is NOT bounded by anything today. This is a known, unclosed gap, and
		// the reader should not infer a control that does not exist. An omission from TenantOwnedContracts.All is
		// invisible to every check that takes the manifest as its expected set — including the boundary guard,
		// whose arms all read the manifest as their oracle. Closing it requires a guard whose oracle is the
		// namespace rather than the manifest: every persistence contract storing tenant-owned rows must appear in
		// the manifest or on an explicit exclusion list naming a reason. Until that guard exists, the only thing
		// standing between a new tenant-owned contract and a silent cross-tenant leak is a person reading this
		// array. That is exactly how the IEventStoreErasure omission was found, and it is not a control.

		if (!decoratedAny && !gatedAny)
		{
			throw new InvalidOperationException(
				$"{nameof(TenantIsolationStrategy.RowDiscriminator)} was selected but no tenant-owned store "
				+ $"({nameof(IEventStore)}, {nameof(IProjectionStore<>)}, {nameof(ISagaStore)}, "
				+ $"{nameof(IInboxStore)}, or {nameof(IOutboxStore)}) is "
				+ "registered. Register your persistence stores before calling AddMultiTenancy.");
		}
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
	/// Wraps every registered closed-generic <see cref="IProjectionStore{TProjection}"/> with
	/// <see cref="TenantScopedProjectionStore{TProjection}"/>, preserving the original registration's lifetime.
	/// </summary>
	private static bool DecorateProjectionStores(IServiceCollection services)
	{
		// A projection store is present only when a closed-generic IProjectionStore<T> that is not already
		// tenant-scoped is registered. Require the projection-store-family capability marker before wrapping
		// anything, so a tenant-unaware projection provider is rejected at registration rather than leaking
		// cross-tenant rows through the fail-closed decorator.
		if (HasDecoratableProjectionStore(services))
		{
			RequireTenantScopingCapability<IProjectionStore<object>>(services, $"{nameof(IProjectionStore<>)}");
		}

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
