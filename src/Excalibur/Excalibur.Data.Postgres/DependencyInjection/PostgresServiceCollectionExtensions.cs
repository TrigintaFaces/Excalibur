// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.Postgres.ErrorHandling;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.Configuration;
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
		services.TryAddSingleton<IDeadLetterStoreAdmin>(sp => sp.GetRequiredService<PostgresDeadLetterStore>());

		return services;
	}

	/// <summary>
	/// Registers the Postgres dead letter store using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddPostgresDeadLetterStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<PostgresDeadLetterOptions>()
			.Bind(configuration);

		// Register dead letter store (uses IOptions pattern via DI)
		services.TryAddSingleton<PostgresDeadLetterStore>();
		services.TryAddSingleton<IDeadLetterStore>(sp => sp.GetRequiredService<PostgresDeadLetterStore>());
		services.TryAddSingleton<IDeadLetterStoreAdmin>(sp => sp.GetRequiredService<PostgresDeadLetterStore>());

		return services;
	}
}
