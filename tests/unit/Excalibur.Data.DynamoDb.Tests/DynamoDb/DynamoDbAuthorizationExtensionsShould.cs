// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.DynamoDb.Authorization;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbAuthorizationExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "DependencyInjection")]
public sealed class DynamoDbAuthorizationExtensionsShould
{
	#region AddDynamoDbAuthorization with Action Tests

	[Fact]
	public void AddDynamoDbAuthorization_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbAuthorization(options => { }));
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithAction_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbAuthorization((Action<DynamoDbAuthorizationOptions>)null!));
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbAuthorization(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithAction_RegistersIGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbAuthorization(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IGrantStore) &&
			sd.ImplementationType == typeof(DynamoDbGrantStore));
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithAction_RegistersIActivityGroupGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbAuthorization(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IActivityGroupGrantStore) &&
			sd.ImplementationType == typeof(DynamoDbActivityGroupGrantStore));
	}

	#endregion

	#region AddDynamoDbAuthorization with ServiceUrl Tests

	[Fact]
	public void AddDynamoDbAuthorization_WithServiceUrl_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbAuthorization("http://localhost:4566"));
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithServiceUrl_ThrowsArgumentException_WhenServiceUrlIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbAuthorization((string)null!));
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithServiceUrl_ThrowsArgumentException_WhenServiceUrlIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbAuthorization(string.Empty));
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithServiceUrl_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbAuthorization("http://localhost:4566");

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithServiceUrl_AcceptsCustomTableNames()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbAuthorization(
			"http://localhost:4566",
			"custom_grants",
			"custom_activity_groups");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddDynamoDbGrantStore Tests

	[Fact]
	public void AddDynamoDbGrantStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbGrantStore(options => { }));
	}

	[Fact]
	public void AddDynamoDbGrantStore_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbGrantStore(null!));
	}

	[Fact]
	public void AddDynamoDbGrantStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbGrantStore(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbGrantStore_RegistersIGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbGrantStore(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IGrantStore) &&
			sd.ImplementationType == typeof(DynamoDbGrantStore));
	}

	[Fact]
	public void AddDynamoDbGrantStore_DoesNotRegisterIActivityGroupGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbGrantStore(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldNotContain(sd => sd.ServiceType == typeof(IActivityGroupGrantStore));
	}

	#endregion

	#region AddDynamoDbActivityGroupGrantStore Tests

	[Fact]
	public void AddDynamoDbActivityGroupGrantStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbActivityGroupGrantStore(options => { }));
	}

	[Fact]
	public void AddDynamoDbActivityGroupGrantStore_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbActivityGroupGrantStore(null!));
	}

	[Fact]
	public void AddDynamoDbActivityGroupGrantStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbActivityGroupGrantStore(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbActivityGroupGrantStore_RegistersIActivityGroupGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbActivityGroupGrantStore(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IActivityGroupGrantStore) &&
			sd.ImplementationType == typeof(DynamoDbActivityGroupGrantStore));
	}

	[Fact]
	public void AddDynamoDbActivityGroupGrantStore_DoesNotRegisterIGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbActivityGroupGrantStore(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldNotContain(sd => sd.ServiceType == typeof(IGrantStore));
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbAuthorizationExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbAuthorizationExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbAuthorizationExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
