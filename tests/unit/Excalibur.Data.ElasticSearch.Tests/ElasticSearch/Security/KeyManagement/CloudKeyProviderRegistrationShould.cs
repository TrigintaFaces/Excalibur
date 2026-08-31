// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Data.Tests.ElasticSearch.Security.KeyManagement;

/// <summary>
/// Binds the guarantee that a cloud key-management entry point never silently substitutes the
/// in-process development key provider for a requested cloud key service.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class CloudKeyProviderRegistrationShould
{
	// Rows carry the provider NAME, not the registration delegate: a delegate is not serializable, so
	// a theory keyed on one collapses to a single unnamed row in the runner instead of one row per
	// provider -- and a provider that stops being covered would not be visible in the results.
	private static readonly Dictionary<string, Func<IServiceCollection, IConfiguration, IServiceCollection>> Registrations =
		new(StringComparer.Ordinal)
		{
			["AWS KMS"] = static (s, c) => s.AddAwsKms(c),
			["Google Cloud KMS"] = static (s, c) => s.AddGoogleCloudKms(c),
			["HashiCorp Vault"] = static (s, c) => s.AddHashiCorpVault(c),
		};

	public static TheoryData<string> UnimplementedProviders => [.. Registrations.Keys];

	private static IConfiguration EmptyConfiguration() =>
		new ConfigurationBuilder().AddInMemoryCollection([]).Build();

	// SAFETY: the requested cloud provider is refused outright, and nothing is bound behind the
	// caller's back. RED against a stub that quietly registers the development provider.
	[Theory]
	[MemberData(nameof(UnimplementedProviders))]
	public void RefuseAnUnimplementedCloudProviderRatherThanSubstituteTheDevelopmentProvider(string providerName)
	{
		var register = Registrations[providerName];
		var services = new ServiceCollection();

		var ex = Should.Throw<NotSupportedException>(() => register(services, EmptyConfiguration()));

		ex.Message.ShouldContain(providerName);
		services.ShouldNotContain(d => d.ServiceType == typeof(IElasticsearchKeyProvider));
	}

	// LIVENESS: a consumer supplying its own real provider is NOT blocked. Without this arm the
	// safety arm above would be satisfied by an entry point that refuses unconditionally.
	[Theory]
	[MemberData(nameof(UnimplementedProviders))]
	public void HonourAConsumerSuppliedKeyProviderInsteadOfRefusing(string providerName)
	{
		var register = Registrations[providerName];
		var consumerProvider = A.Fake<IElasticsearchKeyProvider>();
		var services = new ServiceCollection();
		services.TryAddSingleton(consumerProvider);

		_ = register(services, EmptyConfiguration());

		using var sp = services.BuildServiceProvider();
		sp.GetRequiredService<IElasticsearchKeyProvider>().ShouldBeSameAs(consumerProvider);
	}

	// SAFETY, on the path a real consumer actually takes: selecting a cloud provider through
	// configuration must not quietly land on the development provider either. This is the scenario
	// the defect described -- an operator sets AwsKms in appsettings and believes keys are in a
	// managed key service.
	[Theory]
	[InlineData("AwsKms", "AWS KMS")]
	[InlineData("GoogleCloudKms", "Google Cloud KMS")]
	[InlineData("HashiCorpVault", "HashiCorp Vault")]
	public void RefuseACloudProviderSelectedThroughConfiguration(string configuredProvider, string expectedName)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Elasticsearch:Security:Encryption:KeyManagement:Provider"] = configuredProvider,
			})
			.Build();
		var services = new ServiceCollection();

		var ex = Should.Throw<NotSupportedException>(() => services.AddKeyManagement(configuration));

		ex.Message.ShouldContain(expectedName);
		services.ShouldNotContain(d => d.ServiceType == typeof(IElasticsearchKeyProvider));
	}

	// LIVENESS: configuration that selects the local provider (or omits the setting) still works, so
	// the guard above is not simply refusing every configured composition.
	[Theory]
	[InlineData("Local")]
	[InlineData(null)]
	public void StillHonourConfigurationThatSelectsTheDevelopmentProvider(string? configuredProvider)
	{
		var settings = new Dictionary<string, string?>();
		if (configuredProvider is not null)
		{
			settings["Elasticsearch:Security:Encryption:KeyManagement:Provider"] = configuredProvider;
		}

		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
		var services = new ServiceCollection();

		_ = services.AddKeyManagement(configuration);

		services.ShouldContain(d => d.ServiceType == typeof(IElasticsearchKeyProvider));
	}

	// LIVENESS: the development provider remains reachable when it is asked for BY NAME.
	[Fact]
	public void StillProvideTheDevelopmentProviderWhenItIsRequestedExplicitly()
	{
		var services = new ServiceCollection();

		_ = services.AddLocalKeyProvider();

		using var sp = services.BuildServiceProvider();
		var keyProvider = sp.GetRequiredService<IElasticsearchKeyProvider>();

		keyProvider.ProviderType.ShouldBe(KeyManagementProviderType.Local);
		keyProvider.SupportsHsm.ShouldBeFalse();
	}
}
