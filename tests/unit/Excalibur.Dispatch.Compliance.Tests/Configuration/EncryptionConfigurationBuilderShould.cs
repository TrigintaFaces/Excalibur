// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compliance.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionConfigurationBuilderShould
{
	[Fact]
	public void RegisterInMemoryProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IEncryptionProviderRegistry, EncryptionProviderRegistry>();

		// Act
		services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test-provider"));

		// Assert - builder should not throw and should register services
		var descriptors = services.Where(d => d.ServiceType == typeof(IEncryptionProvider));
		descriptors.ShouldNotBeEmpty();
	}

	[Fact]
	public void RegisterCustomKeyManagementProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddEncryption(builder => builder
			.UseKeyManagement<AesGcmEncryptionProvider>("custom-aes"));

		// Assert
		var descriptors = services.Where(d => d.ServiceType == typeof(IEncryptionProvider));
		descriptors.ShouldNotBeEmpty();
	}

	[Fact]
	public void RegisterProviderInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var mockProvider = A.Fake<IEncryptionProvider>();

		// Act
		services.AddEncryption(builder => builder
			.UseProvider("mock-provider", mockProvider));

		// Assert
		var descriptors = services.Where(d => d.ServiceType == typeof(IEncryptionProvider));
		descriptors.ShouldNotBeEmpty();
	}

	[Fact]
	public void SetPrimaryProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act - should not throw
		services.AddEncryption(builder => builder
			.UseInMemoryKeyManagement("provider-a")
			.UseInMemoryKeyManagement("provider-b")
			.SetAsPrimary("provider-b"));

		// Assert
		var descriptors = services.Where(d => d.ServiceType == typeof(IEncryptionProvider));
		descriptors.Count().ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void ThrowWhenSetPrimaryForUnregisteredProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			services.AddEncryption(builder => builder
				.UseInMemoryKeyManagement("provider-a")
				.SetAsPrimary("nonexistent-provider")));
	}

	[Fact]
	public void AddLegacyProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act - should not throw
		services.AddEncryption(builder => builder
			.UseInMemoryKeyManagement("current")
			.UseInMemoryKeyManagement("old")
			.AddLegacy("old"));

		// Assert - no exception means success
		services.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenAddLegacyForUnregisteredProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			services.AddEncryption(builder => builder
				.UseInMemoryKeyManagement("current")
				.AddLegacy("nonexistent")));
	}

	[Fact]
	public void ConfigureEncryptionOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddEncryption(builder => builder
			.UseInMemoryKeyManagement("test")
			.ConfigureOptions(opts => opts.DefaultPurpose = "field-encryption"));

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<EncryptionOptions>();
		options.ShouldNotBeNull();
		options.DefaultPurpose.ShouldBe("field-encryption");
	}

	[Fact]
	public void ThrowWhenNoProvidersRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			services.AddEncryption(_ => { }));
	}

	[Fact]
	public void ThrowWhenUseProviderWithNullProviderId()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddEncryption(builder => builder
				.UseProvider(null!, A.Fake<IEncryptionProvider>())));
	}

	[Fact]
	public void ThrowWhenUseProviderWithNullProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddEncryption(builder => builder
				.UseProvider("test", null!)));
	}

	[Fact]
	public void ThrowWhenConfigureOptionsWithNull()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddEncryption(builder => builder
				.UseInMemoryKeyManagement("test")
				.ConfigureOptions(null!)));
	}

	[Fact]
	public void ThrowWhenSetAsPrimaryWithNull()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddEncryption(builder => builder
				.UseInMemoryKeyManagement("test")
				.SetAsPrimary(null!)));
	}

	[Fact]
	public void ThrowWhenAddLegacyWithNull()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddEncryption(builder => builder
				.UseInMemoryKeyManagement("test")
				.AddLegacy(null!)));
	}
}
