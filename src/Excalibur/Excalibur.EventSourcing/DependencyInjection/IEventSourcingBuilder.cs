// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

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
	/// Configures a custom event store implementation.
	/// </summary>
	/// <typeparam name="TEventStore"> The event store implementation type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	IEventSourcingBuilder UseEventStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TEventStore>()
		where TEventStore : class, IEventStore;
}
