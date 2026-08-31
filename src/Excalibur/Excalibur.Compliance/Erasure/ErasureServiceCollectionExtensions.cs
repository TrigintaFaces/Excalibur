// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Erasure.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring GDPR erasure services.
/// </summary>
public static class ErasureServiceCollectionExtensions
{
	/// <summary>
	/// Adds GDPR erasure services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGdprErasure(
		this IServiceCollection services,
		Action<ErasureOptions>? configureOptions = null)
	{
		// Configure options
		var optionsBuilder = services.AddOptions<ErasureOptions>();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		// Validate options on startup
		_ = optionsBuilder
			.ValidateOnStart();

		RegisterGdprErasureCore(services);

		return services;
	}

	/// <summary>
	/// Adds GDPR erasure services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="ErasureOptions"/>. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGdprErasure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ErasureOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		RegisterGdprErasureCore(services);

		return services;
	}

	/// <summary>
	/// Adds the in-memory erasure store for development and testing.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks> This store is NOT suitable for production use. Use AddSqlServerErasureStore for production deployments. </remarks>
	public static IServiceCollection AddInMemoryErasureStore(this IServiceCollection services)
	{
		_ = services.AddDataSubjectHashing();
		// The store's constructor REQUIRES the ambient context, and it is registered here by type, so
		// resolution must always succeed. TryAdd keeps a host's own context: the framework default is a
		// single-tenant context, and the multi-tenancy composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();
		services.TryAddSingleton<InMemoryErasureStore>();
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<InMemoryErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<InMemoryErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<InMemoryErasureStore>());
		return services;
	}

	/// <summary>
	/// Adds legal hold services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks> Legal holds support GDPR Article 17(3) exceptions that block erasure when data must be retained for legal reasons. </remarks>
	public static IServiceCollection AddLegalHoldService(this IServiceCollection services)
	{
		_ = services.AddDataSubjectHashing(); // LegalHoldService requires IDataSubjectHasher (B3 — standalone path).
		services.TryAddScoped<ILegalHoldService, LegalHoldService>();
		return services;
	}

	/// <summary>
	/// Adds the in-memory legal hold store for development and testing.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks> This store is NOT suitable for production use. Use AddSqlServerLegalHoldStore for production deployments. </remarks>
	public static IServiceCollection AddInMemoryLegalHoldStore(this IServiceCollection services)
	{
		// The store's constructor REQUIRES the ambient context, and it is registered here by type, so
		// resolution must always succeed. TryAdd keeps a host's own context: the framework default is a
		// single-tenant context, and the multi-tenancy composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();
		services.TryAddSingleton<InMemoryLegalHoldStore>();
		services.TryAddSingleton<ILegalHoldStore>(sp => sp.GetRequiredService<InMemoryLegalHoldStore>());
		services.TryAddSingleton<ILegalHoldQueryStore>(sp => sp.GetRequiredService<InMemoryLegalHoldStore>());
		return services;
	}

	/// <summary>
	/// Adds data inventory services for discovering personal data locations.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks> Data inventory supports automatic discovery from [PersonalData] attributes and manual registration for GDPR RoPA compliance. </remarks>
	public static IServiceCollection AddDataInventoryService(this IServiceCollection services)
	{
		_ = services.AddDataSubjectHashing(); // DataInventoryService requires IDataSubjectHasher (B3 — standalone path).
		services.TryAddScoped<IDataInventoryService, DataInventoryService>();
		return services;
	}

	/// <summary>
	/// Adds the in-memory data inventory store for development and testing.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks> This store is NOT suitable for production use. Use AddSqlServerDataInventoryStore for production deployments. </remarks>
	public static IServiceCollection AddInMemoryDataInventoryStore(this IServiceCollection services)
	{
		// The store's constructor REQUIRES both the ambient context and the tenant-context options, and it is
		// registered here by type, so both must resolve. AddDefaultTenantContext registers the single-tenant
		// default context and the TenantContextOptions binding; TryAdd keeps a host's own context, which the
		// multi-tenancy composition replaces with the resolver-driven one.
		_ = services.AddDefaultTenantContext();
		services.TryAddSingleton<InMemoryDataInventoryStore>();
		services.TryAddSingleton<IDataInventoryStore>(sp => sp.GetRequiredService<InMemoryDataInventoryStore>());
		services.TryAddSingleton<IDataInventoryQueryStore>(sp => sp.GetRequiredService<InMemoryDataInventoryStore>());
		return services;
	}

	/// <summary>
	/// Adds the erasure verification service for defense-in-depth verification.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>Verification uses multiple methods to confirm erasure:</para>
	/// <list type="bullet">
	/// <item>
	/// <description> KMS key deletion confirmation </description>
	/// </item>
	/// <item>
	/// <description> Audit log verification </description>
	/// </item>
	/// <item>
	/// <description> Decryption failure testing </description>
	/// </item>
	/// </list>
	/// <para>
	/// Requires <see cref="IErasureStore" /> and <see cref="IDataInventoryService" /> to
	/// be registered.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddErasureVerificationService(this IServiceCollection services)
	{
		// Verification reads the data inventory to decide what should have been erased, so it cannot be
		// composed without the inventory service this package also owns. TryAdd throughout, so a consumer's
		// own inventory service still wins.
		_ = services.AddDataInventoryService();
		services.TryAddScoped<IErasureVerificationService, ErasureVerificationService>();
		return services;
	}

	/// <summary>
	/// Adds the erasure scheduler background service for automatic execution of scheduled erasures.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration action for scheduler options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>This service:</para>
	/// <list type="bullet">
	/// <item>
	/// <description> Polls for erasure requests past their scheduled execution time </description>
	/// </item>
	/// <item>
	/// <description> Executes erasures via <see cref="IErasureService" /> </description>
	/// </item>
	/// <item>
	/// <description> Handles retry logic with exponential backoff for failed erasures </description>
	/// </item>
	/// <item>
	/// <description> Cleans up expired certificates past their retention period </description>
	/// </item>
	/// </list>
	/// <para> Requires <see cref="IErasureStore" /> and <see cref="IErasureService" /> to be registered. </para>
	/// </remarks>
	public static IServiceCollection AddErasureScheduler(
		this IServiceCollection services,
		Action<ErasureSchedulerOptions>? configureOptions = null)
	{
		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}
		else
		{
			services.TryAddSingleton(Options.Options.Create(new ErasureSchedulerOptions()));
		}

		if (!services.Any(sd => sd.ServiceType == typeof(ErasureSchedulerBackgroundService)))
		{
			_ = services.AddSingleton<ErasureSchedulerBackgroundService>();
			_ = services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ErasureSchedulerBackgroundService>());
		}

		return services;
	}

	/// <summary>
	/// Adds the erasure scheduler background service using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="ErasureSchedulerOptions"/>. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddErasureScheduler(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ErasureSchedulerOptions>().Bind(configuration).ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ErasureSchedulerOptions>, ErasureSchedulerOptionsValidator>());

		if (!services.Any(sd => sd.ServiceType == typeof(ErasureSchedulerBackgroundService)))
		{
			_ = services.AddSingleton<ErasureSchedulerBackgroundService>();
			_ = services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ErasureSchedulerBackgroundService>());
		}

		return services;
	}

	/// <summary>
	/// Adds the legal hold expiration background service for automatic release of expired holds.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration action for expiration options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>This service periodically checks for holds past their expiration date and auto-releases them.</para>
	/// <para> Requires <see cref="ILegalHoldStore" /> to be registered. </para>
	/// </remarks>
	public static IServiceCollection AddLegalHoldExpiration(
		this IServiceCollection services,
		Action<LegalHoldExpirationOptions>? configureOptions = null)
	{
		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}
		else
		{
			services.TryAddSingleton(Options.Options.Create(new LegalHoldExpirationOptions()));
		}

		if (!services.Any(sd => sd.ServiceType == typeof(LegalHoldExpirationService)))
		{
			_ = services.AddSingleton<LegalHoldExpirationService>();
			_ = services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<LegalHoldExpirationService>());
		}

		return services;
	}

	/// <summary>
	/// Adds the legal hold expiration background service using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="LegalHoldExpirationOptions"/>. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddLegalHoldExpiration(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<LegalHoldExpirationOptions>().Bind(configuration).ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<LegalHoldExpirationOptions>, LegalHoldExpirationOptionsValidator>());

		if (!services.Any(sd => sd.ServiceType == typeof(LegalHoldExpirationService)))
		{
			_ = services.AddSingleton<LegalHoldExpirationService>();
			_ = services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<LegalHoldExpirationService>());
		}

		return services;
	}

	/// <summary>
	/// Configures GDPR erasure with options from a configuration section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Configuration action to bind options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Usage:
	/// <code>
	///services.AddGdprErasureFromConfiguration(options =&gt;
	///configuration.GetSection("Compliance:Erasure").Bind(options));
	/// </code>
	/// </remarks>
	public static IServiceCollection AddGdprErasureFromConfiguration(
		this IServiceCollection services,
		Action<ErasureOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<ErasureOptions>()
			.Configure(configure)
			.ValidateOnStart();

		RegisterGdprErasureCore(services);

		return services;
	}

	// Keyed data-subject pseudonymization. The pepper is validated fail-closed on start so a
	// misconfigured deployment cannot silently pseudonymize identifiers with an unkeyed hash. Registered as a
	// singleton so every erasure/legal-hold/data-inventory consumer hashes a given identifier to the same
	// token. Consumers supply the pepper via Configure<DataSubjectHashingOptions> from a secret manager.
	/// <summary>
	/// Registers the keyed data-subject hasher (<see cref="IDataSubjectHasher"/>) and its fail-closed options
	/// validation. Idempotent (<c>TryAdd</c>) — safe to call from every registration path that resolves a
	/// service or store which pseudonymizes data-subject identifiers (erasure, legal hold, data inventory), so
	/// a standalone legal-hold-only or inventory-only wiring still resolves the hasher.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Consumers supply the secret pepper via <c>Configure&lt;DataSubjectHashingOptions&gt;</c> from a secret
	/// manager / KMS; a missing/weak pepper fails closed at startup.
	/// </remarks>
	public static IServiceCollection AddDataSubjectHashing(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<DataSubjectHashingOptions>().ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DataSubjectHashingOptions>, DataSubjectHashingOptionsValidator>());
		services.TryAddSingleton<IDataSubjectHasher, HmacDataSubjectHasher>();
		return services;
	}

	private static void RegisterGdprErasureCore(IServiceCollection services)
	{
		// Register cross-property validator (TryAddEnumerable to coexist with DataAnnotation validators)
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ErasureOptions>, ErasureOptionsValidator>());

		_ = services.AddDataSubjectHashing();

		// TryAdd default IKeyManagementAdmin so AddGdprErasure works against an
		// otherwise-empty IServiceCollection. The in-memory provider is suitable
		// for development and tests; production deployments register a durable
		// provider (Azure Key Vault, AWS KMS, etc.) which wins via TryAdd
		// precedence.
		// See ComplianceEncryptionBuilder: volatile keys must be an explicit choice, never a silent default.
		services.TryAddSingleton<InMemoryKeyManagementProvider>();
		services.TryAddSingleton<IKeyManagementAdmin>(static sp =>
			sp.GetRequiredService<InMemoryKeyManagementProvider>());
		services.TryAddSingleton<IKeyManagementProvider>(static sp =>
			sp.GetRequiredService<InMemoryKeyManagementProvider>());

		// Register core service with factory to resolve optional dependencies
		services.TryAddScoped<ErasureService>(sp => new ErasureService(
			sp.GetRequiredService<IErasureStore>(),
			sp.GetRequiredService<IKeyManagementAdmin>(),
			sp.GetRequiredService<IOptions<ErasureOptions>>(),
			sp.GetRequiredService<ILogger<ErasureService>>(),
			sp.GetRequiredService<IDataSubjectHasher>(),
			sp.GetService<ILegalHoldService>(),
			sp.GetService<IDataInventoryService>(),
			sp.GetServices<IErasureContributor>()));

		services.TryAddScoped<IErasureService>(static sp => sp.GetRequiredService<ErasureService>());
		services.TryAddScoped<IErasureExecutor>(static sp => sp.GetRequiredService<ErasureService>());

		// startup fail-fast: reject an erasure registration that has no data-inventory discovery
		// source AND no key-shred-only opt-in, so a completion certificate is never issued over unverified
		// coverage (marker-inseparable-from-wiring). Defense-in-depth with the runtime affirmative-coverage
		// gate in ErasureService.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ErasureDiscoverySourceValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, ErasureDiscoverySourceValidator>());

		// GDPR crypto-shred is a key-durability-REQUIRING composition: erasure works by destroying the
		// encryption key, so a volatile key provider makes the guarantee meaningless (the keys are gone on
		// restart regardless, and a "shred" over lost keys cannot be attested). Install the startup gate so
		// a volatile provider FAILS CLOSED unless the host opted in (AllowVolatileKeyProvider = true) — the
		// gate resolves the provider that actually won the TryAdd above (a registered durable provider wins).
		services.AddKeyDurabilityGate();
	}
}
