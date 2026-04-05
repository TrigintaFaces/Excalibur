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
public class ExcaliburEventSourcingBuilder : IEventSourcingBuilder
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
				sp.GetRequiredKeyedService<IEventStore>("default"),
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
				sp.GetRequiredKeyedService<IEventStore>("default"),
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

	/// <inheritdoc/>
	public IEventSourcingBuilder UseEventStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TEventStore>()
		where TEventStore : class, IEventStore
	{
		_ = Services.AddKeyedSingleton<IEventStore, TEventStore>("default");
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
