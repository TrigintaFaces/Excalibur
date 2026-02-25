// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.DynamoDb.Authorization;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbAuthorizationExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify authorization extension methods.
/// </remarks>
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
	public void AddDynamoDbAuthorization_WithAction_RegistersIGrantRequestProvider()
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
			sd.ServiceType == typeof(IGrantRequestProvider) &&
			sd.ImplementationType == typeof(DynamoDbGrantService));
	}

	[Fact]
	public void AddDynamoDbAuthorization_WithAction_RegistersIActivityGroupGrantService()
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
			sd.ServiceType == typeof(IActivityGroupGrantService) &&
			sd.ImplementationType == typeof(DynamoDbActivityGroupGrantService));
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

	#region AddDynamoDbGrantService Tests

	[Fact]
	public void AddDynamoDbGrantService_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbGrantService(options => { }));
	}

	[Fact]
	public void AddDynamoDbGrantService_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbGrantService(null!));
	}

	[Fact]
	public void AddDynamoDbGrantService_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbGrantService(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbGrantService_RegistersIGrantRequestProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbGrantService(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IGrantRequestProvider) &&
			sd.ImplementationType == typeof(DynamoDbGrantService));
	}

	[Fact]
	public void AddDynamoDbGrantService_DoesNotRegisterIActivityGroupGrantService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbGrantService(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldNotContain(sd => sd.ServiceType == typeof(IActivityGroupGrantService));
	}

	#endregion

	#region AddDynamoDbActivityGroupGrantService Tests

	[Fact]
	public void AddDynamoDbActivityGroupGrantService_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbActivityGroupGrantService(options => { }));
	}

	[Fact]
	public void AddDynamoDbActivityGroupGrantService_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbActivityGroupGrantService(null!));
	}

	[Fact]
	public void AddDynamoDbActivityGroupGrantService_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbActivityGroupGrantService(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbActivityGroupGrantService_RegistersIActivityGroupGrantService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbActivityGroupGrantService(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IActivityGroupGrantService) &&
			sd.ImplementationType == typeof(DynamoDbActivityGroupGrantService));
	}

	[Fact]
	public void AddDynamoDbActivityGroupGrantService_DoesNotRegisterIGrantRequestProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbActivityGroupGrantService(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldNotContain(sd => sd.ServiceType == typeof(IGrantRequestProvider));
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
