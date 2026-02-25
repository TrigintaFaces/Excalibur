// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Compliance.Vault;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Vault;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class VaultServiceCollectionExtensionsShould
{
	[Fact]
	public void AddVaultKeyManagement_WithConfigure_RegistersProviderAndOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddVaultKeyManagement(options =>
		{
			options.VaultUri = new Uri("http://127.0.0.1:8200");
			options.KeyNamePrefix = "unit-";
			options.Auth.AuthMethod = VaultAuthMethod.Token;
			options.Auth.Token = "unit-token";
		});

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<VaultKeyProvider>().ShouldNotBeNull();
		provider.GetRequiredService<IKeyManagementProvider>().ShouldBeOfType<VaultKeyProvider>();

		var options = provider.GetRequiredService<IOptions<VaultOptions>>().Value;
		options.VaultUri.ShouldBe(new Uri("http://127.0.0.1:8200"));
		options.KeyNamePrefix.ShouldBe("unit-");
		options.Auth.AuthMethod.ShouldBe(VaultAuthMethod.Token);
		options.Auth.Token.ShouldBe("unit-token");
	}

	[Fact]
	public void AddVaultKeyManagement_WithOptionsInstance_CopiesValues()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var source = new VaultOptions
		{
			VaultUri = new Uri("http://127.0.0.1:18200"),
			TransitMountPath = "custom-transit",
			KeyNamePrefix = "custom-",
			Auth =
			{
				AuthMethod = VaultAuthMethod.Token,
				Token = "instance-token"
			}
		};

		_ = services.AddVaultKeyManagement(source);

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<VaultOptions>>().Value;
		options.VaultUri.ShouldBe(source.VaultUri);
		options.TransitMountPath.ShouldBe("custom-transit");
		options.KeyNamePrefix.ShouldBe("custom-");
		options.Auth.Token.ShouldBe("instance-token");
	}

	[Fact]
	public void AddVaultKeyManagement_WithConfigurationSection_BindsOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Vault:VaultUri"] = "http://127.0.0.1:28200",
				["Vault:KeyNamePrefix"] = "cfg-",
				["Vault:Auth:AuthMethod"] = "Token",
				["Vault:Auth:Token"] = "cfg-token"
			})
			.Build();

		_ = services.AddVaultKeyManagement(configuration.GetSection("Vault"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<VaultOptions>>().Value;
		options.VaultUri.ShouldBe(new Uri("http://127.0.0.1:28200"));
		options.KeyNamePrefix.ShouldBe("cfg-");
		options.Auth.AuthMethod.ShouldBe(VaultAuthMethod.Token);
		options.Auth.Token.ShouldBe("cfg-token");
	}

	[Fact]
	public void AddVaultKeyManagement_ThrowsWhenServicesIsNull()
	{
		IServiceCollection? services = null;
		_ = Should.Throw<ArgumentNullException>(() => services!.AddVaultKeyManagement(_ => { }));
	}

	[Fact]
	public void AddVaultKeyManagement_ThrowsWhenConfigureIsNull()
	{
		var services = new ServiceCollection();
		_ = Should.Throw<ArgumentNullException>(() => services.AddVaultKeyManagement((Action<VaultOptions>)null!));
	}

	[Fact]
	public void AddVaultKeyManagement_WithOptions_ThrowsWhenOptionsIsNull()
	{
		var services = new ServiceCollection();
		_ = Should.Throw<ArgumentNullException>(() => services.AddVaultKeyManagement((VaultOptions)null!));
	}

	[Fact]
	public void AddVaultKeyManagement_WithConfigurationSection_ThrowsWhenSectionIsNull()
	{
		var services = new ServiceCollection();
		_ = Should.Throw<ArgumentNullException>(() => services.AddVaultKeyManagement((IConfigurationSection)null!));
	}
}
