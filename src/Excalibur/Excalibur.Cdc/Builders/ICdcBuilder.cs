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
///     cdc.UseSqlServer(sql =>
///     {
///         sql.ConnectionString(connectionString)
///            .SchemaName("cdc")
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
	/// <para>
	/// <strong>Table naming conventions:</strong> Use singular, schema-qualified names
	/// (e.g., <c>"dbo.Order"</c> not <c>"Orders"</c>). SQL Server CDC capture instances
	/// are derived from table names, so consistent naming avoids ambiguity when querying
	/// <c>cdc.fn_cdc_get_all_changes_*</c> functions. If your database uses plural table
	/// names, pass the exact name as it appears in the database.
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
	/// The table name is inferred from the entity type name using the singular form
	/// (e.g., <c>TrackTable&lt;Order&gt;()</c> maps to <c>{DefaultSchema}.Order</c>).
	/// No naive pluralization is applied. For custom table names (e.g., plural tables
	/// or non-default schemas), use the string overload:
	/// <c>TrackTable("sales.OrderItems", configure)</c>.
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
	/// cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
	///    .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
	///    .EnableBackgroundProcessing();
	/// </code>
	/// </example>
	ICdcBuilder EnableBackgroundProcessing(bool enable = true);

	/// <summary>
	/// Binds tracked tables from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="configSectionPath">
	/// The configuration section path containing an array of table tracking entries
	/// (e.g., <c>"Cdc:Tables"</c>).
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="configSectionPath"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Tables bound from configuration are merged additively with tables registered
	/// via <see cref="TrackTable(string, Action{ICdcTableBuilder})"/>. Duplicate table
	/// names (case-insensitive) are skipped — code-registered tables take precedence.
	/// </para>
	/// <para>
	/// Event mappings cannot be expressed in configuration and remain code-only via
	/// <see cref="TrackTable(string, Action{ICdcTableBuilder})"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // appsettings.json:
	/// // {
	/// //   "Cdc": {
	/// //     "Tables": [
	/// //       { "TableName": "dbo.Orders", "CaptureInstance": "dbo_Orders_v2" },
	/// //       { "TableName": "dbo.Customers" }
	/// //     ]
	/// //   }
	/// // }
	///
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(sql =&gt; sql.ConnectionString(connectionString))
	///        .BindTrackedTables("Cdc:Tables")
	///        .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	ICdcBuilder BindTrackedTables(string configSectionPath);

	/// <summary>
	/// Enables automatic discovery of tracked tables from registered
	/// <see cref="ICdcTableProvider"/> implementations in DI.
	/// </summary>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// At startup, all <see cref="ICdcTableProvider"/> services are resolved
	/// from DI, and each provider's <see cref="ICdcTableProvider.TableNames"/>
	/// are registered as tracked tables. Tables already registered by code
	/// (via <see cref="TrackTable(string, Action{ICdcTableBuilder})"/>) or
	/// configuration (via <see cref="BindTrackedTables"/>) take precedence —
	/// duplicates by table name (case-insensitive) are skipped.
	/// </para>
	/// <para>
	/// This bridges provider-specific handler interfaces (e.g., SQL Server's
	/// <c>IDataChangeHandler</c>) with the cross-provider CDC tracking system.
	/// Provider-specific handlers that implement <see cref="ICdcTableProvider"/>
	/// are automatically discovered.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(sql =&gt; sql.ConnectionString(connectionString))
	///        .TrackTablesFromHandlers()
	///        .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	ICdcBuilder TrackTablesFromHandlers();
}
