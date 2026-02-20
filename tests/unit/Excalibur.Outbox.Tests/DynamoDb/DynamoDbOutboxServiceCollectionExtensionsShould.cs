// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbOutboxServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DynamoDbOutboxServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddDynamoDbOutboxStore_WithAction_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDynamoDbOutboxStore(options =>
		{
			options.ServiceUrl = "http://localhost:8000";
		});

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(DynamoDbOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithAction_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDynamoDbOutboxStore(options =>
		{
			options.ServiceUrl = "http://localhost:8000";
			options.TableName = "custom-table";
		});

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<DynamoDbOutboxOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithAction_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutboxStore(options => { }));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithAction_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutboxStore((Action<DynamoDbOutboxOptions>)null!));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ServiceUrl"] = "http://localhost:8000",
			})
			.Build();

		// Act
		_ = services.AddDynamoDbOutboxStore(configuration);

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(DynamoDbOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithConfiguration_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutboxStore(configuration));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithConfiguration_ThrowsOnNullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutboxStore((IConfiguration)null!));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithOptions_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new DynamoDbOutboxOptions
		{
			ServiceUrl = "http://localhost:8000",
			TableName = "custom-table",
		};

		// Act
		_ = services.AddDynamoDbOutboxStore(options);

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(DynamoDbOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithOptions_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var options = new DynamoDbOutboxOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutboxStore(options));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_WithOptions_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutboxStore((DynamoDbOutboxOptions)null!));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbOutboxStore(options =>
		{
			options.ServiceUrl = "http://localhost:8000";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}
}
