// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.EventSourcing.Sqlite.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQLite event sourcing services.
/// </summary>
public static class SqliteEventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Configures SQLite as the event sourcing provider.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for SQLite options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="SqliteEventSourcingOptions.ConnectionString"/> is not configured.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseSqlite(options =&gt;
	///     {
	///         options.ConnectionString = "Data Source=events.db";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseSqlite(
		this IEventSourcingBuilder builder,
		Action<SqliteEventSourcingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqliteEventSourcingOptions();
		configure(options);

		return builder.UseSqliteCore(options);
	}

	/// <summary>
	/// Configures SQLite as the event sourcing provider using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SqliteEventSourcingOptions"/>.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="SqliteEventSourcingOptions.ConnectionString"/> is not configured.
	/// </exception>
	public static IEventSourcingBuilder UseSqlite(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new SqliteEventSourcingOptions();
		configuration.Bind(options);

		return builder.UseSqliteCore(options);
	}

	private static IEventSourcingBuilder UseSqliteCore(
		this IEventSourcingBuilder builder,
		SqliteEventSourcingOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for SQLite event sourcing. " +
				"Set SqliteEventSourcingOptions.ConnectionString (e.g., \"Data Source=events.db\").");
		}

		builder.Services.TryAddSingleton<IEventStore>(sp =>
			new SqliteEventStore(
				options.ConnectionString,
				sp.GetRequiredService<ILogger<SqliteEventStore>>(),
				options.EventStoreTable));

		builder.Services.TryAddSingleton<ISnapshotStore>(sp =>
			new SqliteSnapshotStore(
				options.ConnectionString,
				sp.GetRequiredService<ILogger<SqliteSnapshotStore>>(),
				options.SnapshotStoreTable));

		return builder;
	}
}
