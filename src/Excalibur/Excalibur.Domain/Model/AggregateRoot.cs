// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;
using Excalibur.Domain.Exceptions;

namespace Excalibur.Domain.Model;

/// <summary>
/// Base class for event-sourced aggregates with string keys.
/// </summary>
/// <remarks>
/// This is an alias for <see cref="AggregateRoot{TKey}"/> with <c>string</c> as the key type.
/// Use this for aggregates with string identifiers (the most common case).
/// </remarks>
/// <example>
/// <code>
/// public class OrderAggregate : AggregateRoot
/// {
///     protected override bool ApplyEventInternal(IDomainEvent @event) => @event switch
///     {
///         OrderCreated e => Apply(e),
///         OrderShipped e => Apply(e),
///         _ => false
///     };
/// }
/// </code>
/// </example>
public abstract class AggregateRoot : AggregateRoot<string>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateRoot"/> class.
	/// </summary>
	protected AggregateRoot()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateRoot"/> class with an identifier.
	/// </summary>
	/// <param name="id">The unique identifier of the aggregate.</param>
	protected AggregateRoot(string id) : base(id)
	{
	}
}

/// <summary>
/// Convenience base class for event-sourced aggregates that enables type inference
/// for <c>AddRepository&lt;TAggregate, TKey&gt;()</c> zero-argument registration.
/// </summary>
/// <typeparam name="TAggregate">The concrete aggregate type (CRTP self-type).</typeparam>
/// <typeparam name="TKey">The type of the aggregate identifier.</typeparam>
/// <remarks>
/// <para>
/// Deriving from this class automatically implements <see cref="IAggregateRoot{TAggregate, TKey}"/>,
/// requiring only the <c>static abstract</c> factory methods:
/// </para>
/// <code>
/// public class OrderAggregate : AggregateRoot&lt;OrderAggregate, Guid&gt;
/// {
///     public static OrderAggregate Create(Guid id) =&gt; new() { Id = id };
///
///     public static OrderAggregate FromEvents(Guid id, IEnumerable&lt;HistoricEvent&gt; events)
///     {
///         var aggregate = Create(id);
///         aggregate.LoadFromHistory(events);
///         return aggregate;
///     }
///
///     protected override bool ApplyEventInternal(IDomainEvent @event) =&gt; @event switch
///     {
///         OrderCreated e =&gt; Apply(e),
///         _ =&gt; false
///     };
/// }
/// </code>
/// <para>
/// This enables zero-argument repository registration:
/// <code>
/// builder.AddRepository&lt;OrderAggregate, Guid&gt;();
/// </code>
/// Without this base class, consumers must independently implement
/// <see cref="IAggregateRoot{TAggregate, TKey}"/> on their aggregate class.
/// </para>
/// </remarks>
public abstract class AggregateRoot<TAggregate, TKey> : AggregateRoot<TKey>, IAggregateRoot<TAggregate, TKey>
	where TAggregate : AggregateRoot<TAggregate, TKey>, new()
	where TKey : notnull
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateRoot{TAggregate, TKey}"/> class.
	/// </summary>
	protected AggregateRoot()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateRoot{TAggregate, TKey}"/> class with an identifier.
	/// </summary>
	/// <param name="id">The unique identifier of the aggregate.</param>
	protected AggregateRoot(TKey id) : base(id)
	{
	}

	/// <summary>
	/// Creates a new instance of the aggregate with the specified identifier.
	/// </summary>
	/// <param name="id">The unique identifier for the aggregate.</param>
	/// <returns>A new aggregate instance with its <see cref="AggregateRoot{TKey}.Id"/> set.</returns>
	/// <remarks>
	/// This default implementation uses the parameterless constructor and sets the Id.
	/// Override in the concrete aggregate if custom initialization is needed.
	/// </remarks>
	public static TAggregate Create(TKey id)
	{
		var aggregate = new TAggregate();
		aggregate.Id = id;
		return aggregate;
	}

	/// <summary>
	/// Rebuilds an aggregate from a stream of historical events.
	/// </summary>
	/// <param name="id">The unique identifier for the aggregate.</param>
	/// <param name="events">The stream of events to replay.</param>
	/// <returns>The aggregate rebuilt from the events.</returns>
	public static TAggregate FromEvents(TKey id, IEnumerable<HistoricEvent> events)
	{
		var aggregate = Create(id);
		aggregate.LoadFromHistory(events);
		return aggregate;
	}
}

