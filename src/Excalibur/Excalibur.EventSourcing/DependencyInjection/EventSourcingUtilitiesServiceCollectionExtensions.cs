// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Encryption.Decorators;
using Excalibur.EventSourcing.Bulk;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Observability;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Snapshots.Compression;
using Excalibur.EventSourcing.Snapshots.Security;
using Excalibur.EventSourcing.Snapshots.Upgrading;
using Excalibur.EventSourcing.Snapshots.Versioning;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring event sourcing utility services.
/// </summary>
public static class EventSourcingUtilitiesServiceCollectionExtensions
{
	/// <summary>
	/// Adds the snapshot upgrader registry for typed snapshot version migration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSnapshotUpgraderRegistry(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<SnapshotUpgraderRegistry>();

		return services;
	}

	/// <summary>
	/// Decorates the registered <see cref="ISnapshotStore"/> with encryption support.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Requires an <see cref="ISnapshotEncryptor"/> to be registered.
	/// The decorator wraps the existing <see cref="ISnapshotStore"/> registration.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSnapshotEncryption(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		DecorateSnapshotStore(services, static (inner, sp) =>
			new EncryptingSnapshotStore(inner, sp.GetRequiredService<ISnapshotEncryptor>()));

		return services;
	}

	/// <summary>
	/// Decorates the registered <see cref="IEventStore"/> with per-subject field-level encryption so that a data
	/// subject's <c>[PersonalData]</c> event fields are encrypted at rest under the subject's own key, and destroying
	/// that key (GDPR right to erasure) renders only that subject's personal fields unrecoverable across all events.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// Mirrors <see cref="AddSnapshotEncryption"/>: the decorator wraps the existing <see cref="IEventStore"/>
	/// registration and sits immediately above the store (encryption is the innermost decorator, so bytes reach the
	/// store already encrypted). Requires <see cref="SubjectFieldCryptor"/>, an <see cref="IEventSerializer"/>, an
	/// <see cref="IEncryptionProviderRegistry"/>, and <see cref="EncryptionOptions"/> to be registered.
	/// <para>
	/// Deliberately <see langword="internal"/>: the only correct call order registers the per-subject cryptor
	/// first, so the supported public entry point is <see cref="AddEventSourcingCryptoShredding"/>, which wires the
	/// cryptor and this decorator atomically. Exposing this standalone would let a consumer construct a container
	/// that boots with plaintext PII (cryptor registered after the store) or throws on first append (decorator
	/// registered without a cryptor) — foot-guns removed by keeping the seam off the public surface.
	/// </para>
	/// </remarks>
	internal static IServiceCollection AddEventStoreEncryption(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Decorate FIRST, then record the marker — and only if the decoration actually happened. Registering
		// the marker unconditionally would make it attest that this method was CALLED rather than that the
		// event store is ENCRYPTED: when no IEventStore is registered yet, decoration is skipped and the
		// marker would still claim at-rest encryption, so the startup guard would pass over plaintext PII.
		// No proxy may stand in for the property it represents.
		var decorated = DecorateEventStore(services, static (inner, sp) =>
			new EncryptingEventStoreDecorator(
				inner,
				sp.GetRequiredService<IEncryptionProviderRegistry>(),
				sp.GetRequiredService<SubjectFieldCryptor>(),
				sp.GetRequiredService<IEventSerializer>(),
				sp.GetRequiredService<IOptions<EncryptionOptions>>()));

		// Composition time cannot decide this. A consumer may legitimately enable encryption before selecting
		// a store provider, so "nothing was decorated yet" is not an error here — it only means the marker
		// must not be registered, because no event store is encrypted. Whether that state is a fault is a
		// question about the finished container, and only a startup validator can ask it.
		if (decorated)
		{
			services.TryAddSingleton<EventStoreEncryptionMarker>();
		}

		return services;
	}

	/// <summary>
	/// Registers the per-subject crypto-shred services and wires at-rest field encryption for the event store,
	/// the inbox, and the outbox.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Call this AFTER the base store registrations and BEFORE any tenant-scoping decoration, so encryption lands
	/// as the innermost decorator (bytes reach each store encrypted; tenant context resolves outside it). Each
	/// encrypting decorator sits immediately above its store; an outer tenant-scoping decorator, if present,
	/// wraps it.
	/// </para>
	/// <para>
	/// <strong>Only the event store is keyed per data subject.</strong> Destroying a subject's key there
	/// crypto-shreds that subject's event history. The inbox and outbox decorators encrypt under a single
	/// configured context, not per subject — destroying one subject's key does <em>not</em> render that
	/// subject's inbox or outbox payloads unrecoverable, because they were never encrypted under it. Where an
	/// erasure guarantee must extend to messages in flight on those two surfaces, bound retention instead, so
	/// the payloads age out.
	/// </para>
	/// <para>
	/// This covers the event store, inbox, and outbox automatically. Projection stores are registered per
	/// projection type, so they cannot be auto-covered: call <c>AddProjectionEncryption&lt;TProjection&gt;()</c>
	/// per registered projection type that carries personal data — it carries the same single-context caveat.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddEventSourcingCryptoShredding(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register the per-subject crypto-shred services (SubjectFieldCryptor, IFieldEncryptor, key manager);
		// idempotent/override-safe via the Compliance registration's TryAdd semantics.
		_ = services.AddCryptoShredding();

		// Wire the encrypting decorators so the crypto-shred capability is LIVE at rest across every durable
		// message-bearing store, never configured-but-inert. Inbox/outbox encryption lives in the Compliance
		// package (existing dependency direction); projections stay explicit (open-generic per type).
		_ = services.AddEventStoreEncryption();
		_ = services.AddInboxEncryption();
		_ = services.AddOutboxEncryption();

		return services;
	}

	/// <summary>
	/// Decorates the registered <see cref="IProjectionStore{TProjection}"/> for the given projection type with
	/// at-rest field encryption under a single configured context.
	/// </summary>
	/// <typeparam name="TProjection">The projection type whose store is decorated.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Requires an <see cref="IEncryptionProviderRegistry"/> and <see cref="EncryptionOptions"/> to be registered.
	/// Projection stores are registered per projection type, so encryption is opt-in per type and cannot be
	/// auto-wired by <see cref="AddEventSourcingCryptoShredding"/> (which covers the event store, inbox, and
	/// outbox): call this once for each registered projection type that carries personal data. The decorator
	/// wraps the existing registration at its own lifetime and sits immediately above the store (encryption is
	/// innermost, so bytes reach the store already encrypted; a tenant-scoping decorator, if present, wraps it).
	/// </para>
	/// <para>
	/// <strong>This is not keyed per data subject.</strong> Encryption here uses a single configured context,
	/// not a per-subject key, so destroying one subject's crypto-shred key leaves this projection's payloads
	/// recoverable — projections are rebuildable from the event stream, so erasure on this surface is achieved
	/// by rebuilding from a stream with the subject's events erased, not by key destruction.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddProjectionEncryption<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection>(
		this IServiceCollection services)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);

		DecorateProjectionStore<TProjection>(services, static (inner, sp) =>
			new EncryptingProjectionStoreDecorator<TProjection>(
				inner,
				sp.GetRequiredService<IEncryptionProviderRegistry>(),
				sp.GetRequiredService<IOptions<EncryptionOptions>>()));

		return services;
	}

	/// <summary>
	/// Decorates the registered <see cref="ISnapshotStore"/> with compression support.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Uses <see cref="SnapshotCompressionOptions"/> for configuration.
	/// Call <c>services.Configure&lt;SnapshotCompressionOptions&gt;()</c> to customize.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSnapshotCompression(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<SnapshotCompressionOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SnapshotCompressionOptions>>(new Excalibur.EventSourcing.Snapshots.Compression.SnapshotCompressionOptionsValidator()));

		DecorateSnapshotStore(services, static (inner, sp) =>
			new CompressingSnapshotStore(
				inner,
				sp.GetRequiredService<Options.IOptions<SnapshotCompressionOptions>>()));

		return services;
	}

	/// <summary>
	/// Decorates the registered <see cref="ISnapshotStore"/> with compression support.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure compression options.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSnapshotCompression(
		this IServiceCollection services,
		Action<SnapshotCompressionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		return services.AddSnapshotCompression();
	}

	/// <summary>
	/// Decorates the registered <see cref="ISnapshotStore"/> with compression support
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSnapshotCompression(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SnapshotCompressionOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SnapshotCompressionOptions>>(new Excalibur.EventSourcing.Snapshots.Compression.SnapshotCompressionOptionsValidator()));

		DecorateSnapshotStore(services, static (inner, sp) =>
			new CompressingSnapshotStore(
				inner,
				sp.GetRequiredService<Options.IOptions<SnapshotCompressionOptions>>()));

		return services;
	}

	/// <summary>
	/// Adds snapshot schema versioning support.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSnapshotSchemaVersioning(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISnapshotSchemaValidator, AttributeBasedSnapshotSchemaValidator>();
		services.TryAddSingleton<AttributeBasedSnapshotSchemaValidator>();

		return services;
	}

	/// <summary>
	/// Decorates the registered <see cref="IEventStore"/> with throughput metrics.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="providerName">The provider name for metric tags (e.g., "sqlserver", "inmemory").</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Adds dedicated throughput counters (<c>events_appended</c>, <c>events_loaded</c>)
	/// and duration histograms (<c>append_duration</c>, <c>load_duration</c>) to the event store.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddEventStoreThroughputMetrics(
		this IServiceCollection services,
		string providerName = "default")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrEmpty(providerName);

		DecorateEventStore(services, (inner, sp) =>
		{
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(EventSourcingMeters.EventStore)
						?? new Meter(EventSourcingMeters.EventStore);
			return new EventStoreThroughputMetrics(inner, meter, providerName);
		});

		return services;
	}

	/// <summary>
	/// Adds time-travel query support.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddTimeTravelQuery(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ITimeTravelQuery>(sp =>
			new EventStoreTimeTravelQuery(sp.GetRequiredKeyedService<IEventStore>("default")));

		return services;
	}

	/// <summary>
	/// Adds aggregate bulk operations support.
	/// </summary>
	/// <typeparam name="TAggregate">The aggregate type.</typeparam>
	/// <typeparam name="TKey">The aggregate key type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	[RequiresUnreferencedCode("Bulk operations may require types that cannot be statically analyzed.")]
	public static IServiceCollection AddAggregateBulkOperations<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate, TKey>(this IServiceCollection services)
		where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
		where TKey : notnull
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IAggregateBulkOperations<TAggregate, TKey>>(sp =>
			new AggregateBulkOperations<TAggregate, TKey>(
				sp.GetRequiredService<IEventSourcedRepository<TAggregate, TKey>>()));

		return services;
	}

	/// <summary>
	/// Decorates the registered <see cref="ISnapshotStore"/> with a factory-based wrapper, preserving the
	/// descriptor's service key (if keyed) and lifetime. A keyed registration is re-registered keyed so a
	/// consumer resolving the keyed store observes the decorated instance; re-registering non-keyed would
	/// leave the keyed resolution pointing at the undecorated store, silently bypassing a decorator that
	/// carries a correctness guarantee (e.g. field-level encryption / crypto-shred).
	/// </summary>
	private static void DecorateSnapshotStore(
		IServiceCollection services,
		Func<ISnapshotStore, IServiceProvider, ISnapshotStore> decoratorFactory)
	{
		var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(ISnapshotStore));
		if (descriptor is null)
		{
			return; // No snapshot store registered yet; skip decoration
		}

		_ = services.Remove(descriptor);

		services.Add(descriptor.IsKeyedService
			? ServiceDescriptor.DescribeKeyed(
				typeof(ISnapshotStore),
				descriptor.ServiceKey,
				(sp, _) => decoratorFactory(ResolveOriginal<ISnapshotStore>(descriptor, sp), sp),
				descriptor.Lifetime)
			: ServiceDescriptor.Describe(
				typeof(ISnapshotStore),
				sp => decoratorFactory(ResolveOriginal<ISnapshotStore>(descriptor, sp), sp),
				descriptor.Lifetime));
	}

	/// <summary>
	/// Decorates the registered <see cref="IEventStore"/> with a factory-based wrapper, preserving the
	/// descriptor's service key (if keyed) and lifetime. A keyed registration is re-registered keyed so a
	/// consumer resolving the keyed store observes the decorated instance; re-registering non-keyed would
	/// leave the keyed resolution pointing at the undecorated store, silently bypassing a decorator that
	/// carries a correctness guarantee (e.g. field-level encryption / crypto-shred).
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if an <see cref="IEventStore"/> was registered and has been decorated;
	/// <see langword="false"/> if there was nothing to decorate. Callers whose decorator carries a
	/// correctness guarantee must not treat <see langword="false"/> as success.
	/// </returns>
	private static bool DecorateEventStore(
		IServiceCollection services,
		Func<IEventStore, IServiceProvider, IEventStore> decoratorFactory)
	{
		var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(IEventStore));
		if (descriptor is null)
		{
			return false; // No event store registered yet; nothing was decorated.
		}

		_ = services.Remove(descriptor);

		services.Add(descriptor.IsKeyedService
			? ServiceDescriptor.DescribeKeyed(
				typeof(IEventStore),
				descriptor.ServiceKey,
				(sp, _) => decoratorFactory(ResolveOriginal<IEventStore>(descriptor, sp), sp),
				descriptor.Lifetime)
			: ServiceDescriptor.Describe(
				typeof(IEventStore),
				sp => decoratorFactory(ResolveOriginal<IEventStore>(descriptor, sp), sp),
				descriptor.Lifetime));

		return true;
	}

	/// <summary>
	/// Decorates the registered <see cref="IProjectionStore{TProjection}"/> for one projection type, preserving
	/// the descriptor's service key (if keyed) and lifetime. The undecorated factory is captured from the
	/// original descriptor so the decorator's inner reference never re-enters the decorated registration.
	/// </summary>
	private static void DecorateProjectionStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection>(
		IServiceCollection services,
		Func<IProjectionStore<TProjection>, IServiceProvider, IProjectionStore<TProjection>> decoratorFactory)
		where TProjection : class
	{
		var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(IProjectionStore<TProjection>));
		if (descriptor is null)
		{
			return; // No projection store registered for this type; nothing to encrypt.
		}

		_ = services.Remove(descriptor);

		var originalFactory = BuildProjectionOriginalFactory<TProjection>(descriptor);

		services.Add(descriptor.IsKeyedService
			? ServiceDescriptor.DescribeKeyed(
				typeof(IProjectionStore<TProjection>),
				descriptor.ServiceKey,
				(sp, _) => decoratorFactory(originalFactory(sp), sp),
				descriptor.Lifetime)
			: ServiceDescriptor.Describe(
				typeof(IProjectionStore<TProjection>),
				sp => decoratorFactory(originalFactory(sp), sp),
				descriptor.Lifetime));
	}

	/// <summary>
	/// Produces a factory for the undecorated projection store from whichever registration form the descriptor
	/// uses, read through the keyed-safe accessors (raw reads throw on keyed descriptors on .NET 8+).
	/// </summary>
	private static Func<IServiceProvider, IProjectionStore<TProjection>> BuildProjectionOriginalFactory<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection>(
		ServiceDescriptor descriptor)
		where TProjection : class
	{
		if (descriptor.GetImplementationInstance() is IProjectionStore<TProjection> instance)
		{
			return _ => instance;
		}

		if (descriptor.GetImplementationFactory() is { } factory)
		{
			return sp => (IProjectionStore<TProjection>)factory(sp);
		}

		var implementationType = descriptor.GetImplementationType();
		if (implementationType is not null)
		{
			return sp => (IProjectionStore<TProjection>)ActivatorUtilities.CreateInstance(sp, implementationType);
		}

		throw new InvalidOperationException(
			$"Cannot resolve original IProjectionStore<{typeof(TProjection).Name}> from service descriptor.");
	}

	/// <summary>
	/// Resolves the original service instance from a removed service descriptor.
	/// </summary>
	private static TService ResolveOriginal<TService>(ServiceDescriptor descriptor, IServiceProvider sp)
		where TService : class
	{
		// read implementation members through the keyed-safe accessors (raw reads throw on
		// keyed descriptors on .NET 8+).
		if (descriptor.GetImplementationInstance() is TService instance)
		{
			return instance;
		}

		if (descriptor.GetImplementationFactory() is { } factory)
		{
			return (TService)factory(sp);
		}

		var implementationType = descriptor.GetImplementationType();
		if (implementationType is not null)
		{
			return (TService)ActivatorUtilities.CreateInstance(sp, implementationType);
		}

		throw new InvalidOperationException(
			$"Cannot resolve original {typeof(TService).Name} from service descriptor.");
	}
}
