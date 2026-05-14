// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

		builder.Services.TryAddSingleton<ICdcIdempotencyFilter, InMemoryCdcIdempotencyFilter>();
		return builder;
	}

	/// <summary>
	/// Registers the SQL Server-backed CDC idempotency filter, which persists processed event
	/// records in a database table for durable, multi-instance deduplication.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Suitable for multi-instance deployments where multiple CDC consumers may process
	/// the same events on crash/restart. The filter uses the CDC-native
	/// <c>(tableName, LSN, seqVal)</c> composite key, stored in a SQL Server table.
	/// </para>
	/// <para>
	/// This replaces any previously registered <see cref="ICdcIdempotencyFilter"/>
	/// (including the in-memory filter). Uses <c>AddSingleton</c> semantics.
	/// </para>
	/// <para>
	/// Registers <see cref="SqlServerCdcIdempotencyFilterOptions"/> with
	/// <see cref="IValidateOptions{TOptions}"/> validation and <c>ValidateOnStart()</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =>
	/// {
	///     cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
	///        .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
	///        .UseSqlServerIdempotencyFilter()
	///        .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseSqlServerIdempotencyFilter(this ICdcBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return UseSqlServerIdempotencyFilter(builder, _ => { });
	}

	/// <summary>
	/// Registers the SQL Server-backed CDC idempotency filter with custom options.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">A delegate to configure the idempotency filter options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// See <see cref="UseSqlServerIdempotencyFilter(ICdcBuilder)"/> for full details.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =>
	/// {
	///     cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
	///        .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
	///        .UseSqlServerIdempotencyFilter(opts =>
	///        {
	///            opts.SchemaName = "MySchema";
	///            opts.RetentionPeriod = TimeSpan.FromHours(48);
	///            opts.CleanupBatchSize = 5000;
	///        })
	///        .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseSqlServerIdempotencyFilter(
		this ICdcBuilder builder,
		Action<SqlServerCdcIdempotencyFilterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.AddSingleton<IValidateOptions<SqlServerCdcIdempotencyFilterOptions>,
			SqlServerCdcIdempotencyFilterOptionsValidator>();
		builder.Services.AddOptionsWithValidateOnStart<SqlServerCdcIdempotencyFilterOptions>();
		builder.Services.AddSingleton<ICdcIdempotencyFilter, SqlServerCdcIdempotencyFilter>();

		return builder;
	}
}
