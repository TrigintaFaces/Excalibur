// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;

namespace Excalibur.Outbox.Tests.CosmosDb;

/// <summary>
/// Unit tests for <see cref="CosmosDbOutboxServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CosmosDbOutboxServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddCosmosDbOutboxStore_WithAction_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCosmosDbOutboxStore(options =>
		{
			options.ConnectionString = "test";
			options.DatabaseName = "testdb";
		});

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(CosmosDbOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithAction_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCosmosDbOutboxStore(options =>
		{
			options.ConnectionString = "test-connection";
			options.DatabaseName = "TestDatabase";
			options.ContainerName = "custom-container";
		});

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<CosmosDbOutboxOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithAction_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbOutboxStore(options => { }));
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithAction_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbOutboxStore((Action<CosmosDbOutboxOptions>)null!));
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionString"] = "test-connection",
				["DatabaseName"] = "TestDatabase"
			})
			.Build();

		// Act
		_ = services.AddCosmosDbOutboxStore(configuration);

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(CosmosDbOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithConfiguration_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbOutboxStore(configuration));
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithConfiguration_ThrowsOnNullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbOutboxStore((IConfiguration)null!));
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithSectionName_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["CosmosDb:ConnectionString"] = "test-connection",
				["CosmosDb:DatabaseName"] = "TestDatabase"
			})
			.Build();

		// Act
		_ = services.AddCosmosDbOutboxStore(configuration, "CosmosDb");

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(CosmosDbOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithSectionName_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbOutboxStore(configuration, "CosmosDb"));
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithSectionName_ThrowsOnNullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbOutboxStore(null!, "CosmosDb"));
	}

	[Fact]
	public void AddCosmosDbOutboxStore_WithSectionName_ThrowsOnNullOrWhitespaceSectionName()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCosmosDbOutboxStore(configuration, null!));
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCosmosDbOutboxStore(configuration, ""));
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCosmosDbOutboxStore(configuration, "   "));
	}

	[Fact]
	public void AddCosmosDbOutboxStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddCosmosDbOutboxStore(options =>
		{
			options.ConnectionString = "test";
			options.DatabaseName = "testdb";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}
}
