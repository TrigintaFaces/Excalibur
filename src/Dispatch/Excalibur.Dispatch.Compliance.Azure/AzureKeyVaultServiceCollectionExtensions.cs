// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Compliance.Azure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure Key Vault key management services.
/// </summary>
public static class AzureKeyVaultServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Key Vault key management services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An action to configure the Azure Key Vault options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configure is null. </exception>
	/// <example>
	/// <code>
	///services.AddAzureKeyVaultKeyManagement(options =&gt;
	///{
	///options.VaultUri = new Uri("https://my-vault.vault.azure.net/");
	///options.RequirePremiumTier = true; // For FIPS compliance
	///});
	/// </code>
	/// </example>
	public static IServiceCollection AddAzureKeyVaultKeyManagement(
		this IServiceCollection services,
		Action<AzureKeyVaultOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Configure options
		_ = services.Configure(configure);

		// Add memory cache if not already registered
		_ = services.AddMemoryCache();

		// Register the provider
		services.TryAddSingleton<AzureKeyVaultProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<AzureKeyVaultProvider>());

		return services;
	}

	/// <summary>
	/// Adds Azure Key Vault key management services with a pre-configured options instance.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="options"> The pre-configured options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or options is null. </exception>
	public static IServiceCollection AddAzureKeyVaultKeyManagement(
		this IServiceCollection services,
		AzureKeyVaultOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		return services.AddAzureKeyVaultKeyManagement(o =>
		{
			o.VaultUri = options.VaultUri;
			o.Credential = options.Credential;
			o.KeyNamePrefix = options.KeyNamePrefix;
			o.RequirePremiumTier = options.RequirePremiumTier;
			o.WarnOnStandardTierInProduction = options.WarnOnStandardTierInProduction;
			o.MetadataCacheDuration = options.MetadataCacheDuration;
			o.EnableRetry = options.EnableRetry;
			o.MaxRetryAttempts = options.MaxRetryAttempts;
			o.RetryDelay = options.RetryDelay;
			o.UseSoftwareKeys = options.UseSoftwareKeys;
			o.DefaultKeySizeBits = options.DefaultKeySizeBits;
			o.EnableDetailedTelemetry = options.EnableDetailedTelemetry;
		});
	}

	/// <summary>
	/// Adds Azure Key Vault RSA key wrapping services for envelope encryption.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An action to configure the RSA key wrapping options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="AzureKeyVaultRsaKeyWrapper" /> as the
	/// <see cref="IAzureRsaKeyWrapper" /> implementation for wrapping and unwrapping
	/// AES data encryption keys using RSA keys in Azure Key Vault.
	/// </para>
	/// <para>
	/// This method also registers Azure Key Vault key management if not already configured,
	/// since the RSA key wrapper depends on <see cref="AzureKeyVaultOptions" /> for credential resolution.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAzureKeyVaultRsaKeyWrapping(options =&gt;
	/// {
	///     options.KeyVaultUrl = new Uri("https://my-vault.vault.azure.net/");
	///     options.KeyName = "data-encryption-key";
	///     options.Algorithm = RsaWrappingAlgorithm.RsaOaep256;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAzureKeyVaultRsaKeyWrapping(
		this IServiceCollection services,
		Action<RsaKeyWrappingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RsaKeyWrappingOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<IAzureRsaKeyWrapper, AzureKeyVaultRsaKeyWrapper>();

		return services;
	}

	/// <summary>
	/// Adds Azure Key Vault key management services using configuration from the specified section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configurationSection"> The configuration section containing Azure Key Vault settings. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configurationSection is null. </exception>
	[RequiresDynamicCode("Binding configuration requires dynamic code generation.")]
	[RequiresUnreferencedCode("Binding configuration requires unreferenced members.")]
	public static IServiceCollection AddAzureKeyVaultKeyManagement(
		this IServiceCollection services,
		IConfigurationSection configurationSection)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configurationSection);

		_ = services.Configure<AzureKeyVaultOptions>(options => configurationSection.Bind(options));

		// Add memory cache if not already registered
		_ = services.AddMemoryCache();

		// Register the provider
		services.TryAddSingleton<AzureKeyVaultProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<AzureKeyVaultProvider>());

		return services;
	}
}
