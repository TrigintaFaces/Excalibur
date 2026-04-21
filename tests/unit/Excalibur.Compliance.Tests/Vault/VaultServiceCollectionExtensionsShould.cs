// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Vault;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Vault;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class VaultServiceCollectionExtensionsShould
{
	[Fact]
	public void AddVaultKeyManagement_WithBuilder_RegistersProviderAndOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		_ = services.AddVaultKeyManagement(vault =>
		{
			vault.VaultUri(new Uri("http://127.0.0.1:8200"))
				.KeyNamePrefix("unit-");
		});

		// Verify service registrations exist (don't resolve — VaultKeyProvider connects to Vault on construction)
		services.ShouldContain(sd => sd.ServiceType == typeof(VaultKeyProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IKeyManagementProvider));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<VaultOptions>>().Value;
		options.VaultUri.ShouldBe(new Uri("http://127.0.0.1:8200"));
		options.KeyNamePrefix.ShouldBe("unit-");
	}

	[Fact]
	public void AddVaultKeyManagement_WithBuilder_SetsTransitMountPath()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddVaultKeyManagement(vault =>
		{
			vault.VaultUri(new Uri("http://127.0.0.1:18200"))
				.TransitMountPath("custom-transit")
				.KeyNamePrefix("custom-");
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<VaultOptions>>().Value;
		options.VaultUri.ShouldBe(new Uri("http://127.0.0.1:18200"));
		options.TransitMountPath.ShouldBe("custom-transit");
		options.KeyNamePrefix.ShouldBe("custom-");
	}

	[Fact]
	public void AddVaultKeyManagement_WithBuilder_SetsNamespace()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddVaultKeyManagement(vault =>
		{
			vault.VaultUri(new Uri("http://127.0.0.1:8200"))
				.Namespace("my-enterprise-ns");
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<VaultOptions>>().Value;
		options.Namespace.ShouldBe("my-enterprise-ns");
	}

	[Fact]
	public void AddVaultKeyManagement_WithBuilder_EnablesDetailedTelemetry()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddVaultKeyManagement(vault =>
		{
			vault.VaultUri(new Uri("http://127.0.0.1:8200"))
				.EnableDetailedTelemetry();
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<VaultOptions>>().Value;
		options.EnableDetailedTelemetry.ShouldBeTrue();
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
		_ = Should.Throw<ArgumentNullException>(() => services.AddVaultKeyManagement((Action<IComplianceVaultBuilder>)null!));
	}
}
