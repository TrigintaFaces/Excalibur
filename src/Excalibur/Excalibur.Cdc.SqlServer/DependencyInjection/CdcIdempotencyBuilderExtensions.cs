// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;

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
}
