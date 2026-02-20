// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Audit;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres audit store services.
/// </summary>
public static class PostgresAuditExtensions
{
	/// <summary>
	/// Adds the Postgres audit store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure audit store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="PostgresAuditStore"/> as the implementation of <see cref="IAuditStore"/>.
	/// Uses JSONB for efficient storage and querying of audit event metadata.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddPostgresAuditStore(options =>
	/// {
	///     options.ConnectionString = "Host=localhost;Database=myapp;";
	///     options.SchemaName = "audit";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresAuditStore(
		this IServiceCollection services,
		Action<PostgresAuditOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<PostgresAuditOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<PostgresAuditStore>();
		services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<PostgresAuditStore>());

		return services;
	}

	/// <summary>
	/// Adds the Postgres audit store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresAuditStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddPostgresAuditStore(options =>
		{
			options.ConnectionString = connectionString;
		});
	}
}
