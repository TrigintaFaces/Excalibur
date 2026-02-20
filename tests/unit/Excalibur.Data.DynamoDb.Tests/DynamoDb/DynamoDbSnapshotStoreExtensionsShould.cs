// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Snapshots;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbSnapshotStoreExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify snapshot store extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "DependencyInjection")]
public sealed class DynamoDbSnapshotStoreExtensionsShould
{
	#region AddDynamoDbSnapshotStore Tests

	[Fact]
	public void AddDynamoDbSnapshotStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSnapshotStore(options => { }));
	}

	[Fact]
	public void AddDynamoDbSnapshotStore_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSnapshotStore(null!));
	}

	[Fact]
	public void AddDynamoDbSnapshotStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbSnapshotStore(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "snapshots";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbSnapshotStore_RegistersISnapshotStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbSnapshotStore(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "snapshots";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISnapshotStore) &&
			sd.ImplementationType == typeof(DynamoDbSnapshotStore));
	}

	#endregion

	#region AddDynamoDbSnapshotStore Named Tests

	[Fact]
	public void AddDynamoDbSnapshotStore_Named_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSnapshotStore("test", options => { }));
	}

	[Fact]
	public void AddDynamoDbSnapshotStore_Named_ThrowsArgumentException_WhenNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbSnapshotStore(null!, options => { }));
	}

	[Fact]
	public void AddDynamoDbSnapshotStore_Named_ThrowsArgumentException_WhenNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbSnapshotStore(string.Empty, options => { }));
	}

	[Fact]
	public void AddDynamoDbSnapshotStore_Named_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSnapshotStore("test", null!));
	}

	[Fact]
	public void AddDynamoDbSnapshotStore_Named_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbSnapshotStore("primary", options =>
		{
			options.Region = "us-east-1";
			options.TableName = "snapshots";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbSnapshotStore_Named_RegistersISnapshotStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbSnapshotStore("primary", options =>
		{
			options.Region = "us-east-1";
			options.TableName = "snapshots";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISnapshotStore) &&
			sd.ImplementationType == typeof(DynamoDbSnapshotStore));
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbSnapshotStoreExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbSnapshotStoreExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbSnapshotStoreExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
