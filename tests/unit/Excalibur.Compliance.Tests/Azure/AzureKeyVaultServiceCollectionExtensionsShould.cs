// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Azure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Azure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AzureKeyVaultServiceCollectionExtensionsShould
{
	[Fact]
	public void AddAzureKeyVaultKeyManagement_WithBuilder_RegistersProviderAndOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAzureKeyVaultKeyManagement(azure =>
		{
			azure.VaultUri(new Uri("https://unit-tests.vault.azure.net/"))
				.KeyNamePrefix("unit-");
		});

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<AzureKeyVaultProvider>().ShouldNotBeNull();
		provider.GetRequiredService<IKeyManagementProvider>().ShouldBeOfType<AzureKeyVaultProvider>();

		var options = provider.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
		options.VaultUri.ShouldBe(new Uri("https://unit-tests.vault.azure.net/"));
		options.KeyNamePrefix.ShouldBe("unit-");
	}

	[Fact]
	public void AddAzureKeyVaultKeyManagement_WithBuilder_SetsRequirePremiumTier()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAzureKeyVaultKeyManagement(azure =>
		{
			azure.VaultUri(new Uri("https://premium.vault.azure.net/"))
				.RequirePremiumTier();
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
		options.VaultUri.ShouldBe(new Uri("https://premium.vault.azure.net/"));
		options.RequirePremiumTier.ShouldBeTrue();
	}

	[Fact]
	public void AddAzureKeyVaultKeyManagement_WithBuilder_SetsMetadataCacheDuration()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAzureKeyVaultKeyManagement(azure =>
		{
			azure.VaultUri(new Uri("https://cache.vault.azure.net/"))
				.MetadataCacheDuration(TimeSpan.FromMinutes(10));
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
		options.MetadataCacheDuration.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AddAzureKeyVaultKeyManagement_WithBuilder_SetsEnableDetailedTelemetry()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAzureKeyVaultKeyManagement(azure =>
		{
			azure.VaultUri(new Uri("https://telemetry.vault.azure.net/"))
				.EnableDetailedTelemetry();
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
		options.EnableDetailedTelemetry.ShouldBeTrue();
	}

	[Fact]
	public void AddAzureKeyVaultRsaKeyWrapping_RegistersWrapperAndOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAzureKeyVaultKeyManagement(azure =>
		{
			azure.VaultUri(new Uri("https://wrapping.vault.azure.net/"));
		});

		_ = services.AddAzureKeyVaultRsaKeyWrapping(options =>
		{
			options.KeyVaultUrl = new Uri("https://wrapping.vault.azure.net/");
			options.KeyName = "dispatch-rsa";
			options.Algorithm = RsaWrappingAlgorithm.RsaOaep;
		});

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IAzureRsaKeyWrapper>().ShouldBeOfType<AzureKeyVaultRsaKeyWrapper>();

		var wrappingOptions = provider.GetRequiredService<IOptions<RsaKeyWrappingOptions>>().Value;
		wrappingOptions.KeyVaultUrl.ShouldBe(new Uri("https://wrapping.vault.azure.net/"));
		wrappingOptions.KeyName.ShouldBe("dispatch-rsa");
		wrappingOptions.Algorithm.ShouldBe(RsaWrappingAlgorithm.RsaOaep);
	}

	[Fact]
	public void AddAzureKeyVaultKeyManagement_ThrowsWhenServicesIsNull()
	{
		IServiceCollection? services = null;
		_ = Should.Throw<ArgumentNullException>(() => services!.AddAzureKeyVaultKeyManagement(_ => { }));
	}

	[Fact]
	public void AddAzureKeyVaultKeyManagement_ThrowsWhenConfigureIsNull()
	{
		var services = new ServiceCollection();
		_ = Should.Throw<ArgumentNullException>(() => services.AddAzureKeyVaultKeyManagement((Action<IComplianceAzureBuilder>)null!));
	}

	[Fact]
	public void AddAzureKeyVaultRsaKeyWrapping_ThrowsWhenServicesIsNull()
	{
		IServiceCollection? services = null;
		_ = Should.Throw<ArgumentNullException>(() => services!.AddAzureKeyVaultRsaKeyWrapping(_ => { }));
	}

	[Fact]
	public void AddAzureKeyVaultRsaKeyWrapping_ThrowsWhenConfigureIsNull()
	{
		var services = new ServiceCollection();
		_ = Should.Throw<ArgumentNullException>(() => services.AddAzureKeyVaultRsaKeyWrapping((Action<RsaKeyWrappingOptions>)null!));
	}
}
