// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Domain.Model;

/// <summary>
/// Represents an aggregate root in Domain-Driven Design that uses event sourcing.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the core event sourcing operations. Snapshot and history replay
/// capabilities are available via <see cref="IAggregateSnapshotSupport"/>, which aggregates
/// may also implement. Use <see cref="GetService"/> to access optional capabilities.
/// </para>
/// </remarks>
public interface IAggregateRoot
{
	/// <summary>
	/// Gets the unique identifier of the aggregate.
	/// </summary>
	/// <value>The unique identifier of the aggregate.</value>
	string Id { get; }

	/// <summary>
	/// Gets the current version of the aggregate.
	/// </summary>
	/// <value>The current version of the aggregate.</value>
	long Version { get; }

	/// <summary>
	/// Gets the list of uncommitted events.
	/// </summary>
	/// <returns>The list of uncommitted events.</returns>
	IReadOnlyList<IDomainEvent> GetUncommittedEvents();

	/// <summary>
	/// Marks all uncommitted events as committed.
	/// </summary>
	void MarkEventsAsCommitted();

	/// <summary>
	/// Applies a historical event to rebuild state.
	/// </summary>
	/// <param name="eventData"> The event to apply. </param>
	void ApplyEvent(IDomainEvent eventData);

	/// <summary>
	/// Gets a service of the specified type from this aggregate.
	/// </summary>
	/// <param name="serviceType"> The type of service to retrieve (e.g. <see cref="IAggregateSnapshotSupport"/>). </param>
	/// <returns> The service instance, or <see langword="null"/> if the service is not supported. </returns>
	object? GetService(Type serviceType);
}

/// <summary>
/// Generic interface for aggregates with strongly-typed keys.
/// </summary>
/// <typeparam name="TKey">The type of the aggregate identifier.</typeparam>
/// <remarks>
/// Use this interface when your aggregate requires a non-string key type (e.g., <see cref="Guid"/>, <see cref="int"/>).
/// The base <see cref="IAggregateRoot.Id"/> property returns the string representation of the key.
/// </remarks>
/// <example>
/// <code>
/// public class OrderAggregate : AggregateRoot&lt;Guid&gt;
/// {
///     // Id property is of type Guid
/// }
/// </code>
/// </example>
public interface IAggregateRoot<TKey> : IAggregateRoot
	where TKey : notnull
{
	/// <summary>
	/// Gets the strongly-typed unique identifier of the aggregate.
	/// </summary>
	/// <value>The unique identifier of the aggregate.</value>
	new TKey Id { get; }
}

/// <summary>
/// Generic interface for strongly-typed aggregates with factory methods.
/// </summary>
/// <typeparam name="TAggregate">The type of the aggregate.</typeparam>
/// <typeparam name="TKey">The type of the aggregate identifier.</typeparam>
/// <remarks>
/// This interface provides static factory methods for creating aggregates,
/// enabling polymorphic aggregate construction in generic repository implementations.
/// </remarks>
public interface IAggregateRoot<TAggregate, TKey> : IAggregateRoot<TKey>
	where TAggregate : IAggregateRoot<TAggregate, TKey>
	where TKey : notnull
{
	/// <summary>
	/// Creates a new instance of the aggregate.
	/// </summary>
	/// <param name="id">The unique identifier for the aggregate.</param>
	/// <returns>A new instance of the aggregate.</returns>
	static abstract TAggregate Create(TKey id);

	/// <summary>
	/// Rebuilds the aggregate from a stream of events.
	/// </summary>
	/// <param name="id">The unique identifier for the aggregate.</param>
	/// <param name="events">The stream of events to apply.</param>
	/// <returns>The aggregate rebuilt from the events.</returns>
	static abstract TAggregate FromEvents(TKey id, IEnumerable<IDomainEvent> events);
}
