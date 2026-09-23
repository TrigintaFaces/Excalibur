// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Data.Tests.ElasticSearch.Security.KeyManagement;

/// <summary>
/// Binds the guarantee that selecting a cloud key-management provider for Elasticsearch field encryption
/// never silently substitutes the in-process development provider. This package no longer implements
/// cloud key custody itself (6zlc1i converged field-encryption key custody onto
/// <see cref="IKeyManagementProvider"/> / <see cref="IEncryptionProviderRegistry"/>, the abstractions the
/// dedicated Excalibur.Compliance.Azure/.Aws/.Vault packages implement) -- so the guard here is that
/// <see cref="AddKeyManagement"/> refuses to proceed on a cloud selection unless a consumer already
/// registered a real provider for those two interfaces.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class CloudKeyProviderRegistrationShould
{
	private static IConfiguration ConfigurationFor(string? provider)
	{
		var settings = new Dictionary<string, string?>();
		if (provider is not null)
		{
			settings["Elasticsearch:Security:Encryption:KeyManagement:Provider"] = provider;
		}

		return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
	}

	// SAFETY, on the path a real consumer actually takes: selecting a cloud provider through
	// configuration must not quietly land on the development provider either. This is the scenario
	// the defect described -- an operator sets AzureKeyVault in appsettings and believes keys are in a
	// managed key service.
	[Theory]
	[InlineData("AzureKeyVault", "AzureKeyVault")]
	[InlineData("AwsKms", "AwsKms")]
	[InlineData("GoogleCloudKms", "GoogleCloudKms")]
	[InlineData("HashiCorpVault", "HashiCorpVault")]
	public void RefuseACloudProviderSelectedThroughConfiguration_WhenNoRealProviderIsRegistered(
		string configuredProvider, string expectedNameFragment)
	{
		var services = new ServiceCollection();

		var ex = Should.Throw<NotSupportedException>(
			() => services.AddKeyManagement(ConfigurationFor(configuredProvider)));

		ex.Message.ShouldContain(expectedNameFragment);
		services.ShouldNotContain(d => d.ServiceType == typeof(IKeyManagementProvider));
	}

	// LIVENESS: a consumer that already registered a real IKeyManagementProvider + IEncryptionProviderRegistry
	// (the shape Excalibur.Compliance.Azure/.Aws/.Vault's own DI extensions produce) is NOT blocked. Without
	// this arm the safety arm above would be satisfied by an entry point that refuses unconditionally.
	[Theory]
	[InlineData("AzureKeyVault")]
	[InlineData("AwsKms")]
	[InlineData("GoogleCloudKms")]
	[InlineData("HashiCorpVault")]
	public void HonourAConsumerSuppliedKeyManagementProviderInsteadOfRefusing(string configuredProvider)
	{
		var services = new ServiceCollection();
		var consumerProvider = A.Fake<IKeyManagementProvider>();
		var consumerRegistry = A.Fake<IEncryptionProviderRegistry>();
		services.TryAddSingleton(consumerProvider);
		services.TryAddSingleton(consumerRegistry);

		_ = services.AddKeyManagement(ConfigurationFor(configuredProvider));

		using var sp = services.BuildServiceProvider();
		sp.GetRequiredService<IKeyManagementProvider>().ShouldBeSameAs(consumerProvider);
		sp.GetRequiredService<IEncryptionProviderRegistry>().ShouldBeSameAs(consumerRegistry);
	}

	// LIVENESS: configuration that selects the local provider (or omits the setting) still works, so
	// the guard above is not simply refusing every configured composition.
	[Theory]
	[InlineData("Local")]
	[InlineData(null)]
	public void StillHonourConfigurationThatSelectsTheDevelopmentProvider(string? configuredProvider)
	{
		var services = new ServiceCollection();

		_ = services.AddKeyManagement(ConfigurationFor(configuredProvider));

		services.ShouldContain(d => d.ServiceType == typeof(IKeyManagementProvider));
		services.ShouldContain(d => d.ServiceType == typeof(IEncryptionProviderRegistry));
	}
}
