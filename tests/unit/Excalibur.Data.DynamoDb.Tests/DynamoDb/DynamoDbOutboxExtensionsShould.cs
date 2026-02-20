// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Outbox;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbOutboxExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify outbox extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "DependencyInjection")]
public sealed class DynamoDbOutboxExtensionsShould
{
	#region AddDynamoDbOutbox Tests

	[Fact]
	public void AddDynamoDbOutbox_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutbox(options => { }));
	}

	[Fact]
	public void AddDynamoDbOutbox_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutbox(null!));
	}

	[Fact]
	public void AddDynamoDbOutbox_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbOutbox(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "outbox";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbOutbox_RegistersIOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbOutbox(options =>
		{
			options.Region = "us-east-1";
			options.TableName = "outbox";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore) &&
			sd.ImplementationType == typeof(DynamoDbOutboxStore));
	}

	#endregion

	#region AddDynamoDbOutbox Named Tests

	[Fact]
	public void AddDynamoDbOutbox_Named_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutbox("test", options => { }));
	}

	[Fact]
	public void AddDynamoDbOutbox_Named_ThrowsArgumentException_WhenNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbOutbox(null!, options => { }));
	}

	[Fact]
	public void AddDynamoDbOutbox_Named_ThrowsArgumentException_WhenNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDynamoDbOutbox(string.Empty, options => { }));
	}

	[Fact]
	public void AddDynamoDbOutbox_Named_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDynamoDbOutbox("test", null!));
	}

	[Fact]
	public void AddDynamoDbOutbox_Named_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDynamoDbOutbox("primary", options =>
		{
			options.Region = "us-east-1";
			options.TableName = "outbox";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddDynamoDbOutbox_Named_RegistersIOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDynamoDbOutbox("primary", options =>
		{
			options.Region = "us-east-1";
			options.TableName = "outbox";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore) &&
			sd.ImplementationType == typeof(DynamoDbOutboxStore));
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbOutboxExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbOutboxExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbOutboxExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
