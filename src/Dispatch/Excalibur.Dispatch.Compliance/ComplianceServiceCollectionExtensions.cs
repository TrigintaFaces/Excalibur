// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering compliance services with dependency injection.
/// </summary>
public static class ComplianceServiceCollectionExtensions
{
	/// <summary>
	/// Adds AES-256-GCM encryption services with in-memory key management.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This configuration is suitable for development and testing. For production, use cloud KMS providers (AWS KMS, Azure Key Vault,
	/// Google Cloud KMS) or register a custom <see cref="IKeyManagementProvider" /> implementation.
	/// </para>
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureKeyManagement"> Optional configuration for key management. </param>
	/// <param name="configureEncryption"> Optional configuration for encryption. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddComplianceEncryption(
		this IServiceCollection services,
		Action<InMemoryKeyManagementOptions>? configureKeyManagement = null,
		Action<AesGcmEncryptionOptions>? configureEncryption = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options
		var keyManagementOptions = new InMemoryKeyManagementOptions();
		configureKeyManagement?.Invoke(keyManagementOptions);

		var encryptionOptions = new AesGcmEncryptionOptions();
		configureEncryption?.Invoke(encryptionOptions);

		// Register key management
		services.TryAddSingleton(keyManagementOptions);
		services.TryAddSingleton<InMemoryKeyManagementProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<InMemoryKeyManagementProvider>());

		// Register encryption provider
		services.TryAddSingleton(encryptionOptions);
		services.TryAddSingleton<AesGcmEncryptionProvider>();
		services.TryAddSingleton<IEncryptionProvider>(sp => sp.GetRequiredService<AesGcmEncryptionProvider>());

		// Register FIPS detection and validation services
		services.TryAddSingleton<IFipsDetector, DefaultFipsDetector>();
		services.TryAddSingleton<FipsValidationService>();

		return services;
	}

	/// <summary>
	/// Adds AES-256-GCM encryption with key rotation support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureKeyManagement"> Optional configuration for key management. </param>
	/// <param name="configureEncryption"> Optional configuration for encryption. </param>
	/// <param name="configureRotation"> Optional configuration for rotation. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddComplianceEncryptionWithRotation(
		this IServiceCollection services,
		Action<InMemoryKeyManagementOptions>? configureKeyManagement = null,
		Action<AesGcmEncryptionOptions>? configureEncryption = null,
		Action<RotatingEncryptionOptions>? configureRotation = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options
		var keyManagementOptions = new InMemoryKeyManagementOptions();
		configureKeyManagement?.Invoke(keyManagementOptions);

		var encryptionOptions = new AesGcmEncryptionOptions();
		configureEncryption?.Invoke(encryptionOptions);

		var rotationOptions = new RotatingEncryptionOptions();
		configureRotation?.Invoke(rotationOptions);

		// Register key management
		services.TryAddSingleton(keyManagementOptions);
		services.TryAddSingleton<InMemoryKeyManagementProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<InMemoryKeyManagementProvider>());

		// Register base encryption provider (internal)
		services.TryAddSingleton(encryptionOptions);
		services.TryAddSingleton<AesGcmEncryptionProvider>();

		// Register rotating encryption provider as the primary IEncryptionProvider
		services.TryAddSingleton(rotationOptions);
		services.TryAddSingleton(sp => new RotatingEncryptionProvider(
			sp.GetRequiredService<AesGcmEncryptionProvider>(),
			sp.GetRequiredService<IKeyManagementProvider>(),
			sp.GetRequiredService<Logging.ILogger<RotatingEncryptionProvider>>(),
			rotationOptions));
		services.TryAddSingleton<IEncryptionProvider>(sp => sp.GetRequiredService<RotatingEncryptionProvider>());

		// Register FIPS detection and validation services
		services.TryAddSingleton<IFipsDetector, DefaultFipsDetector>();
		services.TryAddSingleton<FipsValidationService>();

		return services;
	}

	/// <summary>
	/// Adds compliance encryption with a custom key management provider.
	/// </summary>
	/// <typeparam name="TKeyManagement"> The key management provider type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureEncryption"> Optional configuration for encryption. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddComplianceEncryption<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TKeyManagement>(
		this IServiceCollection services,
		Action<AesGcmEncryptionOptions>? configureEncryption = null)
		where TKeyManagement : class, IKeyManagementProvider
	{
		ArgumentNullException.ThrowIfNull(services);

		var encryptionOptions = new AesGcmEncryptionOptions();
		configureEncryption?.Invoke(encryptionOptions);

		// Register custom key management
		services.TryAddSingleton<IKeyManagementProvider, TKeyManagement>();

		// Register encryption provider
		services.TryAddSingleton(encryptionOptions);
		services.TryAddSingleton<AesGcmEncryptionProvider>();
		services.TryAddSingleton<IEncryptionProvider>(sp => sp.GetRequiredService<AesGcmEncryptionProvider>());

		// Register FIPS detection and validation services
		services.TryAddSingleton<IFipsDetector, DefaultFipsDetector>();
		services.TryAddSingleton<FipsValidationService>();

		return services;
	}

	/// <summary>
	/// Adds only the FIPS validation service without encryption providers.
	/// </summary>
	/// <remarks>
	/// Use this method when you only need FIPS compliance checking without the full encryption infrastructure. This is useful for
	/// compliance auditing or when encryption is handled by external systems.
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddFipsValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton<IFipsDetector, DefaultFipsDetector>();
		services.TryAddSingleton<FipsValidationService>();
		return services;
	}

	/// <summary>
	/// Adds FIPS validation with a custom FIPS detector.
	/// </summary>
	/// <remarks> Use this method in unit tests to mock FIPS detection behavior without requiring actual OS configuration changes. </remarks>
	/// <typeparam name="TFipsDetector"> The custom FIPS detector type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddFipsValidation<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TFipsDetector>(
		this IServiceCollection services)
		where TFipsDetector : class, IFipsDetector
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton<IFipsDetector, TFipsDetector>();
		services.TryAddSingleton<FipsValidationService>();
		return services;
	}

	/// <summary>
	/// Adds multi-region disaster recovery support for key management.
	/// </summary>
	/// <remarks>
	/// <para> Multi-region key management provides: </para>
	/// <list type="bullet">
	/// <item>
	/// <description> Active-passive failover between primary and secondary regions </description>
	/// </item>
	/// <item>
	/// <description> Automatic health monitoring and failover detection </description>
	/// </item>
	/// <item>
	/// <description> Geographic key replication for disaster recovery </description>
	/// </item>
	/// <item>
	/// <description> RPO/RTO optimization through configurable sync strategies </description>
	/// </item>
	/// </list>
	/// <para> <strong> Usage with Cloud Providers: </strong> </para>
	/// <code>
	///services.AddMultiRegionKeyManagement&lt;AzureKeyVaultProvider, AzureKeyVaultProvider&gt;(options =&gt;
	///{
	///options.Primary = new RegionConfiguration
	///{
	///RegionId = "westeurope",
	///Endpoint = new Uri("https://myvault-westeu.vault.azure.net/")
	///};
	///options.Secondary = new RegionConfiguration
	///{
	///RegionId = "northeurope",
	///Endpoint = new Uri("https://myvault-northeu.vault.azure.net/")
	///};
	///options.EnableAutomaticFailover = true;
	///options.FailoverThreshold = 3;
	///});
	/// </code>
	/// </remarks>
	/// <typeparam name="TPrimaryProvider"> The type of the primary region key management provider. </typeparam>
	/// <typeparam name="TSecondaryProvider"> The type of the secondary region key management provider. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Configuration action for multi-region options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddMultiRegionKeyManagement<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TPrimaryProvider,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TSecondaryProvider>(
		this IServiceCollection services,
		Action<MultiRegionOptions> configureOptions)
		where TPrimaryProvider : class, IKeyManagementProvider
		where TSecondaryProvider : class, IKeyManagementProvider
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var options = new MultiRegionOptions { Primary = null!, Secondary = null! };
		configureOptions(options);

		if (options.Primary is null)
		{
			throw new ArgumentException(
				Resources.ComplianceServiceCollectionExtensions_PrimaryRegionConfigurationRequired,
				nameof(configureOptions));
		}

		if (options.Secondary is null)
		{
			throw new ArgumentException(
				Resources.ComplianceServiceCollectionExtensions_SecondaryRegionConfigurationRequired,
				nameof(configureOptions));
		}

		// Register options
		_ = services.AddSingleton(options);

		// Register the provider types (keyed by region for resolution)
		_ = services.AddKeyedSingleton<IKeyManagementProvider, TPrimaryProvider>(options.Primary.RegionId);
		_ = services.AddKeyedSingleton<IKeyManagementProvider, TSecondaryProvider>(options.Secondary.RegionId);

		// Register the multi-region provider
		_ = services.AddSingleton(sp =>
		{
			var primaryProvider = sp.GetRequiredKeyedService<IKeyManagementProvider>(options.Primary.RegionId);
			var secondaryProvider = sp.GetRequiredKeyedService<IKeyManagementProvider>(options.Secondary.RegionId);
			var logger = sp.GetRequiredService<Logging.ILogger<MultiRegionKeyProvider>>();

			return new MultiRegionKeyProvider(primaryProvider, secondaryProvider, options, logger);
		});

		// Register as both IMultiRegionKeyProvider and IKeyManagementProvider
		_ = services.AddSingleton<IMultiRegionKeyProvider>(sp => sp.GetRequiredService<MultiRegionKeyProvider>());
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<MultiRegionKeyProvider>());

		return services;
	}

	/// <summary>
	/// Adds multi-region disaster recovery support using factory functions for provider creation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this overload when you need custom provider instantiation logic, such as passing region-specific configuration to each provider.
	/// </para>
	/// <code>
	///services.AddMultiRegionKeyManagement(
	///options =&gt; { /* configure options */ },
	///(sp, regionConfig) =&gt; new AzureKeyVaultProvider(regionConfig.Endpoint),
	///(sp, regionConfig) =&gt; new AzureKeyVaultProvider(regionConfig.Endpoint));
	/// </code>
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Configuration action for multi-region options. </param>
	/// <param name="primaryProviderFactory"> Factory function to create the primary provider. </param>
	/// <param name="secondaryProviderFactory"> Factory function to create the secondary provider. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddMultiRegionKeyManagement(
		this IServiceCollection services,
		Action<MultiRegionOptions> configureOptions,
		Func<IServiceProvider, RegionConfiguration, IKeyManagementProvider> primaryProviderFactory,
		Func<IServiceProvider, RegionConfiguration, IKeyManagementProvider> secondaryProviderFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);
		ArgumentNullException.ThrowIfNull(primaryProviderFactory);
		ArgumentNullException.ThrowIfNull(secondaryProviderFactory);

		var options = new MultiRegionOptions { Primary = null!, Secondary = null! };
		configureOptions(options);

		if (options.Primary is null)
		{
			throw new ArgumentException(
				Resources.ComplianceServiceCollectionExtensions_PrimaryRegionConfigurationRequired,
				nameof(configureOptions));
		}

		if (options.Secondary is null)
		{
			throw new ArgumentException(
				Resources.ComplianceServiceCollectionExtensions_SecondaryRegionConfigurationRequired,
				nameof(configureOptions));
		}

		// Register options
		_ = services.AddSingleton(options);

		// Register the multi-region provider with factory-created providers
		_ = services.AddSingleton(sp =>
		{
			var primaryProvider = primaryProviderFactory(sp, options.Primary);
			var secondaryProvider = secondaryProviderFactory(sp, options.Secondary);
			var logger = sp.GetRequiredService<Logging.ILogger<MultiRegionKeyProvider>>();

			return new MultiRegionKeyProvider(primaryProvider, secondaryProvider, options, logger);
		});

		// Register as both IMultiRegionKeyProvider and IKeyManagementProvider
		_ = services.AddSingleton<IMultiRegionKeyProvider>(sp => sp.GetRequiredService<MultiRegionKeyProvider>());
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<MultiRegionKeyProvider>());

		return services;
	}

	/// <summary>
	/// Adds automatic key rotation services as a background service.
	/// </summary>
	/// <remarks>
	/// <para> Automatic key rotation is required for compliance: </para>
	/// <list type="bullet">
	/// <item>
	/// <description> Default 90-day rotation cycle (configurable per policy) </description>
	/// </item>
	/// <item>
	/// <description> Per-key-purpose rotation schedules </description>
	/// </item>
	/// <item>
	/// <description> Zero-downtime rotation with key versioning </description>
	/// </item>
	/// <item>
	/// <description> Rotation success/failure metrics </description>
	/// </item>
	/// </list>
	/// <para> <strong> Prerequisites: </strong> An <see cref="IKeyManagementProvider" /> must be registered. </para>
	/// <code>
	///services.AddComplianceEncryption();
	///services.AddKeyRotation(options =&gt;
	///{
	///options.CheckInterval = TimeSpan.FromHours(1);
	///options.DefaultPolicy = KeyRotationPolicy.Default;
	///options.AddHighSecurityPolicy("pii-encryption");
	///options.AddArchivalPolicy("backup-encryption");
	///});
	/// </code>
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration for key rotation. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddKeyRotation(
		this IServiceCollection services,
		Action<KeyRotationOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options
		var optionsBuilder = services.AddOptions<KeyRotationOptions>();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register the rotation service as both background service and scheduler interface
		_ = services.AddSingleton<KeyRotationService>();
		_ = services.AddHostedService(sp => sp.GetRequiredService<KeyRotationService>());
		services.TryAddSingleton<IKeyRotationScheduler>(sp => sp.GetRequiredService<KeyRotationService>());

		return services;
	}

	/// <summary>
	/// Adds automatic key rotation with a specific scheduler implementation.
	/// </summary>
	/// <typeparam name="TScheduler"> The scheduler implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration for key rotation. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddKeyRotation<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TScheduler>(
		this IServiceCollection services,
		Action<KeyRotationOptions>? configureOptions = null)
		where TScheduler : class, IKeyRotationScheduler
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options
		var optionsBuilder = services.AddOptions<KeyRotationOptions>();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register the custom scheduler
		services.TryAddSingleton<IKeyRotationScheduler, TScheduler>();

		return services;
	}
}
