// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;
using Excalibur.EventSourcing.Oracle.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using global::Oracle.ManagedDataAccess.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Oracle event sourcing services.
/// </summary>
public static class OracleEventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Oracle event store with a connection factory (advanced scenarios: multi-database, custom pooling).
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating Oracle connections.</param>
	/// <param name="schema">The schema name for the event store table. Default: "EXCALIBUR".</param>
	/// <param name="table">The event store table name. Default: "EVENTSTOREEVENTS".</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleEventStore(
		this IServiceCollection services,
		Func<OracleConnection> connectionFactory,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTS")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.AddDefaultTenantContext();
		services.TryAddKeyedSingleton<IEventStore>("oracle", (sp, _) =>
			new OracleEventStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<OracleEventStore>>(),
				sp.GetService<IPayloadSerializer>(),
				schema,
				table,
				sp.GetService<ITenantContext>()));
		services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("oracle"));

		// Attest the transactional-append capability (OracleEventStore : ITransactionalEventStore) at wire
		// time so the outbox-staging validator can probe registration without resolving the store.
		services.TryAddSingleton<TransactionalEventStoreMarker>();

		return services;
	}

	/// <summary>
	/// Adds the Oracle event store configured via <see cref="OracleEventStoreOptions"/>, validated at startup.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleEventStore(
		this IServiceCollection services,
		Action<OracleEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OracleEventStoreOptions>, OracleEventStoreOptionsValidator>());
		_ = services.AddOptions<OracleEventStoreOptions>().ValidateOnStart();

		services.AddDefaultTenantContext();
		services.TryAddKeyedSingleton<IEventStore>("oracle", (sp, _) =>
		{
			var options = sp.GetRequiredService<IOptions<OracleEventStoreOptions>>().Value;
			var connectionString = options.ConnectionString
				?? throw new InvalidOperationException("Oracle event store ConnectionString is not configured.");

			return new OracleEventStore(
				() => new OracleConnection(connectionString),
				sp.GetRequiredService<ILogger<OracleEventStore>>(),
				sp.GetService<IPayloadSerializer>(),
				options.Schema,
				options.Table,
				sp.GetService<ITenantContext>());
		});
		services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("oracle"));

		// Attest the transactional-append capability (OracleEventStore : ITransactionalEventStore) at wire
		// time so the outbox-staging validator can probe registration without resolving the store.
		services.TryAddSingleton<TransactionalEventStoreMarker>();

		return services;
	}

	/// <summary>
	/// Adds the Oracle snapshot store with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating Oracle connections.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "EXCALIBUR".</param>
	/// <param name="table">The snapshot store table name. Default: "EVENTSTORESNAPSHOTS".</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleSnapshotStore(
		this IServiceCollection services,
		Func<OracleConnection> connectionFactory,
		string schema = "EXCALIBUR",
		string table = "EVENTSTORESNAPSHOTS")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		// Keyed registration mirroring the sibling IEventStore so the core's keyed-"default" consumers
		// (GDPR snapshot erasure resolves ISnapshotStore via GetKeyedService("default")) participate on Oracle.
		services.TryAddKeyedSingleton<ISnapshotStore>("oracle", (sp, _) =>
			new OracleSnapshotStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<OracleSnapshotStore>>(),
				schema,
				table));
		services.TryAddKeyedSingleton<ISnapshotStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISnapshotStore>("oracle"));

		return services;
	}

	/// <summary>
	/// Adds the Oracle snapshot store configured via <see cref="OracleSnapshotStoreOptions"/>, validated at startup.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleSnapshotStore(
		this IServiceCollection services,
		Action<OracleSnapshotStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OracleSnapshotStoreOptions>, OracleSnapshotStoreOptionsValidator>());
		_ = services.AddOptions<OracleSnapshotStoreOptions>().ValidateOnStart();

		// Keyed registration mirroring the sibling IEventStore so the core's keyed-"default" consumers
		// (GDPR snapshot erasure resolves ISnapshotStore via GetKeyedService("default")) participate on Oracle.
		services.TryAddKeyedSingleton<ISnapshotStore>("oracle", (sp, _) =>
		{
			var options = sp.GetRequiredService<IOptions<OracleSnapshotStoreOptions>>().Value;
			var connectionString = options.ConnectionString
				?? throw new InvalidOperationException("Oracle snapshot store ConnectionString is not configured.");

			return new OracleSnapshotStore(
				() => new OracleConnection(connectionString),
				sp.GetRequiredService<ILogger<OracleSnapshotStore>>(),
				options.Schema,
				options.Table);
		});
		services.TryAddKeyedSingleton<ISnapshotStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISnapshotStore>("oracle"));

		return services;
	}
}
