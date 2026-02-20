// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Fluent builder interface for configuring Excalibur CDC processor.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// It provides the single entry point for CDC configuration.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddCdcProcessor(cdc =>
/// {
///     cdc.UseSqlServer(connectionString, sql =>
///     {
///         sql.SchemaName("cdc")
///            .StateTableName("CdcProcessingState")
///            .PollingInterval(TimeSpan.FromSeconds(5))
///            .BatchSize(100);
///     })
///     .TrackTable("dbo.Orders", table =>
///     {
///         table.MapInsert&lt;OrderCreatedEvent&gt;()
///              .MapUpdate&lt;OrderUpdatedEvent&gt;()
///              .MapDelete&lt;OrderDeletedEvent&gt;();
///     })
///     .TrackTable("dbo.Customers", table =>
///     {
///         table.MapInsert&lt;CustomerCreatedEvent&gt;()
///              .MapAll&lt;CustomerChangedEvent&gt;();
///     })
///     .WithRecovery(recovery =>
///     {
///         recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
///                 .MaxAttempts(3)
///                 .AttemptDelay(TimeSpan.FromSeconds(1));
///     })
///     .EnableBackgroundProcessing();
/// });
/// </code>
/// </example>
public interface ICdcBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>.</value>
	Microsoft.Extensions.DependencyInjection.IServiceCollection Services { get; }

	/// <summary>
	/// Configures tracking for a database table with event mappings.
	/// </summary>
	/// <param name="tableName">The fully qualified table name (e.g., "dbo.Orders").</param>
	/// <param name="configure">The table tracking configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="tableName"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure which database tables to track for changes
	/// and how to map those changes to domain events.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// cdc.TrackTable("dbo.Orders", table =>
	/// {
	///     table.MapInsert&lt;OrderCreatedEvent&gt;()
	///          .MapUpdate&lt;OrderUpdatedEvent&gt;()
	///          .MapDelete&lt;OrderDeletedEvent&gt;();
	/// });
	/// </code>
	/// </example>
	ICdcBuilder TrackTable(string tableName, Action<ICdcTableBuilder> configure);

	/// <summary>
	/// Configures tracking for a database table inferred from entity type.
	/// </summary>
	/// <typeparam name="TEntity">The entity type to infer table name from.</typeparam>
	/// <param name="configure">Optional table tracking configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// The table name is inferred from the entity type name using conventions.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// cdc.TrackTable&lt;Order&gt;(table =>
	/// {
	///     table.MapAll&lt;OrderChangedEvent&gt;();
	/// });
	/// </code>
	/// </example>
	ICdcBuilder TrackTable<TEntity>(Action<ICdcTableBuilder>? configure = null) where TEntity : class;

	/// <summary>
	/// Configures recovery options for stale position handling.
	/// </summary>
	/// <param name="configure">The recovery configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure how the CDC processor handles scenarios where
	/// the saved position (LSN) is no longer valid in the database.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// cdc.WithRecovery(recovery =>
	/// {
	///     recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
	///             .MaxAttempts(5)
	///             .AttemptDelay(TimeSpan.FromSeconds(30));
	/// });
	/// </code>
	/// </example>
	ICdcBuilder WithRecovery(Action<ICdcRecoveryBuilder> configure);

	/// <summary>
	/// Enables background processing of CDC changes.
	/// </summary>
	/// <param name="enable">Whether to enable background processing. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, a hosted service will be registered that periodically
	/// polls for CDC changes and processes them through the configured event handlers.
	/// </para>
	/// <para>
	/// For serverless scenarios where background services are not suitable,
	/// omit this call and use <c>ICdcProcessor</c> directly.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// cdc.UseSqlServer(connectionString)
	///    .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
	///    .EnableBackgroundProcessing();
	/// </code>
	/// </example>
	ICdcBuilder EnableBackgroundProcessing(bool enable = true);
}
