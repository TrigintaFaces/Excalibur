// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Patterns.ClaimCheck;


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring the in-memory claim check provider in an <see cref="IServiceCollection" />.
/// </summary>
public static class InMemoryClaimCheckServiceCollectionExtensions
{
	/// <summary>
	/// Adds the in-memory claim check provider to the service collection.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configureOptions">Optional delegate to configure <see cref="ClaimCheckOptions" />.</param>
	/// <param name="enableCleanup">
	/// Whether to enable automatic cleanup of expired entries via background service.
	/// Default is <c>true</c>. Set to <c>false</c> to disable background cleanup
	/// (useful for testing or when cleanup is managed externally).
	/// </param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the in-memory claim check provider as a singleton implementation of
	/// <see cref="IClaimCheckProvider" />. The provider stores claim check payloads in memory using
	/// a thread-safe concurrent dictionary.
	/// </para>
	/// <para>
	/// <strong>Important:</strong> The in-memory provider is intended for testing and local development
	/// scenarios only. It is NOT recommended for production use with large-scale distributed systems due
	/// to memory constraints and lack of durability across process restarts.
	/// </para>
	/// <para>
	/// For production scenarios, use cloud provider implementations:
	/// </para>
	/// <list type="bullet">
	/// <item>Azure Blob Storage: Excalibur.Dispatch.Patterns.ClaimCheck.Azure</item>
	/// <item>AWS S3: Excalibur.Dispatch.Patterns.ClaimCheck.Aws [planned]</item>
	/// <item>Google Cloud Storage: Excalibur.Dispatch.Patterns.ClaimCheck.Gcp [planned]</item>
	/// </list>
	/// <para>
	/// If <paramref name="enableCleanup" /> is <c>true</c>, a background service will be registered that
	/// periodically scans for and removes expired claim check entries. The cleanup interval is controlled by
	/// <see cref="ClaimCheckOptions.CleanupInterval" /> (default: 1 hour).
	/// </para>
	/// </remarks>
	/// <example>
	/// Basic registration:
	/// <code>
	/// services.AddInMemoryClaimCheck();
	/// </code>
	///
	/// With custom configuration:
	/// <code>
	/// services.AddInMemoryClaimCheck(options =>
	/// {
	///     options.PayloadThreshold = 128 * 1024; // 128KB
	///     options.EnableCompression = true;
	///     options.DefaultTtl = TimeSpan.FromDays(3);
	///     options.CleanupInterval = TimeSpan.FromMinutes(30);
	/// });
	/// </code>
	///
	/// Without background cleanup (for testing):
	/// <code>
	/// services.AddInMemoryClaimCheck(enableCleanup: false);
	/// </code>
	/// </example>
	public static IServiceCollection AddInMemoryClaimCheck(
		this IServiceCollection services,
		Action<ClaimCheckOptions>? configureOptions = null,
		bool enableCleanup = true)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Always configure options (uses defaults if configureOptions is null)
		var optionsBuilder = services.AddOptions<ClaimCheckOptions>()
			.Configure(configureOptions ?? (_ => { }));
		optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register the provider as both its concrete type and the interface
		// Use TryAdd to allow overriding in tests
		services.TryAddSingleton<InMemoryClaimCheckProvider>();
		services.TryAddSingleton<IClaimCheckProvider>(static sp =>
			sp.GetRequiredService<InMemoryClaimCheckProvider>());

		// Register background cleanup service if enabled
		if (enableCleanup)
		{
			_ = services.AddHostedService<InMemoryClaimCheckCleanupService>();
		}

		return services;
	}

	/// <summary>
	/// Adds the in-memory claim check provider to the service collection with configuration binding.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="ClaimCheckOptions" />.</param>
	/// <param name="enableCleanup">
	/// Whether to enable automatic cleanup of expired entries via background service.
	/// Default is <c>true</c>.
	/// </param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This overload binds <see cref="ClaimCheckOptions" /> from the provided configuration section,
	/// allowing declarative configuration via appsettings.json or other configuration sources.
	/// </para>
	/// </remarks>
	/// <example>
	/// Registration with configuration binding:
	/// <code>
	/// // appsettings.json:
	/// // {
	/// //   "ClaimCheck": {
	/// //     "PayloadThreshold": 131072,
	/// //     "EnableCompression": true,
	/// //     "CompressionThreshold": 2048,
	/// //     "DefaultTtl": "3.00:00:00",
	/// //     "CleanupInterval": "00:30:00"
	/// //   }
	/// // }
	///
	/// services.AddInMemoryClaimCheck(configuration.GetSection("ClaimCheck"));
	/// </code>
	/// </example>
	public static IServiceCollection AddInMemoryClaimCheck(
		this IServiceCollection services,
		IConfiguration configuration,
		bool enableCleanup = true)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Bind configuration to options
		services.AddOptions<ClaimCheckOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register provider and cleanup service
		services.TryAddSingleton<InMemoryClaimCheckProvider>();
		services.TryAddSingleton<IClaimCheckProvider>(static sp =>
			sp.GetRequiredService<InMemoryClaimCheckProvider>());

		if (enableCleanup)
		{
			_ = services.AddHostedService<InMemoryClaimCheckCleanupService>();
		}

		return services;
	}
}
