// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Snapshots;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventSourcingBuilderExtensions
{
	/// <summary>
	/// Configures a custom snapshot strategy.
	/// </summary>
	/// <typeparam name="TStrategy"> The snapshot strategy implementation type. </typeparam>
	/// <param name="builder"> The builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder AddSnapshotStrategy<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TStrategy>(this IEventSourcingBuilder builder)
		where TStrategy : class, ISnapshotStrategy
	{
		ArgumentNullException.ThrowIfNull(builder);
		ReplaceSnapshotStrategy(builder.Services, ServiceDescriptor.Singleton<ISnapshotStrategy, TStrategy>());
		return builder;
	}

	/// <summary>
	/// Configures an interval-based snapshot strategy.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="eventInterval"> The number of events between snapshots. Default: 100. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder UseIntervalSnapshots(this IEventSourcingBuilder builder, int eventInterval = 100)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ReplaceSnapshotStrategy(builder.Services, ServiceDescriptor.Singleton<ISnapshotStrategy>(new IntervalSnapshotStrategy(eventInterval)));
		return builder;
	}

	/// <summary>
	/// Configures a time-based snapshot strategy.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="timeInterval"> The time interval between snapshots. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder UseTimeBasedSnapshots(this IEventSourcingBuilder builder, TimeSpan timeInterval)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ReplaceSnapshotStrategy(builder.Services, ServiceDescriptor.Singleton<ISnapshotStrategy>(new TimeBasedSnapshotStrategy(timeInterval)));
		return builder;
	}

	/// <summary>
	/// Configures a size-based snapshot strategy.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="maxSizeInBytes"> The maximum size in bytes before creating a snapshot. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder UseSizeBasedSnapshots(this IEventSourcingBuilder builder, long maxSizeInBytes)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ReplaceSnapshotStrategy(builder.Services, ServiceDescriptor.Singleton<ISnapshotStrategy>(new SizeBasedSnapshotStrategy(maxSizeInBytes)));
		return builder;
	}

	/// <summary>
	/// Configures a composite snapshot strategy combining multiple strategies.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="configure"> Action to configure the composite strategy. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder UseCompositeSnapshotStrategy(this IEventSourcingBuilder builder, Action<CompositeSnapshotStrategyBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var strategyBuilder = new CompositeSnapshotStrategyBuilder();
		configure(strategyBuilder);
		ReplaceSnapshotStrategy(builder.Services, ServiceDescriptor.Singleton<ISnapshotStrategy>(strategyBuilder.Build()));
		return builder;
	}

	/// <summary>
	/// Configures a no-op snapshot strategy that never creates snapshots.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder UseNoSnapshots(this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ReplaceSnapshotStrategy(builder.Services, ServiceDescriptor.Singleton<ISnapshotStrategy>(NoSnapshotStrategy.Instance));
		return builder;
	}

	/// <summary>
	/// Configures a custom snapshot manager.
	/// </summary>
	/// <typeparam name="TManager"> The snapshot manager implementation type. </typeparam>
	/// <param name="builder"> The builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder UseSnapshotManager<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TManager>(this IEventSourcingBuilder builder)
		where TManager : class, ISnapshotManager
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.Services.TryAddSingleton<ISnapshotManager, TManager>();
		return builder;
	}

	/// <summary>
	/// Configures a custom event serializer implementation.
	/// </summary>
	/// <typeparam name="TSerializer"> The serializer implementation type. </typeparam>
	/// <param name="builder"> The builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder UseEventSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TSerializer>(this IEventSourcingBuilder builder)
		where TSerializer : class, IEventSerializer
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.Services.TryAddSingleton<IEventSerializer, TSerializer>();
		return builder;
	}

	/// <summary>
	/// Configures a custom transactional outbox writer for event sourcing.
	/// </summary>
	/// <typeparam name="TOutboxWriter"> The transactional outbox writer implementation type. </typeparam>
	/// <param name="builder"> The builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// Most consumers should register the unified outbox via <c>AddExcaliburOutbox(o => o.UseSqlServer(...))</c>
	/// which automatically registers <see cref="ITransactionalOutboxWriter"/>. Use this method only for
	/// custom implementations.
	/// </remarks>
	public static IEventSourcingBuilder UseTransactionalOutboxWriter<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TOutboxWriter>(this IEventSourcingBuilder builder)
		where TOutboxWriter : class, ITransactionalOutboxWriter
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.Services.TryAddSingleton<ITransactionalOutboxWriter, TOutboxWriter>();
		return builder;
	}

	/// <summary>
	/// Configures the upcasting pipeline for event versioning.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="configure"> Action to configure the upcasting builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder AddUpcastingPipeline(this IEventSourcingBuilder builder, Action<UpcastingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMessageUpcasting(configure);
		return builder;
	}

	/// <summary>
	/// Configures snapshot upgrading for automatic snapshot version migration.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="configure"> Action to configure the snapshot upgrading builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IEventSourcingBuilder AddSnapshotUpgrading(this IEventSourcingBuilder builder, Action<SnapshotUpgradingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SnapshotUpgradingOptions();
		var upgradingBuilder = new SnapshotUpgradingBuilder(options);
		configure(upgradingBuilder);

		_ = builder.Services.AddOptions<SnapshotUpgradingOptions>()
			.Configure(opt =>
			{
				opt.EnableAutoUpgradeOnLoad = options.EnableAutoUpgradeOnLoad;
				opt.CurrentSnapshotVersion = options.CurrentSnapshotVersion;
			})
			.ValidateOnStart();

		foreach (var upgrader in upgradingBuilder.Upgraders)
		{
			_ = builder.Services.AddSingleton(upgrader);
		}

		builder.Services.TryAddSingleton<SnapshotVersionManager>();

		return builder;
	}

	/// <summary>
	/// Registers event store erasure support for GDPR compliance.
	/// </summary>
	/// <typeparam name="TMapping">
	/// The <see cref="Erasure.IAggregateDataSubjectMapping"/> implementation that maps
	/// data subjects to their aggregates.
	/// </typeparam>
	/// <param name="builder"> The builder. </param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IEventSourcingBuilder UseEventStoreErasure<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TMapping>(this IEventSourcingBuilder builder)
		where TMapping : class, Erasure.IAggregateDataSubjectMapping
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<Erasure.IAggregateDataSubjectMapping, TMapping>();

		_ = builder.Services.AddSingleton<global::Excalibur.Compliance.IErasureContributor>(sp =>
		{
			var eventStore = sp.GetRequiredKeyedService<IEventStore>("default");
			var erasure = eventStore as IEventStoreErasure
						  ?? throw new InvalidOperationException(
							  $"The registered IEventStore ({eventStore.GetType().Name}) does not implement IEventStoreErasure. " +
							  $"GDPR event store erasure requires an event store that supports the IEventStoreErasure interface.");

			return new Erasure.EventStoreErasureContributor(
				erasure,
				sp.GetRequiredService<Erasure.IAggregateDataSubjectMapping>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Erasure.EventStoreErasureContributor>>(),
				sp.GetKeyedService<ISnapshotStore>("default"));
		});

		return builder;
	}

	/// <summary>
	/// Enables background processing for <see cref="ProjectionMode.Async"/> projections.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">
	/// Optional action to configure <see cref="GlobalStreamProjectionOptions"/> (polling interval, batch size, checkpoint interval).
	/// When omitted, defaults apply: 1 second idle polling, 500 batch size, 100 checkpoint interval.
	/// </param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This is the projection-side equivalent of CDC's <c>EnableBackgroundProcessing()</c>.
	/// It registers a hosted service that polls the global event stream via
	/// <see cref="Queries.IGlobalStreamQuery"/> and dispatches events to all projections
	/// registered with <c>.Async()</c> mode.
	/// </para>
	/// <para>
	/// Requires an <see cref="Queries.IGlobalStreamQuery"/> implementation to be registered
	/// by the event store provider (e.g., <c>UseSqlServer()</c>). If none is registered,
	/// the host logs a warning and exits gracefully.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =>
	/// {
	///     es.UseSqlServer(sql => sql.ConnectionString(connStr));
	///
	///     es.AddProjection&lt;OrderSummary&gt;(p => p
	///         .Async()
	///         .When&lt;OrderCreated&gt;((proj, e) => { proj.Total++; }));
	///
	///     // Start the background host that processes async projections
	///     es.EnableProjectionProcessing(opts =>
	///     {
	///         opts.IdlePollingInterval = TimeSpan.FromSeconds(2);
	///         opts.BatchSize = 200;
	///     });
	/// }));
	/// </code>
	/// </example>
	public static IEventSourcingBuilder EnableProjectionProcessing(
		this IEventSourcingBuilder builder,
		Action<GlobalStreamProjectionOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Configure options (consumer overrides or defaults)
		var optionsBuilder = builder.Services.AddOptions<GlobalStreamProjectionOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateOnStart();

		// Register in-memory checkpoint store as fallback (providers like SqlServer
		// can register a durable implementation that takes precedence via TryAdd).
		builder.Services.TryAddSingleton<ISubscriptionCheckpointStore, InMemorySubscriptionCheckpointStore>();

		// Register the background service
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AsyncProjectionProcessingHost>());

		return builder;
	}

	/// <summary>
	/// Replaces any existing <see cref="ISnapshotStrategy"/> registration with the given descriptor.
	/// This is necessary because <see cref="EventSourcingServiceCollectionExtensions.AddExcaliburEventSourcing(IServiceCollection)"/>
	/// registers <see cref="NoSnapshotStrategy"/> as the default via TryAddSingleton before the
	/// builder action runs. Without replace semantics, all Use*Snapshots methods would be no-ops.
	/// </summary>
	private static void ReplaceSnapshotStrategy(IServiceCollection services, ServiceDescriptor descriptor)
	{
		for (var i = services.Count - 1; i >= 0; i--)
		{
			if (services[i].ServiceType == typeof(ISnapshotStrategy))
			{
				services.RemoveAt(i);
			}
		}

		services.Add(descriptor);
	}
}
