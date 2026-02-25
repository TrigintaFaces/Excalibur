// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbServiceCollectionExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify service collection extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "DependencyInjection")]
public sealed class DynamoDbServiceCollectionExtensionsShould
{
	#region AddDynamoDb with Action Tests

	[Fact]
	public void AddDynamoDb_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDb(options => { }));
	}

	[Fact]
	public void AddDynamoDb_WithAction_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDb((Action<DynamoDbOptions>)null!));
	}

	[Fact]
	public void AddDynamoDb_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDb(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDb_WithAction_RegistersDynamoDbPersistenceProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDb(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbPersistenceProvider));
	}

	[Fact]
	public void AddDynamoDb_WithAction_RegistersDynamoDbHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDb(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbHealthCheck));
	}

	#endregion

	#region AddDynamoDb with Configuration Tests

	[Fact]
	public void AddDynamoDb_WithConfiguration_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDb(configuration));
	}

	[Fact]
	public void AddDynamoDb_WithConfiguration_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDb((IConfiguration)null!));
	}

	[Fact]
	public void AddDynamoDb_WithConfiguration_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Region"] = "us-west-2"
			})
			.Build();

		// Act
		var result = services.AddDynamoDb(configuration);

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddDynamoDb with Configuration and Section Tests

	[Fact]
	public void AddDynamoDb_WithSection_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDb(configuration, "DynamoDB"));
	}

	[Fact]
	public void AddDynamoDb_WithSection_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDb(null!, "DynamoDB"));
	}

	[Fact]
	public void AddDynamoDb_WithSection_ThrowsArgumentException_WhenSectionNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDb(configuration, null!));
	}

	[Fact]
	public void AddDynamoDb_WithSection_ThrowsArgumentException_WhenSectionNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDb(configuration, string.Empty));
	}

	[Fact]
	public void AddDynamoDb_WithSection_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["DynamoDB:Region"] = "eu-west-1"
			})
			.Build();

		// Act
		var result = services.AddDynamoDb(configuration, "DynamoDB");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddDynamoDbWithClient Tests

	[Fact]
	public void AddDynamoDbWithClient_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbWithClient(options => { }));
	}

	[Fact]
	public void AddDynamoDbWithClient_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbWithClient(null!));
	}

	[Fact]
	public void AddDynamoDbWithClient_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbWithClient(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbWithClient_RegistersDynamoDbHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbWithClient(options =>
		{
			options.Region = "us-east-1";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbHealthCheck));
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbServiceCollectionExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbServiceCollectionExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbServiceCollectionExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
