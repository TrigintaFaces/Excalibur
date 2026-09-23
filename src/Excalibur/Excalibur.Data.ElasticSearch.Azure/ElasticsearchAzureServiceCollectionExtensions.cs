// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Azure Key Vault as the Elasticsearch connection-credential store.
/// </summary>
/// <remarks>
/// This extension shipped from <c>Excalibur.Data.ElasticSearch</c> and now lives in its own package so
/// that the base package does not put the Azure SDK on consumers who never call it. The namespace and
/// method signature are unchanged, so adopting the split is a project-file edit and not a source edit.
/// </remarks>
public static class ElasticsearchAzureServiceCollectionExtensions
{
	/// <summary>
	/// Configures Azure Key Vault as the connection-credential store: OAuth tokens, service-account secrets,
	/// passwords, and API keys used to authenticate to Elasticsearch itself.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Call this BEFORE <c>AddAuthentication</c> (or <c>AddElasticsearchSecurity</c>, which calls it) so the
	/// registration below wins over the in-memory development default via <c>TryAdd</c>.
	/// </para>
	/// <para>
	/// <b>This is a connection-credential store, not encryption-key custody.</b> It returns opaque secret
	/// strings and never key material for cryptographic operations. That boundary is the reason this
	/// provider exists separately from <c>Excalibur.Security.Azure</c>'s byte[]-valued
	/// <c>IKeyProvider</c> and from <c>Excalibur.Compliance.Azure</c>'s key-custody provider: routing
	/// Elasticsearch at either of those would hand this package key material. Field-level encryption keys
	/// are resolved through <c>Excalibur.Compliance.IKeyManagementProvider</c> instead — see
	/// <c>AddKeyManagement</c> in the base package.
	/// </para>
	/// </remarks>
	/// <param name="services"> The service collection to add the credential store to. </param>
	/// <param name="configuration"> The configuration to bind Azure Key Vault settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddAzureKeyVaultCredentialStorage(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Nested under the same parent section AddKeyManagement reads its own settings from, so that a
		// consumer who puts their key-management configuration in one place has all of it bound.
		_ = services.AddOptions<AzureKeyVaultOptions>()
			.Bind(configuration.GetSection("Elasticsearch:Security:Encryption:KeyManagement:AzureKeyVault"))
			.ValidateOnStart();

		services.TryAddSingleton<IElasticsearchKeyStorage, AzureKeyVaultProvider>();

		return services;
	}
}
