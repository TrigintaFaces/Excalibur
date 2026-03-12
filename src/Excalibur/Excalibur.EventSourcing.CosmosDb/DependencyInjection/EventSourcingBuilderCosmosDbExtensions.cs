// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Extension methods for configuring Cosmos DB event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// CDC builder pattern (see <c>EventSourcingBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderCosmosDbExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Azure Cosmos DB for event storage.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for Cosmos DB event store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseCosmosDb(options =&gt;
	///     {
	///         options.ConnectionString = connectionString;
	///         options.DatabaseName = "events";
	///         options.ContainerName = "event-store";
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseCosmosDb(
		this IEventSourcingBuilder builder,
		Action<CosmosDbEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddCosmosDbEventStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the event sourcing builder to use Azure Cosmos DB using configuration binding.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configuration">The configuration section to bind.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configuration"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseCosmosDb(configuration.GetSection("CosmosDb"))
	///       .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseCosmosDb(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddCosmosDbEventStore(configuration);

		return builder;
	}
}
