// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb.Saga;
using Excalibur.Dispatch.Abstractions.Messaging;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbSagaExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify saga extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "DependencyInjection")]
public sealed class DynamoDbSagaExtensionsShould
{
	#region AddDynamoDbSagaStore with Action Tests

	[Fact]
	public void AddDynamoDbSagaStore_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSagaStore(options => { }));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithAction_ThrowsArgumentNullException_WhenConfigureOptionsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSagaStore((Action<DynamoDbSagaOptions>)null!));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbSagaStore(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "sagas";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithAction_RegistersDynamoDbSagaStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbSagaStore(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "sagas";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbSagaStore));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithAction_RegistersISagaStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbSagaStore(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "sagas";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ISagaStore));
	}

	#endregion

	#region AddDynamoDbSagaStore with ServiceUrl Tests

	[Fact]
	public void AddDynamoDbSagaStore_WithServiceUrl_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSagaStore("http://localhost:8000"));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithServiceUrl_ThrowsArgumentException_WhenServiceUrlIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbSagaStore((string)null!));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithServiceUrl_ThrowsArgumentException_WhenServiceUrlIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbSagaStore(string.Empty));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithServiceUrl_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbSagaStore("http://localhost:8000");

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithServiceUrl_UsesDefaultTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbSagaStore("http://localhost:8000");

		// Assert - verifies that the registration was successful (default tableName "sagas")
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbSagaStore));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithServiceUrl_AcceptsCustomTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbSagaStore("http://localhost:8000", "custom_sagas");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddDynamoDbSagaStore with ClientFactory Tests

	[Fact]
	public void AddDynamoDbSagaStore_WithClientFactory_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		var client = A.Fake<IAmazonDynamoDB>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSagaStore(_ => client, options => { }));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithClientFactory_ThrowsArgumentNullException_WhenClientFactoryIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSagaStore(null!, options => { }));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithClientFactory_ThrowsArgumentNullException_WhenConfigureOptionsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var client = A.Fake<IAmazonDynamoDB>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbSagaStore(_ => client, null!));
	}

	[Fact]
	public void AddDynamoDbSagaStore_WithClientFactory_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var client = A.Fake<IAmazonDynamoDB>();

		// Act
		var result = services.AddDynamoDbSagaStore(
			_ => client,
			options =>
			{
				options.TableName = "sagas";
			});

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbSagaExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbSagaExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbSagaExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
