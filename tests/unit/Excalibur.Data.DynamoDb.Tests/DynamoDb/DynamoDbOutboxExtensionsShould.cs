// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Outbox.DynamoDb;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbOutboxServiceCollectionExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Sprint 633: Updated for extraction -- AddDynamoDbOutboxStore, ICloudNativeOutboxStore.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "DependencyInjection")]
public sealed class DynamoDbOutboxExtensionsShould
{
	#region AddDynamoDbOutboxStore Tests

	[Fact]
	public void AddDynamoDbOutboxStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services!.AddDynamoDbOutboxStore(options => { }));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutboxStore((Action<DynamoDbOutboxOptions>)null!));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbOutboxStore(options =>
		{
			options.Connection.Region = "us-east-1";
			options.TableName = "outbox";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbOutboxStore_RegistersICloudNativeOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbOutboxStore(options =>
		{
			options.Connection.Region = "us-east-1";
			options.TableName = "outbox";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	[Fact]
	public void AddDynamoDbOutboxStore_RegistersDynamoDbOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbOutboxStore(options =>
		{
			options.Connection.Region = "us-east-1";
			options.TableName = "outbox";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(DynamoDbOutboxStore));
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbOutboxServiceCollectionExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbOutboxServiceCollectionExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbOutboxServiceCollectionExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