/// <summary>
/// Base class for event-sourced aggregates with strongly-typed keys.
/// </summary>
/// <typeparam name="TKey">The type of the aggregate identifier.</typeparam>
/// <remarks>
/// <para>
/// This base class provides:
/// <list type="bullet">
/// <item>Pattern-matching event application via <see cref="ApplyEventInternal"/> (no reflection)</item>
/// <item>Uncommitted events collection for persistence</item>
/// <item>Version tracking for optimistic concurrency</item>
/// <item>ETag-based optimistic concurrency support</item>
/// <item>Snapshot support for performance optimization</item>
/// <item>History replay via <see cref="LoadFromHistory"/></item>
/// </list>
/// </para>
/// <para>
/// Derived classes should implement <see cref="ApplyEventInternal"/> using a switch expression
/// for optimal (&lt;10ns) event application without reflection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderAggregate : AggregateRoot&lt;Guid&gt;
/// {
///     public OrderStatus Status { get; private set; }
///
///     protected override bool ApplyEventInternal(IDomainEvent @event) => @event switch
///     {
///         OrderCreated e => Apply(e),
///         OrderShipped e => Apply(e),
///         _ => false
///     };
///
///     private bool Apply(OrderCreated e)
///     {
///         Id = e.OrderId;
///         Status = OrderStatus.Created;
///         return true;
///     }
/// }
/// </code>
/// </example>
public abstract class AggregateRoot<TKey> : IAggregateRoot<TKey>, IAggregateSnapshotSupport
	where TKey : notnull
{
	private readonly List<IDomainEvent> _uncommittedEvents = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateRoot{TKey}"/> class.
	/// </summary>
	protected AggregateRoot()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateRoot{TKey}"/> class with an identifier.
	/// </summary>
	/// <param name="id">The unique identifier of the aggregate.</param>
	protected AggregateRoot(TKey id)
	{
		Id = id;
	}

	/// <inheritdoc/>
	public TKey Id { get; protected set; } = default!;

	/// <summary>
	/// Gets the string representation of the aggregate identifier.
	/// </summary>
	/// <remarks>
	/// This explicit interface implementation ensures compatibility with <see cref="IAggregateRoot"/>
	/// while allowing derived classes to use strongly-typed <see cref="Id"/>.
	/// </remarks>
	string IAggregateRoot.Id => Id?.ToString() ?? string.Empty;

	/// <inheritdoc/>
	public long Version { get; protected set; }

	/// <inheritdoc/>
	public virtual string AggregateType => GetType().Name;

	/// <inheritdoc/>
	public string? ETag { get; set; }

	/// <summary>
	/// Gets a value indicating whether the aggregate has uncommitted events.
	/// </summary>
	/// <value><see langword="true"/> if there are uncommitted events; otherwise, <see langword="false"/>.</value>
	public bool HasUncommittedEvents => _uncommittedEvents.Count > 0;

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public IReadOnlyList<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MarkEventsAsCommitted()
	{
		Version += _uncommittedEvents.Count;
		_uncommittedEvents.Clear();
	}

	/// <inheritdoc/>
	public void LoadFromHistory(IEnumerable<HistoricEvent> history)
	{
		ArgumentNullException.ThrowIfNull(history);

		foreach (var historic in history)
		{
			// Contiguity: replayed events MUST be gap-free and in order. The store's version is the durable
			// 0-based stream index (first event = 0), while Version is the count of applied events (also the
			// next expected index). The authoritative version travels in the envelope, never in the payload:
			// a domain event cannot be trusted to report the position it was written at, and 36 of the types
			// that implement IDomainEvent have no way to be told it. Inferring the version by counting (the
			// removed `Version++`) would silently apply an event where a missing one belongs and rehydrate a
			// corrupt aggregate without any error.
			var expectedVersion = Version;
			if (historic.Version != expectedVersion)
			{
				throw new EventStreamContiguityException(GetType(), expectedVersion, historic.Version);
			}

			// Totality: an event with no matching ApplyEventInternal arm returns false and is refused here,
			// rather than being silently ignored while the version still advances.
			if (!ApplyEventInternal(historic.Event))
			{
				throw new UnhandledDomainEventException(GetType(), historic.Event.GetType());
			}

			// Advance the count to one past the applied 0-based index.
			Version = historic.Version + 1;
		}
	}

	/// <inheritdoc/>
	public virtual void LoadFromSnapshot(ISnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		ApplySnapshot(snapshot);
		Version = snapshot.Version;
	}

	/// <inheritdoc/>
	public virtual ISnapshot CreateSnapshot()
	{
		throw new NotSupportedException(
			$"Snapshot creation is not implemented for {GetType().Name}. " +
			"How to fix: Override CreateSnapshot() in your aggregate class to return an ISnapshot representing current state. " +
			"Also override ApplySnapshot() to rehydrate from the snapshot.");
	}

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ApplyEvent(IDomainEvent eventData)
	{
		ArgumentNullException.ThrowIfNull(eventData);
		if (!ApplyEventInternal(eventData))
		{
			throw new UnhandledDomainEventException(GetType(), eventData.GetType());
		}
	}

	/// <inheritdoc/>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType.IsAssignableFrom(GetType()) ? this : null;
	}

	/// <summary>
	/// Raises a new event, applies it to update state, and adds it to uncommitted events.
	/// </summary>
	/// <param name="event">The event to raise.</param>
	/// <remarks>
	/// <para>
	/// This method:
	/// <list type="number">
	/// <item>Applies the event via <see cref="ApplyEventInternal"/> to update aggregate state</item>
	/// <item>Adds the event to the uncommitted events collection</item>
	/// </list>
	/// </para>
	/// <para>
	/// Note: Version is NOT incremented here. Version is incremented in
	/// <see cref="MarkEventsAsCommitted"/> when events are persisted.
	/// </para>
	/// </remarks>
	protected void RaiseEvent(IDomainEvent @event)
	{
		ArgumentNullException.ThrowIfNull(@event);

		if (!ApplyEventInternal(@event))
		{
			throw new UnhandledDomainEventException(GetType(), @event.GetType());
		}

		_uncommittedEvents.Add(@event);
	}

	/// <summary>
	/// Applies the event's changes to the aggregate's state using pattern matching.
	/// </summary>
	/// <param name="event">The domain event to apply.</param>
	/// <returns>
	/// <see langword="true"/> if the aggregate recognized and applied the event type; otherwise
	/// <see langword="false"/>. Returning <see langword="false"/> (a missing switch arm) causes the caller
	/// to throw <see cref="UnhandledDomainEventException"/> — an unrecognized event is refused, never
	/// silently ignored.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Implement this method using a switch expression for optimal performance. The default arm returns
	/// <see langword="false"/> so an unhandled event fails loud:
	/// </para>
	/// <code>
	/// protected override bool ApplyEventInternal(IDomainEvent @event) => @event switch
	/// {
	///     OrderCreated e => Apply(e),
	///     OrderConfirmed e => Apply(e),
	///     _ => false
	/// };
	/// </code>
	/// <para>Each <c>Apply</c> overload returns <see langword="true"/>.</para>
	/// </remarks>
	protected abstract bool ApplyEventInternal(IDomainEvent @event);

	/// <summary>
	/// Applies a snapshot to restore aggregate state.
	/// </summary>
	/// <param name="snapshot">The snapshot to apply.</param>
	/// <remarks>
	/// <para>
	/// Override this method to deserialize snapshot data and restore aggregate state.
	/// It must be overridden in lock-step with <see cref="CreateSnapshot"/>: an aggregate
	/// that produces snapshots must also know how to consume them.
	/// </para>
	/// <para>
	/// The base implementation is fail-closed. It throws <see cref="NotSupportedException"/>
	/// rather than silently no-op'ing, because reaching it means a snapshot was produced
	/// (only an overridden <see cref="CreateSnapshot"/> can persist one) but
	/// <see cref="ApplySnapshot"/> was not overridden to consume it. Silently returning
	/// would load empty state and then stamp <see cref="Version"/> from the snapshot in
	/// <see cref="LoadFromSnapshot"/> &#8212; silent state corruption. Aggregates that do not
	/// support snapshots never reach this method, because their <see cref="CreateSnapshot"/>
	/// also throws and so no snapshot is ever created to load.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">
	/// Always thrown by the base implementation, indicating <see cref="ApplySnapshot"/> was not
	/// overridden to match an overridden <see cref="CreateSnapshot"/>.
	/// </exception>
	protected virtual void ApplySnapshot(ISnapshot snapshot)
	{
		// Fail-closed and symmetric with the base CreateSnapshot, which also throws.
		// Reaching here means CreateSnapshot was overridden (a snapshot exists to load) but
		// ApplySnapshot was not. Throwing prevents silently loading empty state and then
		// stamping Version from the snapshot in LoadFromSnapshot (silent data corruption).
		throw new NotSupportedException(
			$"Snapshot application is not implemented for {GetType().Name}, but a snapshot was supplied. " +
			"This aggregate overrides CreateSnapshot() (which produced the snapshot) but does not override ApplySnapshot(). " +
			"How to fix: Override ApplySnapshot(ISnapshot) in your aggregate class to rehydrate state from the snapshot, " +
			"matching the state captured by your overridden CreateSnapshot().");
	}
}
