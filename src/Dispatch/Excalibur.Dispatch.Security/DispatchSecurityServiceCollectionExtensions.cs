// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring security services in the dependency injection container.
/// </summary>
public static class DispatchSecurityServiceCollectionExtensions
{
	/// <summary>
	/// Adds comprehensive security services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration instance. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Security service registration uses reflection for dependency injection and configuration binding")]
	[RequiresDynamicCode("Security service registration uses reflection to scan and register middleware and validators")]
	public static IServiceCollection AddDispatchSecurity(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Add credential management
		_ = services.AddSecureCredentialManagement(configuration);

		// Add input validation
		_ = services.AddInputValidation(configuration);

		// Add security auditing
		_ = services.AddSecurityAuditing(configuration);

		// Add security validators for cloud providers
		_ = services.AddCloudProviderSecurityValidators();

		return services;
	}

	/// <summary>
	/// Adds secure credential management services.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration used to determine credential stores.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSecureCredentialManagement(
	this IServiceCollection services,
	IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Register credential stores
		_ = services.AddSingleton<ICredentialStore, EnvironmentVariableCredentialStore>();

		// Note: Azure Key Vault and AWS Secrets Manager credential stores have been moved to:
		// - Excalibur.Dispatch.Security.Azure.AddAzureKeyVaultCredentialStore()
		// - Excalibur.Dispatch.Security.Aws.AddAwsSecretsManagerCredentialStore()
		// Use those packages for cloud-specific credential management.

		// Add HashiCorp Vault if configured
		var vaultUrl = configuration["Vault:Url"];
		if (!string.IsNullOrEmpty(vaultUrl))
		{
			_ = services.AddSingleton<ICredentialStore, HashiCorpVaultCredentialStore>();
			_ = services.AddSingleton<IWritableCredentialStore, HashiCorpVaultCredentialStore>();
		}

		// Register the main credential provider
		_ = services.AddSingleton<ISecureCredentialProvider, SecureCredentialProvider>();

		return services;
	}

	/// <summary>
	/// Adds input validation services and middleware.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration containing input validation settings.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for input validation settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddInputValidation(
	this IServiceCollection services,
	IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure validation options
		_ = services.AddOptions<InputValidationOptions>()
			.Bind(configuration.GetSection("Security:InputValidation"))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton(static sp => sp.GetRequiredService<IOptions<InputValidationOptions>>().Value);

		// Register validation middleware
		_ = services.AddSingleton<IDispatchMiddleware, InputValidationMiddleware>();

		// Register default validators
		_ = services.AddSingleton<IInputValidator, SqlInjectionValidator>();
		_ = services.AddSingleton<IInputValidator, XssValidator>();
		_ = services.AddSingleton<IInputValidator, PathTraversalValidator>();
		_ = services.AddSingleton<IInputValidator, CommandInjectionValidator>();
		_ = services.AddSingleton<IInputValidator, DataSizeValidator>();
		_ = services.AddSingleton<IInputValidator, MessageAgeValidator>();

		return services;
	}

	/// <summary>
	/// Adds security auditing services.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration containing auditing settings.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("Security auditing registration uses reflection for service instantiation and configuration binding")]
	public static IServiceCollection AddSecurityAuditing(
	this IServiceCollection services,
	IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Register security event logger
		_ = services.AddSingleton<ISecurityEventLogger, SecurityEventLogger>();
		_ = services.AddHostedService(static sp => (SecurityEventLogger)sp.GetRequiredService<ISecurityEventLogger>());

		// Register event store based on configuration
		var storeType = configuration["Security:Auditing:StoreType"];
		_ = (storeType?.ToUpperInvariant()) switch
		{
			"SQL" => services.AddSingleton<ISecurityEventStore, SqlSecurityEventStore>(),
			"ELASTICSEARCH" => services.AddSingleton<ISecurityEventStore, ElasticsearchSecurityEventStore>(),
			"FILE" => services.AddSingleton<ISecurityEventStore, FileSecurityEventStore>(),
			_ => services.AddSingleton<ISecurityEventStore, InMemorySecurityEventStore>(), // Default to in-memory for development
		};
		return services;
	}

	/// <summary>
	/// Adds security validators for cloud provider configurations.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCloudProviderSecurityValidators(this IServiceCollection services)
	{
		// Add validators for cloud provider options (TryAddEnumerable prevents duplicates)
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RabbitMqOptions>, RabbitMqOptionsValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AwsSqsOptions>, AwsSqsOptionsValidator>());
		// Note: AzureServiceBusOptionsValidator has been moved to Excalibur.Dispatch.Security.Azure.AddAzureServiceBusSecurityValidation()
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<KafkaOptions>, KafkaOptionsValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<GooglePubSubOptions>, GooglePubSubOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds the security middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder instance.</param>
	/// <returns>The builder for chaining.</returns>
	public static IDispatchBuilder UseSecurityMiddleware(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Add input validation middleware early in the pipeline Middleware is automatically registered in services and will be used by the pipeline
		return builder;
	}
}

// Additional validator implementations would go in separate files

// Enterprise credential store implementations

// Placeholder types - these would be defined in their respective projects
