// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security;

/// <summary>
/// A security surface must not accept a value it cannot honour. Three shapes of that lie are locked
/// here: a service registered with no behaviour behind it, an authentication type the provider has no
/// arm for, and a key type no shipped provider can generate. Each one leaves a consumer believing a
/// control is in place when nothing is.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecuritySurfaceHonestyShould
{
	/// <summary>
	/// A body-less type (no fields, no properties, no methods of its own) registered as a security
	/// service hands the consumer an object that enforces nothing. Such a type must not exist in the
	/// security namespace at all.
	/// </summary>
	[Fact]
	public void NotDeclareASecurityTypeWithNoBehaviour()
	{
		const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
			| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

		var hollow = typeof(SecurityMonitor).Assembly
			.GetTypes()
			.Where(static t => t.IsClass && !t.IsAbstract && !t.IsNested)
			.Where(static t => t.Namespace?.StartsWith("Excalibur.Data.ElasticSearch.Security", StringComparison.Ordinal) == true)
			.Where(static t => t.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
			.Where(static t => t.GetFields(Declared).Length == 0
				&& t.GetProperties(Declared).Length == 0
				&& t.GetEvents(Declared).Length == 0
				&& t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Length == 0
				&& t.GetMethods(Declared).Length == 0)
			.Select(static t => t.FullName!)
			.OrderBy(static n => n, StringComparer.Ordinal)
			.ToList();

		hollow.ShouldBeEmpty(
			"a type with no members of its own cannot enforce anything; registering one as a security "
			+ "service tells the consumer a control exists when none does");
	}

	/// <summary>
	/// Every value of <see cref="ElasticsearchAuthenticationType"/> must be one the shipped provider can
	/// actually produce a credential for. A value with no arm in the provider's switch is selectable and
	/// then fails as a missing credential rather than as an unsupported method.
	/// </summary>
	[Fact]
	public void OfferOnlyAuthenticationTypesTheProviderHandles()
	{
		// The arms present in SecureElasticsearchAuthenticationProvider.GetAuthenticationAsync.
		ElasticsearchAuthenticationType[] handled =
		[
			ElasticsearchAuthenticationType.None,
			ElasticsearchAuthenticationType.BasicAuthentication,
			ElasticsearchAuthenticationType.ApiKey,
			ElasticsearchAuthenticationType.Base64ApiKey,
			ElasticsearchAuthenticationType.OAuth2,
			ElasticsearchAuthenticationType.ServiceAccount,
		];

		Enum.GetValues<ElasticsearchAuthenticationType>()
			.Except(handled)
			.ShouldBeEmpty("an authentication type the provider has no arm for is selectable and cannot work");
	}

	/// <summary>
	/// Every value of <see cref="EncryptionKeyType"/> must be generatable by the shipped key providers.
	/// The key-management contract stores raw symmetric key material as a secret and exposes no
	/// public-key retrieval or asymmetric operation, so an asymmetric member could not be honoured.
	/// </summary>
	[Fact]
	public void OfferOnlyKeyTypesTheProvidersCanGenerate()
	{
		// The arms present in AzureKeyVaultProvider.GenerateEncryptionKeyAsync.
		EncryptionKeyType[] generatable = [EncryptionKeyType.Aes, EncryptionKeyType.Hmac];

		Enum.GetValues<EncryptionKeyType>()
			.Except(generatable)
			.ShouldBeEmpty("a key type no shipped provider can generate always fails at runtime");
	}
}
