// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model;

/// <summary>
/// Base class for aggregates that have both a technical identity and a business key.
/// </summary>
/// <typeparam name="TKey">The type of the technical identifier (for persistence).</typeparam>
/// <typeparam name="TBusinessKey">The type of the business key (domain-meaningful).</typeparam>
/// <remarks>
/// <para>
/// Use this base class when:
/// <list type="bullet">
/// <item>The business key may change (e.g., employee badge number after transfer)</item>
/// <item>The business key has format constraints from external systems</item>
/// <item>The business key is scoped, not globally unique</item>
/// <item>Users reference entities by the business key (e.g., order numbers)</item>
/// <item>External systems assign the key</item>
/// </list>
/// </para>
/// <para>
/// Use <see cref="AggregateRoot{TKey}"/> instead when:
/// <list type="bullet">
/// <item>The domain key IS the technical identity (immutable, globally unique)</item>
/// <item>No meaningful business key exists</item>
/// <item>Simplicity is preferred</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderAggregate : KeyedAggregateRoot&lt;Guid, string&gt;
/// {
///     private string _orderNumber = string.Empty;
///
///     public override string BusinessKey => _orderNumber;
///
///     protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
///     {
///         OrderCreated e => Apply(e),
///         _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
///     };
///
///     private bool Apply(OrderCreated e)
///     {
///         Id = e.OrderId;
///         _orderNumber = e.OrderNumber;
///         return true;
///     }
/// }
/// </code>
/// </example>
public abstract class KeyedAggregateRoot<TKey, TBusinessKey> : AggregateRoot<TKey>
	where TKey : notnull
	where TBusinessKey : notnull
{
	/// <summary>
	/// Initializes a new instance of the <see cref="KeyedAggregateRoot{TKey, TBusinessKey}"/> class.
	/// </summary>
	protected KeyedAggregateRoot()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyedAggregateRoot{TKey, TBusinessKey}"/> class with an identifier.
	/// </summary>
	/// <param name="id">The unique technical identifier of the aggregate.</param>
	protected KeyedAggregateRoot(TKey id) : base(id)
	{
	}

	/// <summary>
	/// Gets the domain-meaningful business key for this aggregate.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The business key is distinct from <see cref="AggregateRoot{TKey}.Id"/>:
	/// <list type="bullet">
	/// <item><b>Id</b>: Technical identity for persistence and event streams (immutable)</item>
	/// <item><b>BusinessKey</b>: Domain-meaningful identifier for users and external systems (may change)</item>
	/// </list>
	/// </para>
	/// <para>
	/// Examples: Order number, account number, badge number, SKU.
	/// </para>
	/// </remarks>
	/// <value>The business key that identifies this aggregate in the domain.</value>
	public abstract TBusinessKey BusinessKey { get; }
}
