// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Bulk;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Observability;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Snapshots.Compression;
using Excalibur.EventSourcing.Snapshots.Security;
using Excalibur.EventSourcing.Snapshots.Upgrading;
using Excalibur.EventSourcing.Snapshots.Versioning;

using Microsoft.Extensions.DependencyInjection.Extensions;

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
	public static IServiceCollection AddSnapshotCompression(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<SnapshotCompressionOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

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
			new EventStoreTimeTravelQuery(sp.GetRequiredService<IEventStore>()));

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
	/// Decorates the registered ISnapshotStore with a factory-based wrapper.
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

		services.Add(new ServiceDescriptor(
			typeof(ISnapshotStore),
			sp =>
			{
				var inner = ResolveOriginal<ISnapshotStore>(descriptor, sp);
				return decoratorFactory(inner, sp);
			},
			descriptor.Lifetime));
	}

	/// <summary>
	/// Decorates the registered IEventStore with a factory-based wrapper.
	/// </summary>
	private static void DecorateEventStore(
		IServiceCollection services,
		Func<IEventStore, IServiceProvider, IEventStore> decoratorFactory)
	{
		var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(IEventStore));
		if (descriptor is null)
		{
			return; // No event store registered yet; skip decoration
		}

		_ = services.Remove(descriptor);

		services.Add(new ServiceDescriptor(
			typeof(IEventStore),
			sp =>
			{
				var inner = ResolveOriginal<IEventStore>(descriptor, sp);
				return decoratorFactory(inner, sp);
			},
			descriptor.Lifetime));
	}

	/// <summary>
	/// Resolves the original service instance from a removed service descriptor.
	/// </summary>
	private static TService ResolveOriginal<TService>(ServiceDescriptor descriptor, IServiceProvider sp)
		where TService : class
	{
		if (descriptor.ImplementationInstance is TService instance)
		{
			return instance;
		}

		if (descriptor.ImplementationFactory is not null)
		{
			return (TService)descriptor.ImplementationFactory(sp);
		}

		if (descriptor.ImplementationType is not null)
		{
			return (TService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
		}

		throw new InvalidOperationException(
			$"Cannot resolve original {typeof(TService).Name} from service descriptor.");
	}
}
