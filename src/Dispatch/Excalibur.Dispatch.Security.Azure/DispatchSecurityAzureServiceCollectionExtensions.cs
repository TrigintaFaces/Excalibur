// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Azure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure security services in the dependency injection container.
/// </summary>
public static class DispatchSecurityAzureServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Key Vault credential store services.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration containing Azure Key Vault settings.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires the following configuration:
	/// <code>
	/// {
	///   "AzureKeyVault": {
	///     "VaultUri": "https://your-vault.vault.azure.net/",
	///     "KeyPrefix": "dispatch-" // Optional, defaults to "dispatch-"
	///   }
	/// }
	/// </code>
	/// </remarks>
	public static IServiceCollection AddAzureKeyVaultCredentialStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var vaultUri = configuration["AzureKeyVault:VaultUri"];
		if (!string.IsNullOrEmpty(vaultUri))
		{
			_ = services.AddSingleton<ICredentialStore, AzureKeyVaultCredentialStore>();
			_ = services.AddSingleton<IWritableCredentialStore, AzureKeyVaultCredentialStore>();
		}

		return services;
	}

	/// <summary>
	/// Adds Azure Service Bus options validator for security validation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddAzureServiceBusSecurityValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AzureServiceBusOptions>, AzureServiceBusOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds all Azure security services including Key Vault credential store and Service Bus validation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration containing Azure settings.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDispatchSecurityAzure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddAzureKeyVaultCredentialStore(configuration);
		_ = services.AddAzureServiceBusSecurityValidation();

		return services;
	}
}
