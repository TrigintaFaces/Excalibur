// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Provides table discovery and configuration-binding capabilities for the CDC builder.
/// </summary>
/// <remarks>
/// <para>
/// Separated from <see cref="ICdcBuilder"/> following the Interface Segregation Principle.
/// Consumers that only need explicit table tracking via <see cref="ICdcBuilder.TrackTable(string, Action{ICdcTableBuilder})"/>
/// do not need to depend on discovery features.
/// </para>
/// <para>
/// Implementations that support discovery should implement both <see cref="ICdcBuilder"/>
/// and <see cref="ICdcBuilderDiscovery"/>.
/// </para>
/// </remarks>
public interface ICdcBuilderDiscovery
{
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
	/// via <see cref="ICdcBuilder.TrackTable(string, Action{ICdcTableBuilder})"/>. Duplicate table
	/// names (case-insensitive) are skipped -- code-registered tables take precedence.
	/// </para>
	/// <para>
	/// Event mappings cannot be expressed in configuration and remain code-only via
	/// <see cref="ICdcBuilder.TrackTable(string, Action{ICdcTableBuilder})"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
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
	/// (via <see cref="ICdcBuilder.TrackTable(string, Action{ICdcTableBuilder})"/>) or
	/// configuration (via <see cref="BindTrackedTables"/>) take precedence --
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
