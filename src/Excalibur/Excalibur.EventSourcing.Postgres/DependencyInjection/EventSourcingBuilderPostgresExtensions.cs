// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Extension methods for configuring Postgres event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// CDC builder pattern (see <c>CdcBuilderInMemoryExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Postgres for event store,
	/// snapshot store, and outbox store.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="registerHealthChecks">Whether to register health checks. Default: true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="connectionString"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UsePostgres(connectionString)
	///       .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UsePostgres(
		this IEventSourcingBuilder builder,
		string connectionString,
		bool registerHealthChecks = true)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionString);

		_ = builder.Services.AddPostgresEventSourcing(connectionString, registerHealthChecks);

		return builder;
	}

	/// <summary>
	/// Configures the event sourcing builder to use Postgres with detailed options.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for Postgres event sourcing options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UsePostgres(options =&gt;
	///     {
	///         options.ConnectionString = connectionString;
	///         options.HealthChecks.RegisterHealthChecks = true;
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UsePostgres(
		this IEventSourcingBuilder builder,
		Action<PostgresEventSourcingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddPostgresEventSourcing(configure);

		return builder;
	}
}
