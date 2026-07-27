// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Oracle.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>
/// Extension methods for configuring Oracle event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventSourcingBuilderOracleExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Oracle for the event store and snapshot store,
	/// bringing Oracle to parity with the other providers' <c>es =&gt; es.UseX(...)</c> composition shape.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configureEventStore">Configuration action for the Oracle event store options.</param>
	/// <param name="configureSnapshotStore">
	/// Optional configuration action for the Oracle snapshot store options. When omitted, the snapshot store
	/// is registered using the event store's connection string and the default snapshot schema/table, so a
	/// single <see cref="OracleEventStoreOptions.ConnectionString"/> is sufficient for the common case.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configureEventStore"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseOracle(o =&gt;
	///     {
	///         o.ConnectionString = configuration.GetConnectionString("EventStore")!;
	///         o.Schema = "EXCALIBUR";
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// }));
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseOracle(
		this IEventSourcingBuilder builder,
		Action<OracleEventStoreOptions> configureEventStore,
		Action<OracleSnapshotStoreOptions>? configureSnapshotStore = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureEventStore);

		// Materialize the event store options eagerly (at registration time) so the snapshot store can default
		// to the same Oracle connection — the common single-instance case. The action is a pure property setter,
		// so re-invoking it inside AddOracleEventStore's deferred Configure is harmless.
		var eventStoreOptions = new OracleEventStoreOptions();
		configureEventStore(eventStoreOptions);
		var sharedConnectionString = eventStoreOptions.ConnectionString;

		// Delegate to the existing validated Add path so UseOracle inherits the keyed "oracle"/"default"
		// registration, ValidateOnStart, default tenant context, and the transactional-append capability
		// marker — never a divergent, weaker registration.
		_ = builder.Services.AddOracleEventStore(configureEventStore);

		_ = builder.Services.AddOracleSnapshotStore(snapshotOptions =>
		{
			if (!string.IsNullOrWhiteSpace(sharedConnectionString))
			{
				snapshotOptions.ConnectionString = sharedConnectionString;
			}

			configureSnapshotStore?.Invoke(snapshotOptions);
		});

		return builder;
	}
}
