// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Compliance;

using Excalibur.Security;
using Excalibur.Security.EventStores;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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
	/// Adds the encryption-version migration service, which re-encrypts data from one encryption
	/// context to another.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// The migration service re-encrypts through <see cref="Excalibur.Compliance.IEncryptionProvider"/>,
	/// which the consumer supplies: register an encryption provider before resolving
	/// <see cref="Excalibur.Compliance.IEncryptionMigrationService"/>. Migration status is tracked in
	/// process, so a status identifier is only readable from the instance that started that migration.
	/// </remarks>
	public static IServiceCollection AddEncryptionMigration(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IEncryptionMigrationService, EncryptionMigrationService>();

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

		// Note: Azure Key Vault and AWS Secrets Manager credential stores live in their own packages:
		// - Excalibur.Security.Azure: services.AddDispatchSecurityAzure(azure => azure.VaultUri(...))
		// - Excalibur.Security.Aws: services.AddDispatchSecurityAws(aws => aws.Region(...))
		// Use those packages for cloud-specific credential management.

		// Add HashiCorp Vault if configured — uses AddSingleton (not TryAdd) because
		// ICredentialStore is a multi-registration: multiple stores coexist.
		// Guard against double-registration when AddSecureCredentialManagement is called twice.
		// Register the concrete type once, then forward both interfaces to the same instance
		// to avoid creating two separate HashiCorpVaultCredentialStore instances.
		var vaultUrl = configuration["Vault:Url"];
		if (!string.IsNullOrEmpty(vaultUrl))
		{
			// Use ServiceType (not ImplementationType) for the dedup guard: the factory registration
			// below has a null ImplementationType, so an ImplementationType check would never match
			// and would re-register on repeated calls.
			if (!services.Any(sd => sd.ServiceType == typeof(HashiCorpVaultCredentialStore)))
			{
				// Provide a managed HttpClient owned (and disposed) by the singleton store. A long-lived
				// client to a single fixed Vault endpoint avoids socket churn without pulling in the
				// Microsoft.Extensions.Http package (zero new dependency).
				_ = services.AddSingleton(sp => new HashiCorpVaultCredentialStore(
					sp.GetRequiredService<ILogger<HashiCorpVaultCredentialStore>>(),
					sp.GetRequiredService<IConfiguration>(),
#pragma warning disable CA2000 // Ownership transferred to the store, which disposes it in Dispose().
					new HttpClient()));
#pragma warning restore CA2000
				_ = services.AddSingleton<ICredentialStore>(sp => sp.GetRequiredService<HashiCorpVaultCredentialStore>());
				_ = services.AddSingleton<IWritableCredentialStore>(sp => sp.GetRequiredService<HashiCorpVaultCredentialStore>());
			}
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

		// Add to the dispatch pipeline. The pipeline discovers middleware by enumerating
		// IEnumerable<IDispatchMiddleware> from DI; registering only the concrete type above
		// leaves the middleware inert. Ordering is by Stage, so registration order is irrelevant.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, InputValidationMiddleware>());

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

		// Safe-by-default PII sanitizer so the security-audit path masks UserId/SourceIp and redacts
		// secret-shaped payloads with zero configuration — never logging raw PII (OWASP A02). TryAdd lets
		// the richer keyed-hash sanitizer (registered by AddObservability) win when present, and lets a
		// consumer explicitly opt into a different sanitizer by registering one BEFORE this call.
		//
		// Opting into RAW (unsanitized) telemetry is an explicit, deliberate choice — never a silent
		// fallback. A consumer who accepts raw PII in telemetry (e.g. a fully-trusted dev environment)
		// registers the no-op sanitizer themselves before calling this method, which wins over the
		// TryAdd masking default:
		//     services.AddSingleton<ITelemetrySanitizer>(NullTelemetrySanitizer.Instance);
		//     services.AddSecurityAuditing(configuration);
		//
		// Construct from options so a consumer can configure a secret pepper (keyed HMAC-SHA-256
		// fingerprinting) without changing the zero-config default — an unset pepper yields the safe-by-default
		// unkeyed SHA-256 fingerprint. AddOptions is idempotent and guarantees IOptions<T> resolves.
		_ = services.AddOptions<MaskingTelemetrySanitizerOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MaskingTelemetrySanitizerOptions>,
			MaskingTelemetrySanitizerOptionsValidator>());
		services.TryAddSingleton<ITelemetrySanitizer>(static sp =>
			new MaskingTelemetrySanitizer(sp.GetRequiredService<IOptions<MaskingTelemetrySanitizerOptions>>()));

		// Register security event logger using forwarding pattern to avoid hard-cast
		services.TryAddSingleton<SecurityEventLogger>();
		services.TryAddSingleton<ISecurityEventLogger>(static sp => sp.GetRequiredService<SecurityEventLogger>());
		_ = services.AddHostedService(static sp => sp.GetRequiredService<SecurityEventLogger>());

		// Register event store based on configuration (TryAdd = idempotent on repeated calls)
		var storeType = configuration["Security:Auditing:StoreType"];
		switch (storeType?.ToUpperInvariant())
		{
			case "SQL":
				// Fail fast: Excalibur.Security ships no SQL-backed ISecurityEventStore. The prior
				// SqlSecurityEventStore placeholder ACCEPTED then silently DISCARDED every audit event
				// (validated, logged a warning, persisted nothing) and returned empty queries — a
				// catastrophic compliance/forensics data-loss landmine. Refuse to register so the
				// silent-discard behavior is unreachable via StoreType=SQL (observable, not a Warning).
				throw new InvalidOperationException(
					"Security:Auditing:StoreType='SQL' selects a SQL-backed audit store, but no SQL " +
					"ISecurityEventStore implementation is available in Excalibur.Security. SQL persistence " +
					"belongs in a dedicated package (e.g. Excalibur.Security.SqlServer), which is not yet shipped. " +
					"Register a SQL audit store, set StoreType to 'Elasticsearch' or 'File', or omit it to use the " +
					"in-memory store for development. Refusing to start to avoid silently discarding audit events.");
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
	/// Adds the composed security middleware to the dispatch pipeline built by
	/// <c>AddDispatch(builder =&gt; ...)</c>.
	/// </summary>
	/// <param name="builder">The dispatch builder instance.</param>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Call this after composing the security features you want. Only the middleware whose feature
	/// has actually been added is placed in the pipeline: a host that composes signing alone gets
	/// signing alone. Composing no security feature adds no middleware.
	/// </para>
	/// <para>
	/// This is required on the builder path. A dispatch pipeline built via
	/// <c>AddDispatch(builder =&gt; ...)</c> is materialized from the middleware registered on the
	/// builder, so middleware present in the service collection but never added here does not run.
	/// A pipeline built without builder configuration instead discovers middleware from the service
	/// collection, and the security features register themselves there as well; calling this method
	/// is harmless in that case and makes the intent explicit.
	/// </para>
	/// <example>
	/// <code>
	/// services.AddDispatchSecurityMiddleware(configuration);
	/// services.AddDispatch(dispatch =&gt; dispatch.UseSecurityMiddleware());
	/// </code>
	/// </example>
	/// </remarks>
	public static IDispatchBuilder UseSecurityMiddleware(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var added = 0;
		added += builder.UseWhenComposed<JwtAuthenticationMiddleware>();
		added += builder.UseWhenComposed<RateLimitingMiddleware>();
		added += builder.UseWhenComposed<MessageEncryptionMiddleware>();
		added += builder.UseWhenComposed<MessageSigningMiddleware>();
		added += builder.UseWhenComposed<InputValidationMiddleware>();

		// Fail loud rather than add nothing. The pipeline is materialized when the enclosing
		// AddDispatch(configure) call returns, so a security feature composed after this point can
		// never reach it. Silently adding nothing would leave the host running with the security it
		// believes it configured entirely absent -- the failure this method exists to prevent.
		return added > 0
			? builder
			: throw new InvalidOperationException(
				"UseSecurityMiddleware() was called but no security feature has been composed, so it " +
				"would add nothing to the pipeline and the host would start with no security " +
				"middleware running. Compose the features first -- AddDispatchSecurityMiddleware(...), " +
				"or the individual AddJwtAuthentication/AddRateLimiting/AddMessageEncryption/" +
				"AddMessageSigning/AddInputValidation calls -- and note that they must be called " +
				"BEFORE AddDispatch(builder => builder.UseSecurityMiddleware()), because the pipeline " +
				"is built when that AddDispatch call returns.");
	}

	/// <summary>
	/// Adds <typeparamref name="TMiddleware" /> to the pipeline when, and only when, its feature has
	/// been composed into the service collection.
	/// </summary>
	/// <typeparam name="TMiddleware">The security middleware type.</typeparam>
	/// <param name="builder">The dispatch builder instance.</param>
	/// <returns>1 when the middleware was added; otherwise 0.</returns>
	private static int UseWhenComposed<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>(
		this IDispatchBuilder builder)
		where TMiddleware : IDispatchMiddleware
	{
		if (!builder.Services.Any(static descriptor => descriptor.ServiceType == typeof(TMiddleware)))
		{
			return 0;
		}

		_ = builder.UseMiddleware<TMiddleware>();
		return 1;
	}
}
