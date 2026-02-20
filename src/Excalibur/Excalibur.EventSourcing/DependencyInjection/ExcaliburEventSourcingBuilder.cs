// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Implementation;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// Use Excalibur.EventSourcing.Abstractions as canonical source (AD-251-2)
using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using ISnapshotManager = Excalibur.EventSourcing.Abstractions.ISnapshotManager;
using ISnapshotStrategy = Excalibur.EventSourcing.Abstractions.ISnapshotStrategy;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Fluent builder for configuring Excalibur event sourcing services.
/// </summary>
/// <remarks>
/// <para> This builder provides a fluent API for configuring event sourcing infrastructure:
/// <list type="bullet">
/// <item> Repository registration with automatic or explicit factory methods </item>
/// <item> Snapshot strategy configuration (interval, time-based, size-based, composite) </item>
/// <item> Event store and serializer configuration </item>
/// <item> Outbox store integration </item>
/// </list>
/// </para>
/// <para> <b> Usage: </b>
/// <code>
///services.AddExcaliburEventSourcing(builder =&gt; builder
///.AddRepository&lt;OrderAggregate, Guid&gt;()
///.UseIntervalSnapshots(100)
///.UseSnapshotStrategy&lt;CustomStrategy&gt;());
/// </code>
/// </para>
/// </remarks>
public sealed class ExcaliburEventSourcingBuilder : IEventSourcingBuilder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExcaliburEventSourcingBuilder" /> class.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	public ExcaliburEventSourcingBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	public IServiceCollection Services { get; }

	/// <summary>
	/// Registers an event-sourced repository for an aggregate type with string keys.
	/// </summary>
	/// <typeparam name="TAggregate"> The aggregate type with string identifier. </typeparam>
	/// <param name="aggregateFactory"> Factory function to create aggregate instances from a string key. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This method registers an <see cref="IEventSourcedRepository{TAggregate}" /> that automatically upcasts events during aggregate
	/// replay when <see cref="UpcastingOptions.EnableAutoUpcastOnReplay" /> is enabled and <see cref="IUpcastingPipeline" /> is registered.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Repository registration may require types that cannot be statically analyzed.")]
	public IEventSourcingBuilder AddRepository<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate>(
		Func<string, TAggregate> aggregateFactory)
		where TAggregate : class, Domain.Model.IAggregateRoot<string>, Domain.Model.IAggregateSnapshotSupport
	{
		ArgumentNullException.ThrowIfNull(aggregateFactory);

		Services.TryAddSingleton<IEventSourcedRepository<TAggregate>>(sp =>
			new EventSourcedRepository<TAggregate>(
				sp.GetRequiredService<IEventStore>(),
				sp.GetRequiredService<IEventSerializer>(),
				aggregateFactory,
				sp.GetService<IUpcastingPipeline>(),
				sp.GetService<ISnapshotManager>(),
				sp.GetService<ISnapshotStrategy>(),
				sp.GetService<IOptions<UpcastingOptions>>(),
				sp.GetService<IEventSourcedOutboxStore>(),
				sp.GetService<SnapshotVersionManager>(),
				sp.GetService<IOptions<SnapshotUpgradingOptions>>()));

		return this;
	}

	/// <summary>
	/// Registers an event-sourced repository for an aggregate type with generic key type.
	/// </summary>
	/// <typeparam name="TAggregate"> The aggregate type. </typeparam>
	/// <typeparam name="TKey"> The key type for the aggregate. </typeparam>
	/// <param name="aggregateFactory"> Factory function to create aggregate instances from a key. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks> Use this method for aggregates with non-string keys (e.g., Guid, int). </remarks>
	[RequiresUnreferencedCode("Repository registration may require types that cannot be statically analyzed.")]
	public IEventSourcingBuilder AddRepository<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate, TKey>(
		Func<TKey, TAggregate> aggregateFactory)
		where TAggregate : class, Domain.Model.IAggregateRoot<TKey>, Domain.Model.IAggregateSnapshotSupport
		where TKey : notnull
	{
		ArgumentNullException.ThrowIfNull(aggregateFactory);

		Services.TryAddSingleton<IEventSourcedRepository<TAggregate, TKey>>(sp =>
			new EventSourcedRepository<TAggregate, TKey>(
				sp.GetRequiredService<IEventStore>(),
				sp.GetRequiredService<IEventSerializer>(),
				aggregateFactory,
				sp.GetService<IUpcastingPipeline>(),
				sp.GetService<ISnapshotManager>(),
				sp.GetService<ISnapshotStrategy>(),
				sp.GetService<IOptions<UpcastingOptions>>(),
				sp.GetService<IEventSourcedOutboxStore>(),
				sp.GetService<SnapshotVersionManager>(),
				sp.GetService<IOptions<SnapshotUpgradingOptions>>()));

		return this;
	}

	/// <summary>
	/// Registers an event-sourced repository for an aggregate type that implements <see cref="IAggregateRoot{TAggregate, TKey}" /> with static
	/// factory methods.
	/// </summary>
	/// <typeparam name="TAggregate"> The aggregate type implementing <see cref="IAggregateRoot{TAggregate, TKey}" />. </typeparam>
	/// <typeparam name="TKey"> The key type for the aggregate. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This overload automatically uses the <c> static Create(TKey) </c> method from <see cref="IAggregateRoot{TAggregate, TKey}" /> to create
	/// aggregate instances. No factory function is required.
	/// </para>
	/// <para> <b> Usage: </b>
	/// <code>
	///services.AddExcaliburEventSourcing(builder =&gt; builder
	///.AddRepository&lt;OrderAggregate, Guid&gt;());  // Uses OrderAggregate.Create(id)
	/// </code>
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Repository registration may require types that cannot be statically analyzed.")]
	public IEventSourcingBuilder AddRepository<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate, TKey>()
		where TAggregate : class, Domain.Model.IAggregateRoot<TAggregate, TKey>, Domain.Model.IAggregateSnapshotSupport
		where TKey : notnull
	{
		return AddRepository<TAggregate, TKey>(TAggregate.Create);
	}

	/// <summary>
	/// Configures a custom snapshot strategy.
	/// </summary>
	/// <typeparam name="TStrategy"> The snapshot strategy implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder AddSnapshotStrategy<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TStrategy>()
		where TStrategy : class, ISnapshotStrategy
	{
		Services.TryAddSingleton<ISnapshotStrategy, TStrategy>();
		return this;
	}

	/// <summary>
	/// Configures an interval-based snapshot strategy.
	/// </summary>
	/// <param name="eventInterval"> The number of events between snapshots. Default: 100. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseIntervalSnapshots(int eventInterval = 100)
	{
		Services.TryAddSingleton<ISnapshotStrategy>(new IntervalSnapshotStrategy(eventInterval));
		return this;
	}

	/// <summary>
	/// Configures a time-based snapshot strategy.
	/// </summary>
	/// <param name="timeInterval"> The time interval between snapshots. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseTimeBasedSnapshots(TimeSpan timeInterval)
	{
		Services.TryAddSingleton<ISnapshotStrategy>(new TimeBasedSnapshotStrategy(timeInterval));
		return this;
	}

	/// <summary>
	/// Configures a size-based snapshot strategy.
	/// </summary>
	/// <param name="maxSizeInBytes"> The maximum size in bytes before creating a snapshot. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseSizeBasedSnapshots(long maxSizeInBytes)
	{
		Services.TryAddSingleton<ISnapshotStrategy>(new SizeBasedSnapshotStrategy(maxSizeInBytes));
		return this;
	}

	/// <summary>
	/// Configures a composite snapshot strategy combining multiple strategies.
	/// </summary>
	/// <param name="configure"> Action to configure the composite strategy. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseCompositeSnapshotStrategy(Action<CompositeSnapshotStrategyBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var strategyBuilder = new CompositeSnapshotStrategyBuilder();
		configure(strategyBuilder);
		Services.TryAddSingleton(strategyBuilder.Build());
		return this;
	}

	/// <summary>
	/// Configures a no-op snapshot strategy that never creates snapshots.
	/// </summary>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseNoSnapshots()
	{
		Services.TryAddSingleton<ISnapshotStrategy>(NoSnapshotStrategy.Instance);
		return this;
	}

	/// <summary>
	/// Configures a custom snapshot manager.
	/// </summary>
	/// <typeparam name="TManager"> The snapshot manager implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseSnapshotManager<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TManager>()
		where TManager : class, ISnapshotManager
	{
		Services.TryAddSingleton<ISnapshotManager, TManager>();
		return this;
	}

	/// <summary>
	/// Configures a custom event store implementation.
	/// </summary>
	/// <typeparam name="TEventStore"> The event store implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseEventStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TEventStore>()
		where TEventStore : class, IEventStore
	{
		_ = Services.AddSingleton<IEventStore, TEventStore>();
		return this;
	}

	/// <summary>
	/// Configures a custom event serializer implementation.
	/// </summary>
	/// <typeparam name="TSerializer"> The serializer implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseEventSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TSerializer>()
		where TSerializer : class, IEventSerializer
	{
		Services.TryAddSingleton<IEventSerializer, TSerializer>();
		return this;
	}

	/// <summary>
	/// Configures a custom outbox store for event sourcing.
	/// </summary>
	/// <typeparam name="TOutboxStore"> The outbox store implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder UseOutboxStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TOutboxStore>()
		where TOutboxStore : class, IEventSourcedOutboxStore
	{
		Services.TryAddSingleton<IEventSourcedOutboxStore, TOutboxStore>();
		return this;
	}

	/// <summary>
	/// Configures the upcasting pipeline for event versioning.
	/// </summary>
	/// <param name="configure"> Action to configure the upcasting builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public IEventSourcingBuilder AddUpcastingPipeline(Action<UpcastingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_ = Services.AddMessageUpcasting(configure);
		return this;
	}

	/// <summary>
	/// Registers event store erasure support for GDPR compliance.
	/// </summary>
	/// <typeparam name="TMapping">
	/// The <see cref="Erasure.IAggregateDataSubjectMapping"/> implementation that maps
	/// data subjects to their aggregates.
	/// </typeparam>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Registers an <see cref="Dispatch.Compliance.IErasureContributor"/> that delegates
	/// to <see cref="IEventStoreErasure"/> for event tombstoning and optionally
	/// <see cref="ISnapshotStore"/> for snapshot deletion.
	/// </para>
	/// <para>
	/// The <see cref="IEventStore"/> registered in DI must also implement
	/// <see cref="IEventStoreErasure"/>. If it does not, the contributor will fail at
	/// runtime when erasure is attempted.
	/// </para>
	/// </remarks>
	public IEventSourcingBuilder UseEventStoreErasure<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TMapping>()
		where TMapping : class, Erasure.IAggregateDataSubjectMapping
	{
		Services.TryAddSingleton<Erasure.IAggregateDataSubjectMapping, TMapping>();

		// Register the erasure contributor that bridges GDPR erasure to event sourcing.
		// The contributor resolves IEventStoreErasure from the IEventStore at runtime.
		_ = Services.AddSingleton<Dispatch.Compliance.IErasureContributor>(sp =>
		{
			var eventStore = sp.GetRequiredService<IEventStore>();
			var erasure = eventStore as IEventStoreErasure
						  ?? throw new InvalidOperationException(
							  $"The registered IEventStore ({eventStore.GetType().Name}) does not implement IEventStoreErasure. " +
							  $"GDPR event store erasure requires an event store that supports the IEventStoreErasure interface.");

			return new Erasure.EventStoreErasureContributor(
				erasure,
				sp.GetRequiredService<Erasure.IAggregateDataSubjectMapping>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Erasure.EventStoreErasureContributor>>(),
				sp.GetService<ISnapshotStore>());
		});

		return this;
	}

	/// <summary>
	/// Configures snapshot upgrading for automatic snapshot version migration.
	/// </summary>
	/// <param name="configure"> Action to configure the snapshot upgrading builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// Registers the <see cref="SnapshotVersionManager"/> and all configured
	/// <see cref="ISnapshotUpgrader"/> implementations. When enabled, snapshot data
	/// is automatically upgraded during aggregate hydration from snapshots.
	/// </para>
	/// </remarks>
	public IEventSourcingBuilder AddSnapshotUpgrading(Action<SnapshotUpgradingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SnapshotUpgradingOptions();
		var builder = new SnapshotUpgradingBuilder(options);
		configure(builder);

		_ = Services.AddOptions<SnapshotUpgradingOptions>()
			.Configure(opt =>
			{
				opt.EnableAutoUpgradeOnLoad = options.EnableAutoUpgradeOnLoad;
				opt.CurrentSnapshotVersion = options.CurrentSnapshotVersion;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		foreach (var upgrader in builder.Upgraders)
		{
			_ = Services.AddSingleton(upgrader);
		}

		Services.TryAddSingleton<SnapshotVersionManager>();

		return this;
	}
}

/// <summary>
/// Builder for creating composite snapshot strategies.
/// </summary>
public sealed class CompositeSnapshotStrategyBuilder
{
	private readonly List<ISnapshotStrategy> _strategies = [];
	private bool _requireAll;

	/// <summary>
	/// Adds an interval-based snapshot strategy.
	/// </summary>
	/// <param name="eventInterval"> The number of events between snapshots. Default: 100. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public CompositeSnapshotStrategyBuilder AddIntervalStrategy(int eventInterval = 100)
	{
		_strategies.Add(new IntervalSnapshotStrategy(eventInterval));
		return this;
	}

	/// <summary>
	/// Adds a time-based snapshot strategy.
	/// </summary>
	/// <param name="timeInterval"> The time interval between snapshots. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public CompositeSnapshotStrategyBuilder AddTimeBasedStrategy(TimeSpan timeInterval)
	{
		_strategies.Add(new TimeBasedSnapshotStrategy(timeInterval));
		return this;
	}

	/// <summary>
	/// Adds a size-based snapshot strategy.
	/// </summary>
	/// <param name="maxSizeInBytes"> The maximum size in bytes before creating a snapshot. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public CompositeSnapshotStrategyBuilder AddSizeBasedStrategy(long maxSizeInBytes)
	{
		_strategies.Add(new SizeBasedSnapshotStrategy(maxSizeInBytes));
		return this;
	}

	/// <summary>
	/// Sets whether all strategies must agree to create a snapshot.
	/// </summary>
	/// <param name="requireAll"> True if all strategies must agree; otherwise, any strategy triggers snapshot. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public CompositeSnapshotStrategyBuilder RequireAll(bool requireAll = true)
	{
		_requireAll = requireAll;
		return this;
	}

	/// <summary>
	/// Builds the composite snapshot strategy.
	/// </summary>
	/// <returns> The configured composite snapshot strategy. </returns>
	public ISnapshotStrategy Build()
	{
		var mode = _requireAll
			? CompositeSnapshotStrategy.CompositeMode.All
			: CompositeSnapshotStrategy.CompositeMode.Any;
		return new CompositeSnapshotStrategy(mode, [.. _strategies]);
	}
}
