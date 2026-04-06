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
	/// Adds security middleware services (encryption, signing, rate limiting, JWT authentication) to the Dispatch pipeline.
	/// For complete security setup including credential management and auditing, use
	/// <c>AddDispatchSecurity</c> from <c>DispatchSecurityServiceCollectionExtensions</c> instead.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Security middleware registration uses reflection for dependency injection and configuration binding")]
	[RequiresDynamicCode("Security middleware registration uses reflection to instantiate and configure middleware components")]
	public static IServiceCollection AddDispatchSecurityMiddleware(
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
	/// Adds security middleware services with custom configuration.
	/// For complete security setup including credential management and auditing, use
	/// <c>AddDispatchSecurity</c> from <c>DispatchSecurityServiceCollectionExtensions</c> instead.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure security options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDispatchSecurityMiddleware(
		this IServiceCollection services,
		Action<SecurityOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var options = new SecurityOptions();
		configureOptions(options);

		// Configure individual components based on options
		if (options.Encryption.EnableEncryption)
		{
			_ = services.AddMessageEncryption(opt =>
			{
				opt.Enabled = true;
				opt.DefaultAlgorithm = options.Encryption.EncryptionAlgorithm;
				opt.AzureKeyVaultUrl = options.Encryption.AzureKeyVaultUrl;
				opt.AwsKmsKeyArn = options.Encryption.AwsKmsKeyArn;
			});
		}

		if (options.Signing.EnableSigning)
		{
			_ = services.AddMessageSigning(opt =>
			{
				opt.Enabled = true;
				opt.DefaultAlgorithm = options.Signing.SigningAlgorithm;
			});
		}

		if (options.RateLimiting.EnableRateLimiting)
		{
			_ = services.AddRateLimiting(opt =>
			{
				opt.Enabled = true;
				opt.Algorithm = options.RateLimiting.RateLimitAlgorithm;
				opt.DefaultLimits = options.RateLimiting.DefaultRateLimits;
			});
		}

		if (options.Authentication.EnableAuthentication)
		{
			_ = services.AddJwtAuthentication(opt =>
			{
				opt.Enabled = true;
				opt.RequireAuthentication = options.Authentication.RequireAuthentication;
				opt.Credentials.ValidIssuer = options.Authentication.JwtIssuer;
				opt.Credentials.ValidAudience = options.Authentication.JwtAudience;
				opt.Credentials.SigningKey = options.Authentication.JwtSigningKey;
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddMessageEncryption(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<EncryptionOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<EncryptionOptions>, EncryptionOptionsValidator>());
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

		_ = optionsBuilder.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<EncryptionOptions>, EncryptionOptionsValidator>());

		// Add DataProtection with Azure Key Vault if configured
		_ = services.AddDataProtection()
			.SetApplicationName("Excalibur.Dispatch.Security");

		// Register encryption service
		services.TryAddSingleton<IMessageEncryptionService, DataProtectionMessageEncryptionService>();

		// Register middleware concrete type for pipeline resolution
		services.TryAddTransient<MessageEncryptionMiddleware>();

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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddMessageSigning(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SigningOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SigningOptions>, SigningOptionsValidator>());
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

		_ = signingOptionsBuilder.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SigningOptions>, SigningOptionsValidator>());

		// IKeyProvider is registered by cloud-specific packages (Excalibur.Dispatch.Security.Azure, Excalibur.Dispatch.Security.Aws)
		// or a custom implementation for local development scenarios.

		// Register signing service
		services.TryAddSingleton<IMessageSigningService, HmacMessageSigningService>();

		// Register middleware
		services.TryAddTransient<MessageSigningMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds asymmetric message signing services with support for HMAC, ECDSA, and Ed25519 algorithms.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This registers a <see cref="CompositeMessageSigningService"/> that delegates to algorithm-specific
	/// <see cref="ISignatureAlgorithmProvider"/> instances. All standard providers (HMAC, ECDSA, Ed25519)
	/// are registered via <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>.
	/// </para>
	/// <para>
	/// Use this instead of <see cref="AddMessageSigning(IServiceCollection, Action{SigningOptions}?)"/>
	/// when asymmetric (non-repudiation) signing is required. The existing <c>AddMessageSigning</c> method
	/// remains available for HMAC-only scenarios.
	/// </para>
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration delegate for signing options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddAsymmetricSigning(
		this IServiceCollection services,
		Action<SigningOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options if provided
		var optionsBuilder = services.AddOptions<SigningOptions>();
		if (configureOptions != null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SigningOptions>, SigningOptionsValidator>());

		// Register all algorithm providers via TryAddEnumerable (coexist, don't duplicate)
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ISignatureAlgorithmProvider, HmacSignatureAlgorithmProvider>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ISignatureAlgorithmProvider, EcdsaSignatureAlgorithmProvider>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ISignatureAlgorithmProvider, Ed25519SignatureAlgorithmProvider>());

		// Composite replaces HMAC-only service
		services.AddSingleton<IMessageSigningService, CompositeMessageSigningService>();

		// Register middleware (same as AddMessageSigning)
		services.TryAddTransient<MessageSigningMiddleware>();

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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddRateLimiting(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<RateLimitingOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>());
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

		_ = rateLimitOptionsBuilder.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>());

		// Register middleware as singleton for shared rate limiters
		services.TryAddSingleton<RateLimitingMiddleware>();

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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddJwtAuthentication(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<JwtAuthenticationOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<JwtAuthenticationOptions>, JwtAuthenticationOptionsValidator>());
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

		_ = jwtOptionsBuilder.ValidateOnStart();

		// Register cross-property validator
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JwtAuthenticationOptions>, JwtAuthenticationOptionsValidator>());

		// Register middleware
		services.TryAddTransient<JwtAuthenticationMiddleware>();

		return services;
	}
}
