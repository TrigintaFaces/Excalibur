// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Microsoft.Extensions.DependencyInjection, matching every sibling DI extension in this package, so the
// methods are discoverable from a host that has only the standard DI using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers a CDC idempotency filter on an <see cref="IServiceCollection"/>, for hosts that configure CDC
/// through the config-driven job path as well as those using the fluent builder.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The change-event processor resolves its filter optionally, so a host that
/// registers none gets CDC with no deduplication and nothing reports it. Until these overloads existed the
/// only public way to supply one was an extension on <c>ICdcBuilder</c>, which the config-driven job path
/// never constructs, and both the filter interface and its implementations are <see langword="internal"/>
/// so a host could not register them by hand either.
/// </para>
/// <para>
/// <b>Both CDC configuration paths are first-class</b>, which is why the gap mattered rather than being a
/// reason to migrate: <c>CdcTableConfig</c> is the shared shape for both, and
/// <c>CdcCaptureInstanceDeriver</c> is documented as the single source of truth shared by both so they
/// produce identical runtime behavior. A capability reachable from one and not the other was an asymmetry
/// in the registration surface, not in the design.
/// </para>
/// <para>
/// <b>These are the single registration site.</b> The <c>ICdcBuilder</c> overloads delegate here rather
/// than repeating the registrations, so the two paths cannot drift apart.
/// </para>
/// <para>
/// <b>The durable filter requires a table that is never created at runtime.</b> Run
/// <c>Scripts/002_CreateCdcIdempotencySchema.sql</c> against the target database before the first change
/// is processed; without it every duplicate check fails with <i>Invalid object name</i> and CDC processing
/// stops. The in-memory filter needs no schema and deduplicates only within one process lifetime.
/// </para>
/// </remarks>
public static class CdcIdempotencyServiceCollectionExtensions
{
	/// <summary>
	/// Registers the in-process CDC idempotency filter, which deduplicates within a single process
	/// lifetime and requires no database schema.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The same collection, for chaining.</returns>
	/// <remarks>
	/// Uses <c>TryAdd</c> semantics, so an already-registered filter wins. Suitable for a single-instance
	/// deployment or a development host. It does NOT survive a restart and does NOT coordinate between
	/// instances, so two instances processing the same change both consider it new.
	/// </remarks>
	public static IServiceCollection AddInMemoryCdcIdempotencyFilter(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ICdcIdempotencyFilter, InMemoryCdcIdempotencyFilter>();
		return services;
	}

	/// <summary>
	/// Registers the SQL Server-backed CDC idempotency filter against a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The connection string for the database holding the dedupe table.</param>
	/// <param name="configure">Optionally configures schema, table, retention and cleanup batch size.</param>
	/// <returns>The same collection, for chaining.</returns>
	/// <remarks>
	/// The convenience overload: a connection is created per operation from this string. Use the
	/// connection-factory overload when a connection needs per-call construction the string cannot
	/// express, such as acquiring a managed-identity access token.
	/// </remarks>
	public static IServiceCollection AddSqlServerCdcIdempotencyFilter(
		this IServiceCollection services,
		string connectionString,
		Action<SqlServerCdcIdempotencyFilterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return AddSqlServerCdcIdempotencyFilter(
			services,
			_ => () => new SqlConnection(connectionString),
			configure);
	}

	/// <summary>
	/// Registers the SQL Server-backed CDC idempotency filter against a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">
	/// Resolves, from the provider, a delegate that creates one connection per operation. The filter owns
	/// and disposes each connection it creates.
	/// </param>
	/// <param name="configure">Optionally configures schema, table, retention and cleanup batch size.</param>
	/// <returns>The same collection, for chaining.</returns>
	/// <remarks>
	/// <para>
	/// <b>The connection source is a parameter rather than an ambient dependency, deliberately.</b> The
	/// filter previously injected an <see cref="IDbConnection"/> that nothing in this framework registered,
	/// so registration succeeded and resolution failed — the error arrived at the first change processed
	/// rather than at the call that was wrong. Taking it here means a host cannot register the filter
	/// without saying where its table lives.
	/// </para>
	/// <para>
	/// Uses <c>AddSingleton</c> rather than <c>TryAdd</c>, so this deliberately replaces any filter already
	/// registered — including the in-memory one. A durable choice should win over a process-local default.
	/// </para>
	/// <para>
	/// If you override the schema or table name, rename the objects in
	/// <c>Scripts/002_CreateCdcIdempotencySchema.sql</c> to match before running it.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerCdcIdempotencyFilter(
		this IServiceCollection services,
		Func<IServiceProvider, Func<IDbConnection>> connectionFactory,
		Action<SqlServerCdcIdempotencyFilterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		services.TryAddEnumerable(ServiceDescriptor.Singleton<
			IValidateOptions<SqlServerCdcIdempotencyFilterOptions>,
			SqlServerCdcIdempotencyFilterOptionsValidator>());
		_ = services.AddOptionsWithValidateOnStart<SqlServerCdcIdempotencyFilterOptions>();

		_ = services.AddSingleton<ICdcIdempotencyFilter>(sp => new SqlServerCdcIdempotencyFilter(
			connectionFactory(sp),
			sp.GetRequiredService<IOptions<SqlServerCdcIdempotencyFilterOptions>>(),
			sp.GetRequiredService<ILogger<SqlServerCdcIdempotencyFilter>>()));

		return services;
	}
}
