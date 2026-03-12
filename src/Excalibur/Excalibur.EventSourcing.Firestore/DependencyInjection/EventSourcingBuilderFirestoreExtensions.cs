// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Firestore;

/// <summary>
/// Extension methods for configuring Firestore event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// CDC builder pattern (see <c>EventSourcingBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderFirestoreExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Google Cloud Firestore for event storage.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for Firestore event store options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseFirestore(options =&gt;
	///     {
	///         options.ProjectId = "my-project";
	///         options.CollectionName = "events";
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseFirestore(
		this IEventSourcingBuilder builder,
		Action<FirestoreEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddFirestoreEventStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the event sourcing builder to use Google Cloud Firestore using configuration binding.
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
	///     es.UseFirestore(configuration.GetSection("Firestore"))
	///       .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseFirestore(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddFirestoreEventStore(configuration);

		return builder;
	}
}
