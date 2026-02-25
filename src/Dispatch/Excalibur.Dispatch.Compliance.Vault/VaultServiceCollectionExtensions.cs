// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Compliance.Vault;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring HashiCorp Vault key management services.
/// </summary>
public static class VaultServiceCollectionExtensions
{
	/// <summary>
	/// Adds HashiCorp Vault key management services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the Vault options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddVaultKeyManagement(options =>
	/// {
	///     options.VaultUri = new Uri("https://vault.example.com:8200");
	///     options.Auth.AuthMethod = VaultAuthMethod.AppRole;
	///     options.Auth.AppRoleId = "role-id";
	///     options.Auth.AppRoleSecretId = "secret-id";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddVaultKeyManagement(
		this IServiceCollection services,
		Action<VaultOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Configure options
		_ = services.Configure(configure);

		// Add memory cache if not already registered
		_ = services.AddMemoryCache();

		// Register the provider
		services.TryAddSingleton<VaultKeyProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<VaultKeyProvider>());

		return services;
	}

	/// <summary>
	/// Adds HashiCorp Vault key management services with a pre-configured options instance.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The pre-configured options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or options is null.</exception>
	public static IServiceCollection AddVaultKeyManagement(
		this IServiceCollection services,
		VaultOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		return services.AddVaultKeyManagement(o =>
		{
			o.VaultUri = options.VaultUri;
			o.TransitMountPath = options.TransitMountPath;
			o.KeyNamePrefix = options.KeyNamePrefix;
			o.Namespace = options.Namespace;
			o.MetadataCacheDuration = options.MetadataCacheDuration;
			o.HttpTimeout = options.HttpTimeout;
			o.EnableDetailedTelemetry = options.EnableDetailedTelemetry;
			o.Auth = options.Auth;
			o.Keys = options.Keys;
			o.Retry = options.Retry;
		});
	}

	/// <summary>
	/// Adds HashiCorp Vault key management services using configuration from the specified section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configurationSection">The configuration section containing Vault settings.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configurationSection is null.</exception>
	[RequiresDynamicCode("Binding configuration and validating data annotations require dynamic code generation.")]
	[RequiresUnreferencedCode("Binding configuration and validating data annotations require unreferenced members.")]
	public static IServiceCollection AddVaultKeyManagement(
		this IServiceCollection services,
		IConfigurationSection configurationSection)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configurationSection);

		_ = services.Configure<VaultOptions>(options => configurationSection.Bind(options));

		// Add memory cache if not already registered
		_ = services.AddMemoryCache();

		// Register the provider
		services.TryAddSingleton<VaultKeyProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<VaultKeyProvider>());

		return services;
	}
}
