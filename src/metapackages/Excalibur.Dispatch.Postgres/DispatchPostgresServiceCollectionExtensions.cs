// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.EventSourcing.Postgres;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension that bundles Excalibur.Dispatch with PostgreSQL event sourcing into a single
/// registration call.
/// </summary>
public static class DispatchPostgresServiceCollectionExtensions
{
	/// <summary>
	/// Registers Excalibur.Dispatch with PostgreSQL event sourcing using the specified connection string.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This call registers no outbox. The PostgreSQL outbox store is constructed from an
	/// <c>Excalibur.Data.IDb</c>, which is supplied by the application rather than by the framework, and a
	/// connection string alone is not enough to build one. A host that wants a PostgreSQL outbox therefore
	/// registers its own <c>IDb</c> and declares the outbox against it:
	/// </para>
	/// <code>
	/// services.AddScoped&lt;IDb, MyPostgresDb&gt;();
	/// services.AddDispatchWithPostgres(connectionString);
	/// services.AddExcalibur(x =&gt; x.AddOutbox(o =&gt;
	///     o.UsePostgres(pg =&gt; pg.ConnectionFactory(sp =&gt; sp.GetRequiredService&lt;IDb&gt;()))));
	/// </code>
	/// <para>
	/// The SQL Server counterpart, <c>AddDispatchWithSqlServer</c>, does register an outbox: that store
	/// builds from a connection factory and needs nothing from the application.
	/// </para>
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The PostgreSQL connection string.</param>
	/// <param name="configureDispatch">Optional dispatch builder configuration. Supplying it takes over handler registration: when it is omitted, handlers are discovered by scanning the entry assembly; when it is supplied, only the handlers it names are registered.</param>
	/// <returns>The service collection for chaining.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddDispatchWithPostgres(
		this IServiceCollection services,
		string connectionString,
		Action<IDispatchBuilder>? configureDispatch = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = services.AddDispatch(configureDispatch ?? (static d => d.AddHandlersFromEntryAssembly()));

		// Routed through the IExcaliburBuilder bridge so the flat AddExcaliburEventSourcing aggregator
		// stays internal.
		_ = services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
			es.UsePostgres(pg => pg.ConnectionString(connectionString))));

		return services;
	}
}
