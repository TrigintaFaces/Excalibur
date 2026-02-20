// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.BoundedContext;

/// <summary>
/// Declares the bounded context that an aggregate root, entity, or value object belongs to.
/// </summary>
/// <remarks>
/// <para>
/// In Domain-Driven Design, a bounded context defines the boundary within which a particular
/// domain model is defined and applicable. This attribute enables static and runtime validation
/// that types do not cross bounded context boundaries inappropriately.
/// </para>
/// <para>
/// Usage:
/// <code>
/// [BoundedContext("Orders")]
/// public class OrderAggregate : AggregateRoot { }
///
/// [BoundedContext("Inventory")]
/// public class InventoryItem : AggregateRoot { }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(
	AttributeTargets.Class | AttributeTargets.Interface,
	Inherited = true,
	AllowMultiple = false)]
public sealed class BoundedContextAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BoundedContextAttribute"/> class.
	/// </summary>
	/// <param name="name">The name of the bounded context.</param>
	public BoundedContextAttribute(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		Name = name;
	}

	/// <summary>
	/// Gets the name of the bounded context.
	/// </summary>
	/// <value>The bounded context name.</value>
	public string Name { get; }
}
