// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Postgres.ErrorHandling;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres data services.
/// </summary>
public static class PostgresServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Postgres dead letter store.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresDeadLetterStore(
		this IServiceCollection services,
		string connectionString)
	{
		return services.AddPostgresDeadLetterStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Registers the Postgres dead letter store with full configuration options.
	/// Uses IOptions pattern for configuration consistency with other Postgres stores.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the dead letter options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresDeadLetterStore(
		this IServiceCollection services,
		Action<PostgresDeadLetterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register options via IOptions pattern
		_ = services.Configure(configure);

		// Validate options at startup
		var options = new PostgresDeadLetterOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new ArgumentException("ConnectionString must be provided", nameof(configure));
		}

		// Register dead letter store (uses IOptions pattern via DI)
		services.TryAddSingleton<PostgresDeadLetterStore>();
		services.TryAddSingleton<IDeadLetterStore>(sp => sp.GetRequiredService<PostgresDeadLetterStore>());

		return services;
	}
}
