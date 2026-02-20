// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Fluent builder interface for configuring CDC table tracking settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures how changes to a specific database table are tracked
/// and mapped to domain events. It supports mapping different operations (INSERT,
/// UPDATE, DELETE) to different event types.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// cdc.TrackTable("dbo.Orders", table =>
/// {
///     table.MapInsert&lt;OrderCreatedEvent&gt;()
///          .MapUpdate&lt;OrderUpdatedEvent&gt;()
///          .MapDelete&lt;OrderDeletedEvent&gt;()
///          .WithFilter(change => change.NewValue != null)
///          .CaptureInstance("dbo_Orders_CT");
/// });
/// </code>
/// </example>
public interface ICdcTableBuilder
{
	/// <summary>
	/// Maps INSERT operations to an event type.
	/// </summary>
	/// <typeparam name="TEvent">The event type to create for INSERT operations.</typeparam>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When a new row is inserted into the tracked table, an instance of
	/// <typeparamref name="TEvent"/> will be created with the new row data.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// table.MapInsert&lt;OrderCreatedEvent&gt;();
	/// </code>
	/// </example>
	ICdcTableBuilder MapInsert<TEvent>() where TEvent : class;

	/// <summary>
	/// Maps UPDATE operations to an event type.
	/// </summary>
	/// <typeparam name="TEvent">The event type to create for UPDATE operations.</typeparam>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When an existing row is updated in the tracked table, an instance of
	/// <typeparamref name="TEvent"/> will be created with both old and new row data.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// table.MapUpdate&lt;OrderUpdatedEvent&gt;();
	/// </code>
	/// </example>
	ICdcTableBuilder MapUpdate<TEvent>() where TEvent : class;

	/// <summary>
	/// Maps DELETE operations to an event type.
	/// </summary>
	/// <typeparam name="TEvent">The event type to create for DELETE operations.</typeparam>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When a row is deleted from the tracked table, an instance of
	/// <typeparamref name="TEvent"/> will be created with the deleted row data.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// table.MapDelete&lt;OrderDeletedEvent&gt;();
	/// </code>
	/// </example>
	ICdcTableBuilder MapDelete<TEvent>() where TEvent : class;

	/// <summary>
	/// Maps ALL operations (INSERT, UPDATE, DELETE) to a single event type.
	/// </summary>
	/// <typeparam name="TEvent">The event type to create for all operations.</typeparam>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is a convenience method that registers the same event type for all
	/// change operations. Use this when you want a unified event that captures
	/// any change to the table.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// table.MapAll&lt;OrderChangedEvent&gt;();
	/// </code>
	/// </example>
	ICdcTableBuilder MapAll<TEvent>() where TEvent : class;

	/// <summary>
	/// Configures a filter predicate for change events.
	/// </summary>
	/// <param name="predicate">The predicate to filter changes.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="predicate"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Only changes that match the filter predicate will be processed.
	/// This allows filtering changes at the CDC level before events are created.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// table.WithFilter(change => change.ColumnName != "UpdatedAt");
	/// </code>
	/// </example>
	ICdcTableBuilder WithFilter(Func<CdcDataChange, bool> predicate);

	/// <summary>
	/// Sets the CDC capture instance name (SQL Server specific).
	/// </summary>
	/// <param name="name">The capture instance name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// By default, SQL Server CDC uses the capture instance name format
	/// <c>schema_table</c>. Use this method to override the default name.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// table.CaptureInstance("dbo_Orders_Audit");
	/// </code>
	/// </example>
	ICdcTableBuilder CaptureInstance(string name);
}
