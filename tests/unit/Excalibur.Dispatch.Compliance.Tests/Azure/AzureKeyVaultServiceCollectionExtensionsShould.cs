// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Compliance.Azure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Azure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AzureKeyVaultServiceCollectionExtensionsShould
{
	[Fact]
	public void AddAzureKeyVaultKeyManagement_WithConfigure_RegistersProviderAndOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAzureKeyVaultKeyManagement(options =>
		{
			options.VaultUri = new Uri("https://unit-tests.vault.azure.net/");
			options.KeyNamePrefix = "unit-";
			options.UseSoftwareKeys = true;
		});

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<AzureKeyVaultProvider>().ShouldNotBeNull();
		provider.GetRequiredService<IKeyManagementProvider>().ShouldBeOfType<AzureKeyVaultProvider>();

		var options = provider.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
		options.VaultUri.ShouldBe(new Uri("https://unit-tests.vault.azure.net/"));
		options.KeyNamePrefix.ShouldBe("unit-");
		options.UseSoftwareKeys.ShouldBeTrue();
	}

	[Fact]
	public void AddAzureKeyVaultKeyManagement_WithOptionsInstance_CopiesValues()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var source = new AzureKeyVaultOptions
		{
			VaultUri = new Uri("https://instance.vault.azure.net/"),
			KeyNamePrefix = "instance-",
			UseSoftwareKeys = true,
			MaxRetryAttempts = 9
		};

		_ = services.AddAzureKeyVaultKeyManagement(source);

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
		options.VaultUri.ShouldBe(source.VaultUri);
		options.KeyNamePrefix.ShouldBe(source.KeyNamePrefix);
		options.UseSoftwareKeys.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(9);
	}

	[Fact]
	public void AddAzureKeyVaultKeyManagement_WithConfigurationSection_BindsOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AzureKeyVault:VaultUri"] = "https://config.vault.azure.net/",
				["AzureKeyVault:KeyNamePrefix"] = "cfg-",
				["AzureKeyVault:UseSoftwareKeys"] = "true"
			})
			.Build();

		_ = services.AddAzureKeyVaultKeyManagement(configuration.GetSection("AzureKeyVault"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AzureKeyVaultOptions>>().Value;
		options.VaultUri.ShouldBe(new Uri("https://config.vault.azure.net/"));
		options.KeyNamePrefix.ShouldBe("cfg-");
		options.UseSoftwareKeys.ShouldBeTrue();
	}

	[Fact]
	public void AddAzureKeyVaultRsaKeyWrapping_RegistersWrapperAndOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAzureKeyVaultKeyManagement(options =>
		{
			options.VaultUri = new Uri("https://wrapping.vault.azure.net/");
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
		_ = Should.Throw<ArgumentNullException>(() => services.AddAzureKeyVaultKeyManagement((Action<AzureKeyVaultOptions>)null!));
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
