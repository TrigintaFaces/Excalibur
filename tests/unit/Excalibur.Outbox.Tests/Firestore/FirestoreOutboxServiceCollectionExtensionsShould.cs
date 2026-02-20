// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreOutboxServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestoreOutboxServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddFirestoreOutboxStore_WithAction_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - explicitly call to avoid ambiguity with other Firestore package
		_ = FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(FirestoreOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithAction_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, options =>
		{
			options.ProjectId = "test-project";
			options.CollectionName = "custom-collection";
		});

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<FirestoreOutboxOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithAction_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, options => { }));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithAction_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, (Action<FirestoreOutboxOptions>)null!));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ProjectId"] = "test-project",
			})
			.Build();

		// Act
		_ = FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, configuration);

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(FirestoreOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithConfiguration_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, configuration));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithConfiguration_ThrowsOnNullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, (IConfiguration)null!));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithOptions_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new FirestoreOutboxOptions
		{
			ProjectId = "test-project",
			CollectionName = "custom-collection",
		};

		// Act
		_ = FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, options);

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(FirestoreOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithOptions_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var options = new FirestoreOutboxOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, options));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithOptions_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, (FirestoreOutboxOptions)null!));
	}

	[Fact]
	public void AddFirestoreOutboxStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = FirestoreOutboxServiceCollectionExtensions.AddFirestoreOutboxStore(services, options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}
}
