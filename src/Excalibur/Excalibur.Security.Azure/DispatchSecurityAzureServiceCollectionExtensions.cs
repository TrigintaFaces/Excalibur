// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security.Azure;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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

	/// <summary>
	/// Registers an Azure Key Vault-backed <see cref="IKeyProvider"/> for message-signing keys. Key
	/// material is stored as a Key Vault secret (base64-encoded), retrieved keys are cached with a bounded
	/// TTL, and an unknown key fails closed (a <see cref="SigningException"/> is thrown — no key is minted
	/// on the retrieval path). The vault URI is validated at startup via <c>ValidateOnStart</c>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration for the provider options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddAzureKeyVaultKeyProvider(
		this IServiceCollection services,
		Action<AzureKeyVaultKeyProviderOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<AzureKeyVaultKeyProviderOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<
			IValidateOptions<AzureKeyVaultKeyProviderOptions>,
			AzureKeyVaultKeyProviderOptionsValidator>());

		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton<IKeyProvider>(sp => new AzureKeyVaultKeyProvider(
			sp.GetRequiredService<ILogger<AzureKeyVaultKeyProvider>>(),
			sp.GetRequiredService<IOptions<AzureKeyVaultKeyProviderOptions>>(),
			sp.GetRequiredService<TimeProvider>()));

		return services;
	}
}
