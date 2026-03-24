// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

namespace Excalibur.EventSourcing.MongoDB;

/// <summary>
/// Extension methods for configuring MongoDB event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// builder pattern (see <c>EventSourcingBuilderCosmosDbExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderMongoDbExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use MongoDB for event storage.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for MongoDB event store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseMongoDB(options =&gt;
	///     {
	///         options.ConnectionString = configuration.GetConnectionString("MongoDB")!;
	///         options.DatabaseName = "events";
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseMongoDB(
		this IEventSourcingBuilder builder,
		Action<MongoDbEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbEventStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the event sourcing builder to use MongoDB with an existing client.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="clientFactory">Factory function that provides a MongoDB client.</param>
	/// <param name="configure">Action to configure event store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/>, <paramref name="clientFactory"/>, or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing MongoDB infrastructure.
	/// </remarks>
	public static IEventSourcingBuilder UseMongoDB(
		this IEventSourcingBuilder builder,
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<MongoDbEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbEventStore(clientFactory, configure);

		return builder;
	}
}
