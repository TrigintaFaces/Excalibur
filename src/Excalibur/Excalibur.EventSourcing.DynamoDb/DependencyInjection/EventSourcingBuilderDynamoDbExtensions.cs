// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.DynamoDb;

/// <summary>
/// Extension methods for configuring DynamoDB event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// CDC builder pattern (see <c>EventSourcingBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderDynamoDbExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use AWS DynamoDB for event storage.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for DynamoDB event store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseDynamoDb(options =&gt;
	///     {
	///         options.EventsTableName = "event-store";
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseDynamoDb(
		this IEventSourcingBuilder builder,
		Action<DynamoDbEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddDynamoDbEventStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the event sourcing builder to use AWS DynamoDB using configuration binding.
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
	///     es.UseDynamoDb(configuration.GetSection("DynamoDb"))
	///       .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseDynamoDb(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddDynamoDbEventStore(configuration);

		return builder;
	}
}
