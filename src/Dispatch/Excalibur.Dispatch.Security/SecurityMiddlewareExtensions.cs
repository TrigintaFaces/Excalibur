// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;





using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Extension methods for registering security middleware and services.
/// </summary>
public static class SecurityMiddlewareExtensions
{
	/// <summary>
	/// Adds comprehensive security services to the Dispatch pipeline.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Security middleware registration uses reflection for dependency injection and configuration binding")]
	[RequiresDynamicCode("Security middleware registration uses reflection to instantiate and configure middleware components")]
	public static IServiceCollection AddDispatchSecurity(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Add all security components with default configuration
		_ = services.AddMessageEncryption(configuration.GetSection("Security:Encryption"));
		_ = services.AddMessageSigning(configuration.GetSection("Security:Signing"));
		_ = services.AddRateLimiting(configuration.GetSection("Security:RateLimiting"));
		_ = services.AddJwtAuthentication(configuration.GetSection("Security:Authentication"));

		return services;
	}

	/// <summary>
	/// Adds comprehensive security services with custom configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure security options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDispatchSecurity(
		this IServiceCollection services,
		Action<SecurityOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var options = new SecurityOptions();
		configureOptions(options);

		// Configure individual components based on options
		if (options.EnableEncryption)
		{
			_ = services.AddMessageEncryption(opt =>
			{
				opt.Enabled = true;
				opt.DefaultAlgorithm = options.EncryptionAlgorithm;
				opt.AzureKeyVaultUrl = options.AzureKeyVaultUrl;
				opt.AwsKmsKeyArn = options.AwsKmsKeyArn;
			});
		}

		if (options.EnableSigning)
		{
			_ = services.AddMessageSigning(opt =>
			{
				opt.Enabled = true;
				opt.DefaultAlgorithm = options.SigningAlgorithm;
			});
		}

		if (options.EnableRateLimiting)
		{
			_ = services.AddRateLimiting(opt =>
			{
				opt.Enabled = true;
				opt.Algorithm = options.RateLimitAlgorithm;
				opt.DefaultLimits = options.DefaultRateLimits;
			});
		}

		if (options.EnableAuthentication)
		{
			_ = services.AddJwtAuthentication(opt =>
			{
				opt.Enabled = true;
				opt.RequireAuthentication = options.RequireAuthentication;
				opt.Credentials.ValidIssuer = options.JwtIssuer;
				opt.Credentials.ValidAudience = options.JwtAudience;
				opt.Credentials.SigningKey = options.JwtSigningKey;
			});
		}

		return services;
	}

	/// <summary>
	/// Adds message encryption services to the DI container.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section containing encryption settings.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for encryption settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddMessageEncryption(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<EncryptionOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return services.AddMessageEncryption();
	}

	/// <summary>
	/// Adds message encryption services with custom configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration delegate for encryption options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMessageEncryption(
		this IServiceCollection services,
		Action<EncryptionOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options if provided
		var optionsBuilder = services.AddOptions<EncryptionOptions>();
		if (configureOptions != null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Add DataProtection with Azure Key Vault if configured
		_ = services.AddDataProtection()
			.SetApplicationName("Excalibur.Dispatch.Security");

		// Register encryption service
		services.TryAddSingleton<IMessageEncryptionService, DataProtectionMessageEncryptionService>();

		// Register middleware
		services.TryAddTransient<IDispatchMiddleware, MessageEncryptionMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds message signing services to the DI container.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section containing signing settings.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for signing settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddMessageSigning(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SigningOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return services.AddMessageSigning();
	}

	/// <summary>
	/// Adds message signing services with custom configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration delegate for signing options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMessageSigning(
		this IServiceCollection services,
		Action<SigningOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options if provided
		var signingOptionsBuilder = services.AddOptions<SigningOptions>();
		if (configureOptions != null)
		{
			_ = signingOptionsBuilder.Configure(configureOptions);
		}

		_ = signingOptionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// IKeyProvider is registered by cloud-specific packages (Excalibur.Dispatch.Security.Azure, Excalibur.Dispatch.Security.Aws)
		// or a custom implementation for local development scenarios.

		// Register signing service
		services.TryAddSingleton<IMessageSigningService, HmacMessageSigningService>();

		// Register middleware
		services.TryAddTransient<IDispatchMiddleware, MessageSigningMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds rate limiting services to the DI container.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section containing rate limiting settings.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresDynamicCode(
		"Configuration binding for rate limiting settings requires dynamic code generation for property reflection and value conversion.")]
	[RequiresUnreferencedCode("Configuration binding for rate limiting may reference types that could be trimmed")]
	public static IServiceCollection AddRateLimiting(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<RateLimitingOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return services.AddRateLimiting();
	}

	/// <summary>
	/// Adds rate limiting services with custom configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration delegate for rate limiting options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRateLimiting(
		this IServiceCollection services,
		Action<RateLimitingOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options if provided
		var rateLimitOptionsBuilder = services.AddOptions<RateLimitingOptions>();
		if (configureOptions != null)
		{
			_ = rateLimitOptionsBuilder.Configure(configureOptions);
		}

		_ = rateLimitOptionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register middleware as singleton for shared rate limiters
		services.TryAddSingleton<RateLimitingMiddleware>();
		services.TryAddTransient<IDispatchMiddleware>(static sp => sp.GetRequiredService<RateLimitingMiddleware>());

		return services;
	}

	/// <summary>
	/// Adds JWT authentication services to the DI container.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section containing authentication settings.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresDynamicCode(
		"Configuration binding for JWT authentication settings requires dynamic code generation for property reflection and value conversion.")]
	[RequiresUnreferencedCode("Configuration binding for JWT authentication may reference types that could be trimmed")]
	public static IServiceCollection AddJwtAuthentication(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<JwtAuthenticationOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return services.AddJwtAuthentication();
	}

	/// <summary>
	/// Adds JWT authentication services with custom configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration delegate for authentication options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddJwtAuthentication(
		this IServiceCollection services,
		Action<JwtAuthenticationOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options if provided
		var jwtOptionsBuilder = services.AddOptions<JwtAuthenticationOptions>();
		if (configureOptions != null)
		{
			_ = jwtOptionsBuilder.Configure(configureOptions);
		}

		_ = jwtOptionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register cross-property validator (TryAddEnumerable to coexist with DataAnnotation validators)
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JwtAuthenticationOptions>, JwtAuthenticationOptionsValidator>());

		// Register middleware
		services.TryAddTransient<IDispatchMiddleware, JwtAuthenticationMiddleware>();

		return services;
	}
}
