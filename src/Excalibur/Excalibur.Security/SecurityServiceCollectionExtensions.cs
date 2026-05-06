// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Security;


using Excalibur.Security.EventStores;

using Microsoft.Extensions.DependencyInjection.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring security services in the dependency injection container.
/// </summary>
public static class SecurityServiceCollectionExtensions
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

		// Add security middleware (encryption, signing, rate limiting, JWT authentication)
		_ = services.AddDispatchSecurityMiddleware(configuration);

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

		// Register credential stores (TryAdd = idempotent on repeated calls)
		services.TryAddSingleton<ICredentialStore, EnvironmentVariableCredentialStore>();

		// Note: Azure Key Vault and AWS Secrets Manager credential stores have been moved to:
		// - Excalibur.Security.Azure.AddAzureKeyVaultCredentialStore()
		// - Excalibur.Security.Aws.AddAwsSecretsManagerCredentialStore()
		// Use those packages for cloud-specific credential management.

		// Add HashiCorp Vault if configured
		var vaultUrl = configuration["Vault:Url"];
		if (!string.IsNullOrEmpty(vaultUrl))
		{
			services.TryAddSingleton<ICredentialStore, HashiCorpVaultCredentialStore>();
			services.TryAddSingleton<IWritableCredentialStore, HashiCorpVaultCredentialStore>();
		}

		// Register the main credential provider
		services.TryAddSingleton<ISecureCredentialProvider, SecureCredentialProvider>();

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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddInputValidation(
	this IServiceCollection services,
	IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure validation options
		_ = services.AddOptions<InputValidationOptions>()
			.Bind(configuration.GetSection("Security:InputValidation"))
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InputValidationOptions>, InputValidationOptionsValidator>());

		_ = services.AddSingleton(static sp => sp.GetRequiredService<IOptions<InputValidationOptions>>().Value);

		// Register validation middleware concrete type for pipeline resolution
		services.TryAddSingleton<InputValidationMiddleware>();

		// No default validators registered -- IInputValidator is a consumer extension point.
		// Consumers register their own validators for their application's specific needs.
		// SQL injection prevention belongs in parameterized queries, not message-level validation.
		// XSS prevention belongs in output encoding, not message-level validation.

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

		// Register security event logger using forwarding pattern to avoid hard-cast
		services.TryAddSingleton<SecurityEventLogger>();
		services.TryAddSingleton<ISecurityEventLogger>(static sp => sp.GetRequiredService<SecurityEventLogger>());
		_ = services.AddHostedService(static sp => sp.GetRequiredService<SecurityEventLogger>());

		// Register event store based on configuration (TryAdd = idempotent on repeated calls)
		var storeType = configuration["Security:Auditing:StoreType"];
		switch (storeType?.ToUpperInvariant())
		{
			case "SQL":
				services.TryAddSingleton<ISecurityEventStore, SqlSecurityEventStore>();
				break;
			case "ELASTICSEARCH":
				services.TryAddSingleton<ISecurityEventStore, ElasticsearchSecurityEventStore>();
				break;
			case "FILE":
				services.TryAddSingleton<ISecurityEventStore, FileSecurityEventStore>();
				break;
			default:
				services.TryAddSingleton<ISecurityEventStore, InMemorySecurityEventStore>(); // Default to in-memory for development
				break;
		}

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
