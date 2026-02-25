// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

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
			.PostConfigure(options => options.Validate())
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Ensure signing options are available (defaults to empty key for dev/test)
		services.TryAddSingleton(Options.Options.Create(new ErasureSigningOptions()));

		// Register core service with factory to resolve optional dependencies
		services.TryAddScoped<IErasureService>(sp => new ErasureService(
			sp.GetRequiredService<IErasureStore>(),
			sp.GetRequiredService<IKeyManagementProvider>(),
			sp.GetRequiredService<IOptions<ErasureOptions>>(),
			sp.GetRequiredService<IOptions<ErasureSigningOptions>>(),
			sp.GetRequiredService<ILogger<ErasureService>>(),
			sp.GetService<ILegalHoldService>(),
			sp.GetService<IDataInventoryService>(),
			sp.GetServices<IErasureContributor>()));

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
	/// Requires <see cref="IErasureStore" />, <see cref="Encryption.IKeyManagementProvider" />, and <see cref="IDataInventoryService" /> to
	/// be registered.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddErasureVerificationService(this IServiceCollection services)
	{
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

		_ = services.AddSingleton<ErasureSchedulerBackgroundService>();
		_ = services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ErasureSchedulerBackgroundService>());

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

		_ = services.AddSingleton<LegalHoldExpirationService>();
		_ = services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<LegalHoldExpirationService>());

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
			.PostConfigure(options => options.Validate())
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Ensure signing options are available (defaults to empty key for dev/test)
		services.TryAddSingleton(Options.Options.Create(new ErasureSigningOptions()));

		// Register core service with factory to resolve optional dependencies
		services.TryAddScoped<IErasureService>(sp => new ErasureService(
			sp.GetRequiredService<IErasureStore>(),
			sp.GetRequiredService<IKeyManagementProvider>(),
			sp.GetRequiredService<IOptions<ErasureOptions>>(),
			sp.GetRequiredService<IOptions<ErasureSigningOptions>>(),
			sp.GetRequiredService<ILogger<ErasureService>>(),
			sp.GetService<ILegalHoldService>(),
			sp.GetService<IDataInventoryService>(),
			sp.GetServices<IErasureContributor>()));

		return services;
	}
}
