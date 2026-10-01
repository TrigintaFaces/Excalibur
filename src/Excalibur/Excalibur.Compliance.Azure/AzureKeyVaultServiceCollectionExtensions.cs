// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Azure.Security.KeyVault.Keys;

using Excalibur.Compliance;
using Excalibur.Compliance.Azure;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure Key Vault key management services.
/// </summary>
public static class AzureKeyVaultServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Key Vault key management services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Azure Key Vault compliance builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddAzureKeyVaultKeyManagement(azure =&gt;
	/// {
	///     azure.VaultUri(new Uri("https://my-vault.vault.azure.net/"))
	///          .RequirePremiumTier();
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddAzureKeyVaultKeyManagement(
		this IServiceCollection services,
		Action<IComplianceAzureBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new AzureKeyVaultOptions();
		var builder = new ComplianceAzureBuilder(options);
		configure(builder);

		RegisterOptionsAndServices(services, builder, options);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		ComplianceAzureBuilder builder,
		AzureKeyVaultOptions options)
	{
		_ = services.Configure<AzureKeyVaultOptions>(opt =>
		{
			opt.VaultUri = options.VaultUri;
			opt.KeyNamePrefix = options.KeyNamePrefix;
			opt.RequirePremiumTier = options.RequirePremiumTier;
			opt.MetadataCacheDuration = options.MetadataCacheDuration;
			opt.EnableDetailedTelemetry = options.EnableDetailedTelemetry;
		});

		if (builder.BindConfigurationPath is not null)
		{
			_ = services.AddOptions<AzureKeyVaultOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<AzureKeyVaultOptions>().ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AzureKeyVaultOptions>, AzureKeyVaultOptionsValidator>());

		// Add memory cache if not already registered
		_ = services.AddMemoryCache();

		// Register the provider.
		//
		// Constructed explicitly, with NAMED arguments, rather than left to constructor selection. The provider
		// has two constructors differing only in a trailing KeyClient, and which one the container picks would
		// otherwise depend on whether that service happens to be registered -- a resolution rule this file does
		// not state and a reader cannot see. Naming each argument also makes a future parameter that nobody wires
		// a compile error here instead of a silently absent dependency.
		//
		// GetService, not GetRequiredService: supplying the client is optional, and its absence means "build one
		// from the options" rather than a misconfiguration.
		services.TryAddSingleton(sp => new AzureKeyVaultProvider(
			options: sp.GetRequiredService<IOptions<AzureKeyVaultOptions>>(),
			cache: sp.GetRequiredService<IMemoryCache>(),
			logger: sp.GetRequiredService<ILogger<AzureKeyVaultProvider>>(),
			keyClient: sp.GetService<KeyClient>()));
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<AzureKeyVaultProvider>());
		services.TryAddSingleton<IKeyManagementAdmin>(sp => sp.GetRequiredService<AzureKeyVaultProvider>());
	}
}
