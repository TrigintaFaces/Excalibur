// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Versioning;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Fluent builder interface for configuring Excalibur event sourcing services.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern,
/// consistent with <see cref="Outbox.IOutboxBuilder"/> and <see cref="Cdc.ICdcBuilder"/>.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcaliburEventSourcing(builder =>
/// {
///     builder.AddRepository&lt;OrderAggregate, Guid&gt;()
///            .UseIntervalSnapshots(100)
///            .UseEventStore&lt;SqlServerEventStore&gt;();
/// });
/// </code>
/// </example>
public interface IEventSourcingBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The <see cref="IServiceCollection"/>.</value>
	IServiceCollection Services { get; }

	/// <summary>
	/// Registers an event-sourced repository for an aggregate type with string keys.
	/// </summary>
	/// <typeparam name="TAggregate"> The aggregate type with string identifier. </typeparam>
	/// <param name="aggregateFactory"> Factory function to create aggregate instances from a string key. </param>
	/// <returns> The builder for fluent configuration. </returns>
	[RequiresUnreferencedCode("Repository registration may require types that cannot be statically analyzed.")]
	IEventSourcingBuilder AddRepository<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate>(
		Func<string, TAggregate> aggregateFactory)
		where TAggregate : class, Domain.Model.IAggregateRoot<string>, Domain.Model.IAggregateSnapshotSupport;

	/// <summary>
	/// Registers an event-sourced repository for an aggregate type with generic key type.
	/// </summary>
	/// <typeparam name="TAggregate"> The aggregate type. </typeparam>
	/// <typeparam name="TKey"> The key type for the aggregate. </typeparam>
	/// <param name="aggregateFactory"> Factory function to create aggregate instances from a key. </param>
	/// <returns> The builder for fluent configuration. </returns>
	[RequiresUnreferencedCode("Repository registration may require types that cannot be statically analyzed.")]
	IEventSourcingBuilder AddRepository<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate, TKey>(
		Func<TKey, TAggregate> aggregateFactory)
		where TAggregate : class, Domain.Model.IAggregateRoot<TKey>, Domain.Model.IAggregateSnapshotSupport
		where TKey : notnull;

	/// <summary>
	/// Registers an event-sourced repository for an aggregate type that implements
	/// <see cref="Domain.Model.IAggregateRoot{TAggregate, TKey}"/> with static factory methods.
	/// </summary>
	/// <typeparam name="TAggregate"> The aggregate type. </typeparam>
	/// <typeparam name="TKey"> The key type for the aggregate. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	[RequiresUnreferencedCode("Repository registration may require types that cannot be statically analyzed.")]
	IEventSourcingBuilder AddRepository<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate, TKey>()
		where TAggregate : class, Domain.Model.IAggregateRoot<TAggregate, TKey>, Domain.Model.IAggregateSnapshotSupport
		where TKey : notnull;

	/// <summary>
	/// Configures a custom snapshot strategy.
	/// </summary>
	/// <typeparam name="TStrategy"> The snapshot strategy implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder AddSnapshotStrategy<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TStrategy>()
		where TStrategy : class, ISnapshotStrategy;

	/// <summary>
	/// Configures an interval-based snapshot strategy.
	/// </summary>
	/// <param name="eventInterval"> The number of events between snapshots. Default: 100. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseIntervalSnapshots(int eventInterval = 100);

	/// <summary>
	/// Configures a time-based snapshot strategy.
	/// </summary>
	/// <param name="timeInterval"> The time interval between snapshots. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseTimeBasedSnapshots(TimeSpan timeInterval);

	/// <summary>
	/// Configures a size-based snapshot strategy.
	/// </summary>
	/// <param name="maxSizeInBytes"> The maximum size in bytes before creating a snapshot. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseSizeBasedSnapshots(long maxSizeInBytes);

	/// <summary>
	/// Configures a composite snapshot strategy combining multiple strategies.
	/// </summary>
	/// <param name="configure"> Action to configure the composite strategy. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseCompositeSnapshotStrategy(Action<CompositeSnapshotStrategyBuilder> configure);

	/// <summary>
	/// Configures a no-op snapshot strategy that never creates snapshots.
	/// </summary>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseNoSnapshots();

	/// <summary>
	/// Configures a custom snapshot manager.
	/// </summary>
	/// <typeparam name="TManager"> The snapshot manager implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseSnapshotManager<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TManager>()
		where TManager : class, ISnapshotManager;

	/// <summary>
	/// Configures a custom event store implementation.
	/// </summary>
	/// <typeparam name="TEventStore"> The event store implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseEventStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TEventStore>()
		where TEventStore : class, IEventStore;

	/// <summary>
	/// Configures a custom event serializer implementation.
	/// </summary>
	/// <typeparam name="TSerializer"> The serializer implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseEventSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TSerializer>()
		where TSerializer : class, Dispatch.Abstractions.IEventSerializer;

	/// <summary>
	/// Configures a custom outbox store for event sourcing.
	/// </summary>
	/// <typeparam name="TOutboxStore"> The outbox store implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseOutboxStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TOutboxStore>()
		where TOutboxStore : class, Outbox.IEventSourcedOutboxStore;

	/// <summary>
	/// Configures the upcasting pipeline for event versioning.
	/// </summary>
	/// <param name="configure"> Action to configure the upcasting builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder AddUpcastingPipeline(Action<UpcastingBuilder> configure);

	/// <summary>
	/// Configures snapshot upgrading for automatic snapshot version migration.
	/// </summary>
	/// <param name="configure"> Action to configure the snapshot upgrading builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder AddSnapshotUpgrading(Action<SnapshotUpgradingBuilder> configure);

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
	/// This method registers an <see cref="Dispatch.Compliance.IErasureContributor"/>
	/// that tombstones events and deletes snapshots when GDPR erasure is executed.
	/// The <see cref="IEventStore"/> implementation must also implement
	/// <see cref="IEventStoreErasure"/> for this to function.
	/// </para>
	/// <para>
	/// Requires <c>AddGdprErasure()</c> from <c>Excalibur.Dispatch.Compliance</c> to be registered.
	/// </para>
	/// </remarks>
	IEventSourcingBuilder UseEventStoreErasure<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TMapping>()
		where TMapping : class, Erasure.IAggregateDataSubjectMapping;
}
