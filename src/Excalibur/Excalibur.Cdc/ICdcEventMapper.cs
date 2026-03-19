// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Maps CDC column data to a typed event instance.
/// </summary>
/// <typeparam name="TEvent">The target event type.</typeparam>
/// <remarks>
/// <para>
/// Implementations should be source-generated or hand-written for AOT safety.
/// Do not use reflection-based property mapping in implementations.
/// </para>
/// <para>
/// Use with <see cref="ICdcTableBuilder"/> methods like
/// <c>MapInsert&lt;TEvent, TMapper&gt;()</c> to register a mapper for a specific change type.
/// The mapper is resolved from DI and invoked at runtime when CDC changes are detected.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// internal sealed class OrderCreatedEventMapper : ICdcEventMapper&lt;OrderCreatedEvent&gt;
/// {
///     public OrderCreatedEvent Map(IReadOnlyList&lt;CdcDataChange&gt; changes, CdcChangeType changeType)
///     {
///         return new OrderCreatedEvent
///         {
///             OrderId = changes.GetValue&lt;int&gt;("OrderId"),
///             CustomerId = changes.GetValue&lt;string&gt;("CustomerId"),
///             Total = changes.GetValue&lt;decimal&gt;("Total")
///         };
///     }
/// }
/// </code>
/// </example>
public interface ICdcEventMapper<out TEvent>
{
	/// <summary>
	/// Creates a typed event from CDC column changes.
	/// </summary>
	/// <param name="changes">The column-level changes from the CDC capture.</param>
	/// <param name="changeType">The type of change (Insert, Update, Delete).</param>
	/// <returns>A typed event instance populated from the change data.</returns>
	TEvent Map(IReadOnlyList<CdcDataChange> changes, CdcChangeType changeType);
}
