// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Cdc;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcServiceCollectionExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify CDC extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "DependencyInjection")]
public sealed class DynamoDbCdcExtensionsShould
{
	#region AddDynamoDbCdc Tests

	[Fact]
	public void AddDynamoDbCdc_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbCdc(options =>
			{
				options.StreamArn = "arn:aws:dynamodb:...";
			}));
	}

	[Fact]
	public void AddDynamoDbCdc_ThrowsArgumentNullException_WhenConfigureOptionsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbCdc((Action<DynamoDbCdcOptions>)null!));
	}

	[Fact]
	public void AddDynamoDbCdc_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbCdc(options =>
		{
			options.StreamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbCdc_RegistersDynamoDbCdcOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbCdc(options =>
		{
			options.StreamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigureOptions<DynamoDbCdcOptions>));
	}

	[Fact]
	public void AddDynamoDbCdc_RegistersIDynamoDbCdcProcessor()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbCdc(options =>
		{
			options.StreamArn = "arn:aws:dynamodb:...";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDynamoDbCdcProcessor) &&
			sd.ImplementationType == typeof(DynamoDbCdcProcessor));
	}

	#endregion

	#region AddInMemoryDynamoDbCdcStateStore Tests

	[Fact]
	public void AddInMemoryDynamoDbCdcStateStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryDynamoDbCdcStateStore());
	}

	[Fact]
	public void AddInMemoryDynamoDbCdcStateStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddInMemoryDynamoDbCdcStateStore();

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddInMemoryDynamoDbCdcStateStore_RegistersIDynamoDbCdcStateStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddInMemoryDynamoDbCdcStateStore();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDynamoDbCdcStateStore) &&
			sd.ImplementationType == typeof(InMemoryDynamoDbCdcStateStore));
	}

	#endregion

	#region AddDynamoDbCdcStateStore Tests

	[Fact]
	public void AddDynamoDbCdcStateStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbCdcStateStore("cdc-state"));
	}

	[Fact]
	public void AddDynamoDbCdcStateStore_ThrowsArgumentException_WhenTableNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbCdcStateStore(null!));
	}

	[Fact]
	public void AddDynamoDbCdcStateStore_ThrowsArgumentException_WhenTableNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbCdcStateStore(string.Empty));
	}

	[Fact]
	public void AddDynamoDbCdcStateStore_ThrowsArgumentException_WhenTableNameIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbCdcStateStore("   "));
	}

	[Fact]
	public void AddDynamoDbCdcStateStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbCdcStateStore("cdc-state-table");

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbCdcStateStore_RegistersOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbCdcStateStore("cdc-state-table");

		// Assert — ValidateDataAnnotations() registers IValidateOptions via DataAnnotationValidateOptions
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<DynamoDbCdcStateStoreOptions>));
	}

	[Fact]
	public void AddDynamoDbCdcStateStore_RegistersIDynamoDbCdcStateStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbCdcStateStore("cdc-state-table");

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IDynamoDbCdcStateStore));
	}

	[Fact]
	public void AddDynamoDbCdcStateStore_AcceptsConfigureOptionsAction()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbCdcStateStore("cdc-state-table", options =>
		{
			// Configure additional options if needed
			options.TableName = "custom-cdc-state";
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
		typeof(DynamoDbCdcServiceCollectionExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbCdcServiceCollectionExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbCdcServiceCollectionExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
