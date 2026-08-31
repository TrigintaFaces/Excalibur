// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.EventSourcing.Sqlite.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQLite event sourcing services.
/// </summary>
public static class SqliteEventSourcingServiceCollectionExtensions
{
	/// <summary>The service key this provider publishes its stores under.</summary>
	private const string SqliteProviderKey = "sqlite";

	/// <summary>The service key the core's non-keyed store aliases forward to.</summary>
	private const string DefaultProviderKey = "default";

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
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseSqlite(options =&gt;
	///     {
	///         options.ConnectionString = "Data Source=events.db";
	///     });
	/// }));
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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

		_ = builder.Services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store (injecting ITenantContext for the row-level tenant
		// predicate, since this store's constructor declares one) AND emits the
		// ITenantScopingCapability<IEventStore> marker inseparably (matching the seam every other provider
		// registers IEventStore through) -- the marker cannot exist without the store that must honor the
		// tenant, and AddMultiTenancy(RowDiscriminator)'s fail-closed capability check now finds a
		// truthful marker for SQLite instead of refusing to start.
		builder.Services.AddTenantAwareStore<IEventStore, SqliteEventStore>(sp =>
			new SqliteEventStore(
				options.ConnectionString,
				sp.GetRequiredService<ILogger<SqliteEventStore>>(),
				tenantContext: sp.GetRequiredService<ITenantContext>(),
				table: options.EventStoreTable,
				tenantContextOptions: sp.GetRequiredService<IOptions<TenantContextOptions>>(),
				eventTypeInfoResolver: options.EventTypeInfoResolver));

		// Mirror the event store registration immediately above: AddTenantAwareStore injects ITenantContext
		// AND emits the ITenantScopingCapability<ISnapshotStore> marker inseparably, matching how Postgres
		// registers its snapshot store. ISnapshotStore is not in the RowDiscriminator gate's required-contract
		// list, so this does not change what AddMultiTenancy accepts or rejects -- it removes the one
		// remaining path (a bare TryAddSingleton) through which this store could have been built without the
		// tenant context it requires.
		builder.Services.AddTenantAwareStore<ISnapshotStore, SqliteSnapshotStore>(sp =>
			new SqliteSnapshotStore(
				options.ConnectionString,
				sp.GetRequiredService<ILogger<SqliteSnapshotStore>>(),
				tenantContext: sp.GetRequiredService<ITenantContext>(),
				table: options.SnapshotStoreTable,
				tenantContextOptions: sp.GetRequiredService<IOptions<TenantContextOptions>>()));

		// AddTenantAwareStore registers the CONCRETE store -- it names the contract only to decide which
		// tenancy capability to attest. Publishing the contract is the provider's job, and it is keyed: the
		// core registers a non-keyed IEventStore/ISnapshotStore alias that forwards to the "default" key, so
		// a provider that registers neither key leaves both the keyed and the non-keyed resolution with
		// nothing behind them. Every sibling provider closes this the same way, and SQLite alone did not --
		// which made the startup prerequisite validator report a correctly-configured host as having no
		// event store at all, and made anything resolving the contract (a repository, erasure, notification)
		// fail at the point of use.
		builder.Services.TryAddKeyedSingleton<IEventStore>(
			SqliteProviderKey, (sp, _) => sp.GetRequiredService<SqliteEventStore>());
		builder.Services.TryAddKeyedSingleton<IEventStore>(
			DefaultProviderKey, (sp, _) => sp.GetRequiredKeyedService<IEventStore>(SqliteProviderKey));

		builder.Services.TryAddKeyedSingleton<ISnapshotStore>(
			SqliteProviderKey, (sp, _) => sp.GetRequiredService<SqliteSnapshotStore>());
		builder.Services.TryAddKeyedSingleton<ISnapshotStore>(
			DefaultProviderKey, (sp, _) => sp.GetRequiredKeyedService<ISnapshotStore>(SqliteProviderKey));

		return builder;
	}
}
