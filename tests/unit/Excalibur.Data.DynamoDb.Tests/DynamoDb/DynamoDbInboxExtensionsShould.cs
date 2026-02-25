// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb.Inbox;
using Excalibur.Dispatch.Abstractions;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbInboxExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify inbox extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "DependencyInjection")]
public sealed class DynamoDbInboxExtensionsShould
{
	#region AddDynamoDbInboxStore with Action Tests

	[Fact]
	public void AddDynamoDbInboxStore_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbInboxStore(options => { }));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithAction_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbInboxStore(null!));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbInboxStore(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "inbox";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithAction_RegistersDynamoDbInboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbInboxStore(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "inbox";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbInboxStore));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithAction_RegistersIInboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbInboxStore(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "inbox";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IInboxStore));
	}

	#endregion

	#region AddDynamoDbInboxStore with Region and TableName Tests

	[Fact]
	public void AddDynamoDbInboxStore_WithRegion_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbInboxStore("us-east-1", "inbox"));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithRegion_ThrowsArgumentException_WhenRegionIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbInboxStore(null!, "inbox"));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithRegion_ThrowsArgumentException_WhenRegionIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbInboxStore(string.Empty, "inbox"));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithRegion_ThrowsArgumentException_WhenTableNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbInboxStore("us-east-1", null!));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithRegion_ThrowsArgumentException_WhenTableNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbInboxStore("us-east-1", string.Empty));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithRegion_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbInboxStore("us-east-1", "inbox_messages");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddDynamoDbInboxStore with ClientProvider Tests

	[Fact]
	public void AddDynamoDbInboxStore_WithClientProvider_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		var client = A.Fake<IAmazonDynamoDB>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbInboxStore(_ => client, options => { }));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithClientProvider_ThrowsArgumentNullException_WhenClientProviderIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbInboxStore(null!, options => { }));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithClientProvider_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var client = A.Fake<IAmazonDynamoDB>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbInboxStore(_ => client, null!));
	}

	[Fact]
	public void AddDynamoDbInboxStore_WithClientProvider_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var client = A.Fake<IAmazonDynamoDB>();

		// Act
		var result = services.AddDynamoDbInboxStore(
			_ => client,
			options =>
			{
				options.TableName = "inbox";
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
		typeof(DynamoDbInboxExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbInboxExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbInboxExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
