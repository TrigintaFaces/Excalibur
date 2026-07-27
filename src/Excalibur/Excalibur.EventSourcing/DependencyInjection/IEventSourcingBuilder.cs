// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Fluent builder interface for configuring Excalibur event sourcing services.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern,
/// consistent with <c>IOutboxBuilder</c> and <c>ICdcBuilder</c>.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcalibur(x => x.AddEventSourcing(builder =>
/// {
///     builder.AddRepository&lt;OrderAggregate, Guid&gt;()
///            .UseIntervalSnapshots(100)
///            .UseEventStore&lt;SqlServerEventStore&gt;();
/// }));
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
	/// <param name="configureOptions"> Optional per-aggregate repository configuration (e.g., outbox staging strategy). </param>
	/// <returns> The builder for fluent configuration. </returns>
	[RequiresUnreferencedCode("Repository registration may require types that cannot be statically analyzed.")]
	IEventSourcingBuilder AddRepository<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate>(
		Func<string, TAggregate> aggregateFactory,
		Action<Implementation.EventSourcedRepositoryOptions>? configureOptions = null)
		where TAggregate : class, Domain.Model.IAggregateRoot<string>, Domain.Model.IAggregateSnapshotSupport;

	/// <summary>
	/// Registers an event-sourced repository for an aggregate type with generic key type.
	/// </summary>
	/// <typeparam name="TAggregate"> The aggregate type. </typeparam>
	/// <typeparam name="TKey"> The key type for the aggregate. </typeparam>
	/// <param name="aggregateFactory"> Factory function to create aggregate instances from a key. </param>
	/// <param name="configureOptions"> Optional per-aggregate repository configuration. </param>
	/// <returns> The builder for fluent configuration. </returns>
	[RequiresUnreferencedCode("Repository registration may require types that cannot be statically analyzed.")]
	IEventSourcingBuilder AddRepository<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TAggregate, TKey>(
		Func<TKey, TAggregate> aggregateFactory,
		Action<Implementation.EventSourcedRepositoryOptions>? configureOptions = null)
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
	/// Configures a custom event store implementation.
	/// </summary>
	/// <typeparam name="TEventStore"> The event store implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseEventStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TEventStore>()
		where TEventStore : class, IEventStore;

	/// <summary>
	/// Registers <typeparamref name="TEvent"/> in the secure event-type allow-list consulted by the
	/// default serializer, at the event-sourcing wiring call site.
	/// </summary>
	/// <typeparam name="TEvent"> The event type to register. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// The default serializer rejects unregistered event types for security; registering them here gives it
	/// a secure and functional resolution path (mirrors <c>JsonSerializerContext</c> opt-in). Equivalent to
	/// <c>services.AddEventTypes&lt;TEvent&gt;()</c>, discoverable at the builder.
	/// </remarks>
	IEventSourcingBuilder RegisterEventTypes<TEvent>();

	/// <summary>
	/// Registers the specified event types in the secure event-type allow-list.
	/// </summary>
	/// <param name="eventTypes"> The event types to register. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder RegisterEventTypes(params Type[] eventTypes);

	/// <summary>
	/// Registers every <see cref="Excalibur.Dispatch.IDomainEvent"/> type in <paramref name="assembly"/>
	/// in the secure event-type allow-list.
	/// </summary>
	/// <param name="assembly"> The assembly to scan (typically <c>typeof(Program).Assembly</c>). </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// A compile-time-known, consumer-controlled DI-time scan — categorically different from the runtime
	/// reflection scan the serializer rejects by default. The security guarantee is unchanged; this removes
	/// the risk of hand-listing and missing an event type.
	/// </remarks>
	[RequiresUnreferencedCode("Scans the assembly for IDomainEvent types via reflection, which is not trim-safe. Use RegisterEventTypes<TEvent>() or RegisterEventTypes(params Type[]) for a trim/AOT-safe path.")]
	IEventSourcingBuilder RegisterEventTypesFromAssembly(System.Reflection.Assembly assembly);
}
