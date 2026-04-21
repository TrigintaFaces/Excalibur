// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security.Azure;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure security services in the dependency injection container.
/// </summary>
public static class DispatchSecurityAzureServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure security services (Key Vault credential store, Service Bus validation) to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Azure security builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddDispatchSecurityAzure(azure =&gt;
	/// {
	///     azure.VaultUri("https://my-vault.vault.azure.net/")
	///          .EnableServiceBusValidation();
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddDispatchSecurityAzure(
		this IServiceCollection services,
		Action<ISecurityAzureBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new SecurityAzureBuilder();
		configure(builder);

		if (!string.IsNullOrEmpty(builder.VaultUri))
		{
			_ = services.AddSingleton<ICredentialStore, AzureKeyVaultCredentialStore>();
			_ = services.AddSingleton<IWritableCredentialStore, AzureKeyVaultCredentialStore>();
		}

		if (builder.ServiceBusValidationEnabled)
		{
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IValidateOptions<AzureServiceBusOptions>, AzureServiceBusOptionsValidator>());
		}

		return services;
	}
}
