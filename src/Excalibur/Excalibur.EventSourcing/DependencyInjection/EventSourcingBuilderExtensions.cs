// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using ISnapshotManager = Excalibur.EventSourcing.Abstractions.ISnapshotManager;
using ISnapshotStrategy = Excalibur.EventSourcing.Abstractions.ISnapshotStrategy;

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
		builder.Services.TryAddSingleton<ISnapshotStrategy, TStrategy>();
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
		builder.Services.TryAddSingleton<ISnapshotStrategy>(new IntervalSnapshotStrategy(eventInterval));
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
		builder.Services.TryAddSingleton<ISnapshotStrategy>(new TimeBasedSnapshotStrategy(timeInterval));
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
		builder.Services.TryAddSingleton<ISnapshotStrategy>(new SizeBasedSnapshotStrategy(maxSizeInBytes));
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
		builder.Services.TryAddSingleton(strategyBuilder.Build());
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
		builder.Services.TryAddSingleton<ISnapshotStrategy>(NoSnapshotStrategy.Instance);
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
	/// Configures a custom outbox store for event sourcing.
	/// </summary>
	/// <typeparam name="TOutboxStore"> The outbox store implementation type. </typeparam>
	/// <param name="builder"> The builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IEventSourcingBuilder UseOutboxStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TOutboxStore>(this IEventSourcingBuilder builder)
		where TOutboxStore : class, IEventSourcedOutboxStore
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.Services.TryAddSingleton<IEventSourcedOutboxStore, TOutboxStore>();
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

		_ = builder.Services.AddSingleton<Dispatch.Compliance.IErasureContributor>(sp =>
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
}
