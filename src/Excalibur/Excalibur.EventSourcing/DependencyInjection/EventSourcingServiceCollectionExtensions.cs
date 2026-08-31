// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Implementation;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur event sourcing services.
/// </summary>
public static class EventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur event sourcing services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the core event sourcing infrastructure with sensible defaults:
	/// <list type="bullet">
	/// <item><see cref="ISnapshotStrategy"/> - <see cref="NoSnapshotStrategy"/> (no snapshots by default)</item>
	/// </list>
	/// </para>
	/// <para>
	/// Use <see cref="AddExcaliburEventSourcing(IServiceCollection, Action{IEventSourcingBuilder})"/>
	/// to configure repositories, snapshot strategies, and other options.
	/// </para>
	/// </remarks>
	internal static IServiceCollection AddExcaliburEventSourcing(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register Dispatch primitives first (IDispatcher, IMessageBus, etc.)
		_ = services.AddDispatchPipeline();

		// The startup validators below are IHostedService singletons that take an ILogger<T>. Registering them
		// without the logging services makes the IHostedService enumerable unresolvable for any consumer who
		// has not separately called AddLogging(). Add it here, as Microsoft's own AddHttpClient does, so this
		// entry point is self-sufficient; AddLogging is idempotent and adds no providers.
		_ = services.AddLogging();

		// Register default snapshot strategy (no snapshots)
		services.TryAddSingleton<ISnapshotStrategy>(NoSnapshotStrategy.Instance);

		// fail loud at host start if the consumer forgot to pick an event store.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, EventSourcingPrerequisiteValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, EventSourcingPrerequisiteValidator>());

		// Fail host start when GDPR crypto-shredding is configured but the event store is not wired for
		// at-rest field encryption (consumer configured crypto-shred but omitted AddEventSourcingCryptoShredding()).
		// Always-on so the silent-inert PII gap fails closed even when the opt-in wiring is forgotten.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, CryptoShreddingWiringValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, CryptoShreddingWiringValidator>());

		// Sibling always-on guards for the inbox and outbox surfaces: fail host start when crypto-shredding is
		// configured but a registered inbox/outbox store is NOT wired for at-rest field encryption. Registered
		// here (not inside AddInboxEncryption/AddOutboxEncryption) precisely because the fault they catch is the
		// SKIPPED wire — a guard living inside the wire would be skipped alongside it. Each probes its store
		// KEYED (the "default" registration), so a keyed-only store is not mistaken for absent.
		_ = services.AddStoreEncryptionWiringGuards();

		// Fail loud at host start when the default type-rejecting JSON serializer is paired with an empty
		// event-type registry — a configuration that bricks every aggregate replay at runtime. Converts a
		// silent runtime failure into an honest startup failure that names the fix (RegisterEventTypes*).
		services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, EventTypeRegistrationValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, EventTypeRegistrationValidator>());

		// fail fast at startup when OutboxStagingStrategy.Transactional is explicitly
		// selected without the transactional infrastructure (ITransactionalOutboxWriter + a transactional event
		// store), instead of silently degrading to non-atomic eventually-consistent staging.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<EventSourcedRepositoryOptions>, TransactionalStagingCapabilityValidator>());

		// fail fast at startup when OutboxStagingStrategy.EventuallyConsistent is explicitly
		// selected without a registered IOutboxStore, instead of silently skipping outbox staging (integration
		// events would be lost with no diagnostic).
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<EventSourcedRepositoryOptions>, EventSourcedRepositoryStagingCapabilityValidator>());
		_ = services.AddOptions<EventSourcedRepositoryOptions>().ValidateOnStart();

		// Non-keyed convenience aliases: forward to keyed "default" so consumers
		// can inject IEventStore / ISnapshotStore directly without [FromKeyedServices("default")].
		// All providers register their store as keyed "default" via TryAddKeyedSingleton,
		// so these single forwarding registrations work for all providers.
		//
		// Unconditional, and it has to be: registering the keyed "default" store AFTER this call is a
		// supported ordering (a consumer may hand-register one, and every provider extension runs later),
		// so there is nothing here to condition on yet. What that costs is a descriptor for a contract the
		// host may never back - most visibly ISnapshotStore, which is optional, so an event-sourced host
		// with an event store and no snapshot store still carries an ISnapshotStore alias.
		//
		// AddKeyedDefaultAlias is what keeps that from misleading a registration-time gate: it marks the
		// descriptor as a forwarder, so a gate walking the collection can tell "a store is registered" from
		// "something promises this contract and nothing provides it". Registering these with a bare
		// TryAddSingleton would make an unbacked alias indistinguishable from a real store, and the
		// fail-closed multi-tenancy gate would demand a tenant capability of a store nobody registered.
		_ = services.AddKeyedDefaultAlias<IEventStore>();
		_ = services.AddKeyedDefaultAlias<ISnapshotStore>();

		return services;
	}

	/// <summary>
	/// Adds Excalibur event sourcing services with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the event sourcing builder.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring Excalibur event sourcing. It allows you to
	/// register repositories, configure snapshot strategies, and set up event upcasting.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(builder =>
	/// {
	///     // Register repositories with explicit factory
	///     builder.AddRepository&lt;OrderAggregate, Guid&gt;(id => new OrderAggregate(id));
	///
	///     // Or use static factory from IAggregateRoot&lt;TAggregate, TKey&gt;
	///     builder.AddRepository&lt;CustomerAggregate, Guid&gt;();
	///
	///     // Configure snapshot strategy
	///     builder.UseIntervalSnapshots(100);
	///
	///     // Or use composite strategy
	///     builder.UseCompositeSnapshotStrategy(s => s
	///         .AddIntervalStrategy(50)
	///         .AddTimeBasedStrategy(TimeSpan.FromMinutes(5))
	///         .RequireAll());
	///
	///     // Configure event upcasting
	///     builder.AddUpcastingPipeline(u => u
	///         .RegisterUpcaster&lt;OrderCreatedV1, OrderCreatedV2&gt;(new OrderCreatedV1ToV2())
	///         .EnableAutoUpcastOnReplay());
	/// }));
	/// </code>
	/// </para>
	/// </remarks>
	internal static IServiceCollection AddExcaliburEventSourcing(
		this IServiceCollection services,
		Action<IEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure base services are registered
		_ = services.AddExcaliburEventSourcing();

		// Configure using the builder pattern
		var builder = new ExcaliburEventSourcingBuilder(services);
		configure(builder);

		return services;
	}

	/// <summary>
	/// Checks if Excalibur event sourcing services have been registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>True if event sourcing services are registered; otherwise false.</returns>
	public static bool HasExcaliburEventSourcing(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		return services.Any(s => s.ServiceType == typeof(ISnapshotStrategy));
	}
}
