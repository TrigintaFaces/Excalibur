// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring CDC idempotency filtering on <see cref="ICdcBuilder"/>.
/// </summary>
public static class CdcIdempotencyBuilderExtensions
{
	/// <summary>
	/// Registers the in-memory CDC idempotency filter, which deduplicates events
	/// using a bounded in-memory cache (10,000 entries, skip-when-full).
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Suitable for single-instance deployments. The filter does not survive process
	/// restarts — it is purely in-memory.
	/// </para>
	/// <para>
	/// When registered, the CDC processor checks each event's <c>(tableName, LSN, seqVal)</c>
	/// before invoking the handler. Already-processed events are skipped.
	/// </para>
	/// <para>
	/// Uses <c>TryAddSingleton</c> semantics — if a filter is already registered,
	/// this call is a no-op.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =>
	/// {
	///     cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
	///        .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
	///        .UseInMemoryIdempotencyFilter()
	///        .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseInMemoryIdempotencyFilter(this ICdcBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Delegates to the IServiceCollection overload so the job path and the builder path share ONE
		// registration site and cannot drift apart.
		_ = builder.Services.AddInMemoryCdcIdempotencyFilter();
		return builder;
	}

	/// <summary>
	/// Registers the SQL Server-backed CDC idempotency filter against a connection string.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="connectionString">The connection string for the database holding the dedupe table.</param>
	/// <param name="configure">Optionally configures schema, table, retention and cleanup batch size.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// <b>The connection source is required, and that is a correction.</b> This overload previously took no
	/// connection at all: the filter injected an <see cref="System.Data.IDbConnection"/> that nothing in
	/// this framework registers, so the registration succeeded and resolution failed at the first change
	/// processed. Naming the database here makes the requirement visible at the call that creates it.
	/// </para>
	/// <para>
	/// The dedupe table is OUR bookkeeping, not the source database's, so it normally belongs beside the
	/// CDC state store rather than in the database being captured.
	/// </para>
	/// <para>
	/// Requires <c>Scripts/002_CreateCdcIdempotencySchema.sql</c> to have been run against that database;
	/// the table is never created at runtime.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =>
	/// {
	///     cdc.UseSqlServer(sql => sql.ConnectionString(sourceConnectionString))
	///        .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
	///        .UseSqlServerIdempotencyFilter(stateConnectionString)
	///        .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseSqlServerIdempotencyFilter(
		this ICdcBuilder builder,
		string connectionString,
		Action<SqlServerCdcIdempotencyFilterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddSqlServerCdcIdempotencyFilter(connectionString, configure);

		return builder;
	}

	/// <summary>
	/// Registers the SQL Server-backed CDC idempotency filter against a connection factory.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="connectionFactory">
	/// Resolves, from the provider, a delegate creating one connection per operation.
	/// </param>
	/// <param name="configure">Optionally configures schema, table, retention and cleanup batch size.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Use this rather than the connection-string overload when a connection needs per-call construction
	/// the string cannot express, such as acquiring a managed-identity access token. Both delegate to the
	/// same <see cref="IServiceCollection"/> registration, which is the single registration site shared
	/// with the config-driven job path.
	/// </remarks>
	public static ICdcBuilder UseSqlServerIdempotencyFilter(
		this ICdcBuilder builder,
		Func<IServiceProvider, Func<System.Data.IDbConnection>> connectionFactory,
		Action<SqlServerCdcIdempotencyFilterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		_ = builder.Services.AddSqlServerCdcIdempotencyFilter(connectionFactory, configure);

		return builder;
	}
}
