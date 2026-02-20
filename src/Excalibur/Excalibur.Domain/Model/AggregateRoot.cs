// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

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
///     protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
///     {
///         OrderCreated e => Apply(e),
///         OrderShipped e => Apply(e),
///         _ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
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
///     protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
///     {
///         OrderCreated e => Apply(e),
///         OrderShipped e => Apply(e),
///         _ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
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
	public IReadOnlyList<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents;

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MarkEventsAsCommitted()
	{
		Version += _uncommittedEvents.Count;
		_uncommittedEvents.Clear();
	}

	/// <inheritdoc/>
	public void LoadFromHistory(IEnumerable<IDomainEvent> history)
	{
		ArgumentNullException.ThrowIfNull(history);

		foreach (var @event in history)
		{
			ApplyEventInternal(@event);
			Version++;
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
	[RequiresUnreferencedCode("Snapshot creation may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Snapshot creation may require dynamic code generation which is not compatible with AOT compilation.")]
	public virtual ISnapshot CreateSnapshot()
	{
		throw new NotSupportedException(
			$"Snapshot creation is not implemented for {GetType().Name}. " +
			"Override CreateSnapshot() to enable snapshotting.");
	}

	/// <inheritdoc/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ApplyEvent(IDomainEvent eventData)
	{
		ArgumentNullException.ThrowIfNull(eventData);
		ApplyEventInternal(eventData);
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

		ApplyEventInternal(@event);
		_uncommittedEvents.Add(@event);
	}

	/// <summary>
	/// Applies the event's changes to the aggregate's state using pattern matching.
	/// </summary>
	/// <param name="event">The domain event to apply.</param>
	/// <remarks>
	/// <para>
	/// Implement this method using a switch expression for optimal performance:
	/// </para>
	/// <code>
	/// protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	/// {
	///     OrderCreated e => Apply(e),
	///     OrderConfirmed e => Apply(e),
	///     _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
	/// };
	/// </code>
	/// </remarks>
	protected abstract void ApplyEventInternal(IDomainEvent @event);

	/// <summary>
	/// Applies a snapshot to restore aggregate state.
	/// </summary>
	/// <param name="snapshot">The snapshot to apply.</param>
	/// <remarks>
	/// Override this method to deserialize snapshot data and restore aggregate state.
	/// The default implementation does nothing.
	/// </remarks>
	protected virtual void ApplySnapshot(ISnapshot snapshot)
	{
		// Default implementation does nothing.
		// Override in derived class to apply snapshot state.
	}
}
