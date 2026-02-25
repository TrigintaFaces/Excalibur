// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compliance.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterEncryptionWithBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddEncryption(builder => builder
			.UseInMemoryKeyManagement("test-provider"));

		// Assert
		var descriptors = services.Where(d => d.ServiceType == typeof(IEncryptionProviderRegistry));
		descriptors.ShouldNotBeEmpty();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddEncryption()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddEncryption(b => b.UseInMemoryKeyManagement()));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull_AddEncryption()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() => services.AddEncryption(null!));
	}

	[Fact]
	public void RegisterDevEncryption()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddDevEncryption();

		// Assert
		var registryDescriptors = services.Where(d => d.ServiceType == typeof(IEncryptionProviderRegistry));
		registryDescriptors.ShouldNotBeEmpty();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddDevEncryption()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddDevEncryption());
	}

	[Fact]
	public void RegisterHkdfKeyDerivation()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddHkdfKeyDerivation();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<Excalibur.Dispatch.Compliance.Encryption.HkdfKeyDeriver>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterHkdfKeyDerivationWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddHkdfKeyDerivation(opts => opts.DefaultOutputLength = 64);

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<Excalibur.Dispatch.Compliance.Encryption.HkdfKeyDeriver>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddHkdfKeyDerivation()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddHkdfKeyDerivation());
	}
}
