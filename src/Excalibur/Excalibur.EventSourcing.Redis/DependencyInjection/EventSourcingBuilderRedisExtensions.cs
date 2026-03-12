// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Redis;

/// <summary>
/// Extension methods for configuring Redis event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// CDC builder pattern (see <c>EventSourcingBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderRedisExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Redis for event store and snapshot store.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseRedis("localhost:6379")
	///       .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseRedis(
		this IEventSourcingBuilder builder,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddRedisEventSourcing(connectionString);

		return builder;
	}

	/// <summary>
	/// Configures the event sourcing builder to use Redis with separate event store
	/// and snapshot store configuration.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configureEventStore">Configuration action for Redis event store options.</param>
	/// <param name="configureSnapshotStore">Configuration action for Redis snapshot store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/>, <paramref name="configureEventStore"/>,
	/// or <paramref name="configureSnapshotStore"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseRedis(
	///         eventStore =&gt; eventStore.ConnectionString = "localhost:6379",
	///         snapshotStore =&gt; snapshotStore.ConnectionString = "localhost:6379")
	///       .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseRedis(
		this IEventSourcingBuilder builder,
		Action<RedisEventStoreOptions> configureEventStore,
		Action<RedisSnapshotStoreOptions> configureSnapshotStore)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureEventStore);
		ArgumentNullException.ThrowIfNull(configureSnapshotStore);

		_ = builder.Services.AddRedisEventSourcing(configureEventStore, configureSnapshotStore);

		return builder;
	}
}
