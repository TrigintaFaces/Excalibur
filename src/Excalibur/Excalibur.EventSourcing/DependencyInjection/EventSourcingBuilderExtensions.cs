// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Snapshots;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SnapshotUpgradingOptions>, SnapshotUpgradingOptionsValidator>());

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

		// Declare that event-store erasure honors the ambient tenant: the erasure contributor scopes the
		// tombstone to the data subject's tenant (BeginScope) and the erase/is-erased requests fail closed
		// on a null discriminator. Paired with the IAggregateDataSubjectMapping opt-in the gate keys on, so
		// a multi-tenant host that opts into erasure passes the tenant-scoping requirement instead of
		// failing closed at startup.
		//
		// Emitted here through the canonical internal seam (EventSourcing is an InternalsVisibleTo friend of
		// Excalibur.Dispatch.Abstractions), co-located with the erasure wiring it attests, so the sole
		// ITenantScopingCapability implementation is the one internal marker — no provider clone.
		Microsoft.Extensions.DependencyInjection.TenantScopedStoreServiceCollectionExtensions
			.AddTenantScopingCapability<IEventStoreErasure>(builder.Services);

		// TryAddEnumerable, not AddSingleton: this is a multi-implementation service, so a plain Add
		// registers a SECOND copy when a composed host calls this method twice -- the same aggregate is
		// then tombstoned twice and the certificate counts it twice. The sibling registrations in this
		// method are already idempotent; this makes the contributors match them. The generic
		// implementation-type argument is what lets TryAddEnumerable de-duplicate a factory registration.
		builder.Services.TryAddEnumerable(ServiceDescriptor
			.Singleton<global::Excalibur.Compliance.IErasureContributor, Erasure.EventStoreErasureContributor>(sp =>
		{
			var eventStore = sp.GetRequiredKeyedService<IEventStore>("default");
			// Ask the store, do not test its type: the resolved store is the decorated one, and a decorator
			// answers for the capabilities of the store it wraps.
			var erasure = eventStore.GetService(typeof(IEventStoreErasure)) as IEventStoreErasure
						  ?? throw new InvalidOperationException(
							  $"The registered IEventStore ({eventStore.GetType().Name}) does not implement IEventStoreErasure. " +
							  $"GDPR event store erasure requires an event store that supports the IEventStoreErasure interface.");

			return new Erasure.EventStoreErasureContributor(
				erasure,
				sp.GetRequiredService<Erasure.IAggregateDataSubjectMapping>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Erasure.EventStoreErasureContributor>>(),
				sp.GetKeyedService<ISnapshotStore>("default"),
				// Lets the contributor reach the read models: it enumerates the projection registry and
				// invokes each registration's pre-bound clear delegate after the tombstone. Passed as the
				// provider rather than as the registry so a host with no projections registers nothing extra
				// and the delegate can resolve the projection store and recovery service it closes over.
				sp,
				// The aggregate types this deployment is legally obliged to keep through an erasure. Absent
				// when nothing is declared, which is the behaviour every host had before retentions existed:
				// every mapped aggregate is tombstoned.
				sp.GetService<global::Excalibur.Compliance.IErasureRetentionRegistry>());
		}));

		// Fail-closed for the surface the framework CANNOT erase. Erasure tombstones event rows in place
		// and notifies nothing, so a projection that already folded the subject's events keeps them --
		// and the coverage gate could not see that, because coverage is judged over locations the
		// consumer's own inventory discovered and nothing declares a projection store as one. The
		// certificate therefore read Completed with every read model untouched. This contributor reports
		// the gap on every erasure so the outcome is PARTIAL by construction rather than by the
		// consumer's diligence; it stands down when no projections are registered, and when a consumer
		// registers their own contributor covering projections.
		builder.Services.TryAddEnumerable(ServiceDescriptor
			.Singleton<global::Excalibur.Compliance.IErasureContributor, Erasure.ProjectionErasureGapContributor>(
				sp => new Erasure.ProjectionErasureGapContributor(sp)));

		// Fail-closed startup gate: GDPR event-store erasure composed with tenant-sharding is not yet
		// supported — the tenant-routing store does not route erasure to per-tenant shards, so an erase would
		// not reach the subject's shard. Rather than let the erasure contributor resolve into an unroutable
		// state, fail fast at startup when both are composed. The sharding marker is registered by the routing
		// wiring (both EnableTenantSharding and AddMultiTenancy's sharding strategy), so this covers every
		// sharding entry point regardless of registration order; a non-sharding host has no marker and passes.
		builder.Services.AddOptions<ShardingErasureGuardOptions>().ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ShardingErasureGuardOptions>, ShardingErasureGuard>());

		// Fail-closed startup gate: the throw in the contributor factory above runs on resolution, which for
		// a scoped erasure service is the first right-to-erasure request — the consumer would learn their
		// composition cannot erase with a statutory clock already running. This applies the same probe at
		// boot. The factory throw stays as the floor for a host-less composition (serverless wiring), where
		// ValidateOnStart never runs; two layers, not a replacement. Registered here so a host that never
		// opts into erasure is untouched.
		builder.Services.AddOptions<TieredErasureGuardOptions>().ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TieredErasureGuardOptions>, TieredErasureGuard>());

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

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<GlobalStreamProjectionOptions>, GlobalStreamProjectionOptionsValidator>());

		// Register in-memory checkpoint store as fallback (providers like SqlServer
		// can register a durable implementation that takes precedence via TryAdd).
		builder.Services.TryAddSingleton<ISubscriptionCheckpointStore, InMemorySubscriptionCheckpointStore>();

		// Projection-lag read model: pairs each subscription checkpoint with the global-stream head.
		// Requires an IGlobalStreamQuery (registered by a provider such as SqlServer); consumers that
		// resolve it without a provider configured should probe via GetService and fail open.
		builder.Services.TryAddSingleton<IProjectionLagReadModel>(static sp =>
			new ProjectionLagReadModel(
				sp.GetRequiredService<ISubscriptionCheckpointStore>(),
				sp.GetService<IGlobalStreamQuery>()));

		// Register the background service
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AsyncProjectionProcessingHost>());

		// A processing host with no stream to poll returns immediately, so the application would start,
		// report healthy, and process nothing forever. Turn that silence into a startup failure — the same
		// precondition, and the same argument, as AddProjectionRebuild().
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ProjectionProcessingPrerequisiteValidator>());
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, ProjectionProcessingPrerequisiteValidator>());

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

	/// <summary>
	/// Requires every registered projection to be written through a store that records how far the
	/// projection has been folded, so a re-delivered event cannot be applied twice.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>What this buys.</b> A projection is applied by loading it, mutating it and writing it back,
	/// and without a recorded position nothing can tell that an event is being applied a second time. A
	/// handler that assigns a value survives that; one that accumulates — incrementing a total,
	/// appending to a list — double-counts on every redelivery, silently and without bound. The
	/// framework cannot tell the two apart, which is why this is a choice the host makes rather than a
	/// default the framework guesses.
	/// </para>
	/// <para>
	/// <b>It fails at startup rather than degrading.</b> A store without the capability is refused by
	/// name, with the projections that cannot be served. Falling back to the unconditional write would
	/// hand back exactly the defect this prevents, reported as success.
	/// </para>
	/// <para>
	/// Requires the last-applied-position migration for your provider, shipped alongside its other
	/// schema scripts.
	/// </para>
	/// </remarks>
	/// <param name="builder">The event sourcing builder.</param>
	/// <returns>The builder, for chaining.</returns>
	public static IEventSourcingBuilder RequirePositionedProjectionWrites(this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, PositionedProjectionWriteValidator>());
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, PositionedProjectionWriteValidator>());

		return builder;
	}
}
