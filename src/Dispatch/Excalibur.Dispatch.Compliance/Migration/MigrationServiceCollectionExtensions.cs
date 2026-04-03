// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring migration services.
/// </summary>
public static class MigrationServiceCollectionExtensions
{
	/// <summary>
	/// Adds encryption migration services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional action to configure migration options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddEncryptionMigration(options =>
	/// {
	///     options.TargetVersion = EncryptionVersion.V1_1;
	///     options.EnableLazyReEncryption = true;
	///     options.MaxConcurrentMigrations = 8;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddEncryptionMigration(
		this IServiceCollection services,
		Action<MigrationOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.Configure(configureOptions ?? (_ => { }));
		RegisterMigrationCore(services);

		return services;
	}

	/// <summary>
	/// Adds encryption migration services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="MigrationOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddEncryptionMigration(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<MigrationOptions>().Bind(configuration).ValidateDataAnnotations().ValidateOnStart();
		RegisterMigrationCore(services);

		return services;
	}

	private static void RegisterMigrationCore(IServiceCollection services)
	{
		services.TryAddSingleton<MigrationService>();
		services.TryAddSingleton<IMigrationService>(sp => sp.GetRequiredService<MigrationService>());
		services.TryAddSingleton<IMigrationInfo>(sp => sp.GetRequiredService<MigrationService>());
	}
}
