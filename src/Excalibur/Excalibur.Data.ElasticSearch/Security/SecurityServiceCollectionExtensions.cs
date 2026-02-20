// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Elasticsearch security services in the dependency injection container.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
	/// <summary>
	/// Adds comprehensive Elasticsearch security services to the specified service collection.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind security settings from. </param>
	/// <param name="configureOptions"> Optional action to configure security settings. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddElasticsearchSecurity(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<ElasticsearchSecurityOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		ArgumentNullException.ThrowIfNull(configuration);

		// Configure security settings
		var securitySection = configuration.GetSection("Elasticsearch:Security");
		_ = services.Configure<ElasticsearchSecurityOptions>(securitySection);

		if (configureOptions != null)
		{
			_ = services.Configure(configureOptions);
		}

		// Register core security services
		_ = services.AddSecurityCore();
		_ = services.AddAuthentication(configuration);
		_ = services.AddFieldEncryption();
		_ = services.AddKeyManagement(configuration);
		_ = services.AddSecurityAuditing();
		_ = services.AddSecurityMonitoring();

		return services;
	}

	/// <summary>
	/// Adds authentication services with configurable providers.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind authentication settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddAuthentication(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Configure authentication settings
		_ = services.Configure<AuthenticationOptions>(
			configuration.GetSection("Elasticsearch:Security:Authentication"));

		// Register authentication provider
		services.TryAddSingleton<IElasticsearchAuthenticationProvider, SecureElasticsearchAuthenticationProvider>();

		return services;
	}

	/// <summary>
	/// Adds field-level encryption services.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddFieldEncryption(this IServiceCollection services)
	{
		// Register field encryption service Default encryption options are already configured in the EncryptionOptions class
		services.TryAddSingleton<IElasticsearchFieldEncryptor, FieldEncryptor>();

		return services;
	}

	/// <summary>
	/// Adds key management services with configurable providers.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind key management settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddKeyManagement(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Configure key management settings
		var keyManagementSection = configuration.GetSection("Elasticsearch:Security:Encryption:KeyManagement");
		_ = services.Configure<KeyManagementOptions>(keyManagementSection);

		// Register key provider based on configuration
		var provider = keyManagementSection.GetValue<KeyManagementProvider>("Provider");
		_ = provider switch
		{
			KeyManagementProvider.AzureKeyVault => services.AddAzureKeyVault(configuration),
			KeyManagementProvider.AwsKms => services.AddAwsKms(configuration),
			KeyManagementProvider.GoogleCloudKms => services.AddGoogleCloudKms(configuration),
			KeyManagementProvider.HashiCorpVault => services.AddHashiCorpVault(configuration),
			_ => services.AddLocalKeyProvider(),
		};
		return services;
	}

	/// <summary>
	/// Adds Azure Key Vault integration.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind Azure Key Vault settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddAzureKeyVault(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		_ = services.Configure<AzureKeyVaultOptions>(
			configuration.GetSection("Elasticsearch:Security:KeyManagement:AzureKeyVault"));

		services.TryAddSingleton<IElasticsearchKeyProvider, AzureKeyVaultProvider>();

		return services;
	}

	/// <summary>
	/// Adds AWS KMS integration.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind AWS KMS settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddAwsKms(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// AWS KMS implementation would be added here
		services.TryAddSingleton<IElasticsearchKeyProvider, LocalKeyProvider>();
		return services;
	}

	/// <summary>
	/// Adds Google Cloud KMS integration.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind Google Cloud KMS settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddGoogleCloudKms(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Google Cloud KMS implementation would be added here
		services.TryAddSingleton<IElasticsearchKeyProvider, LocalKeyProvider>();
		return services;
	}

	/// <summary>
	/// Adds HashiCorp Vault integration.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind HashiCorp Vault settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddHashiCorpVault(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// HashiCorp Vault implementation would be added here
		services.TryAddSingleton<IElasticsearchKeyProvider, LocalKeyProvider>();
		return services;
	}

	/// <summary>
	/// Adds local key provider for development and testing.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddLocalKeyProvider(this IServiceCollection services)
	{
		services.TryAddSingleton<IElasticsearchKeyProvider, LocalKeyProvider>();
		return services;
	}

	/// <summary>
	/// Adds security auditing services.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddSecurityAuditing(this IServiceCollection services)
	{
		// Configure audit settings Audit settings have default values already configured in the class definition Remove Configure call
		// since init-only properties cannot be set this way

		// Register auditing service (core + sub-interfaces forwarded to the same singleton)
		services.TryAddSingleton<SecurityAuditor>();
		services.TryAddSingleton<IElasticsearchSecurityAuditor>(static sp => sp.GetRequiredService<SecurityAuditor>());
		services.TryAddSingleton<IElasticsearchSecurityAuditorReporting>(static sp => sp.GetRequiredService<SecurityAuditor>());
		services.TryAddSingleton<IElasticsearchSecurityAuditorMaintenance>(static sp => sp.GetRequiredService<SecurityAuditor>());

		return services;
	}

	/// <summary>
	/// Adds security monitoring and threat detection services.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddSecurityMonitoring(this IServiceCollection services)
	{
		// Configure monitoring settings Security monitoring settings have default values already configured in the class definition Remove
		// Configure call since init-only properties cannot be set this way

		// Register monitoring service
		services.TryAddSingleton<IElasticsearchSecurityMonitor, SecurityMonitor>();

		// Register background service for monitoring
		_ = services.AddHostedService<SecurityMonitoringBackgroundService>();

		return services;
	}

	/// <summary>
	/// Adds core security services and infrastructure.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for method chaining. </returns>
	private static IServiceCollection AddSecurityCore(this IServiceCollection services)
	{
		// Register security event handlers
		services.TryAddSingleton<SecurityEventAggregator>();

		// Register security policy engine
		services.TryAddSingleton<SecurityPolicyEngine>();

		return services;
	}
}
