// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Compliance.Vault;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring HashiCorp Vault key management services.
/// </summary>
public static class VaultServiceCollectionExtensions
{
	/// <summary>
	/// Adds HashiCorp Vault key management services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Vault compliance builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddVaultKeyManagement(vault =&gt;
	/// {
	///     vault.VaultUri(new Uri("https://vault.example.com:8200"))
	///          .TransitMountPath("transit")
	///          .KeyNamePrefix("myapp-");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddVaultKeyManagement(
		this IServiceCollection services,
		Action<IComplianceVaultBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new VaultOptions();
		var builder = new ComplianceVaultBuilder(options);
		configure(builder);

		RegisterOptionsAndServices(services, builder, options);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		ComplianceVaultBuilder builder,
		VaultOptions options)
	{
		_ = services.Configure<VaultOptions>(opt =>
		{
			opt.VaultUri = options.VaultUri;
			opt.TransitMountPath = options.TransitMountPath;
			opt.KeyNamePrefix = options.KeyNamePrefix;
			opt.Namespace = options.Namespace;
			opt.EnableDetailedTelemetry = options.EnableDetailedTelemetry;
		});

		if (builder.BindConfigurationPath is not null)
		{
			_ = services.AddOptions<VaultOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<VaultOptions>().ValidateOnStart();

		RegisterVaultCore(services);
	}

	private static void RegisterVaultCore(IServiceCollection services)
	{
		// Register validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<VaultOptions>, VaultOptionsValidator>());

		// Add memory cache if not already registered
		_ = services.AddMemoryCache();

		// Register the provider
		services.TryAddSingleton<VaultKeyProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<VaultKeyProvider>());
		services.TryAddSingleton<IKeyManagementAdmin>(sp => sp.GetRequiredService<VaultKeyProvider>());
	}
}
