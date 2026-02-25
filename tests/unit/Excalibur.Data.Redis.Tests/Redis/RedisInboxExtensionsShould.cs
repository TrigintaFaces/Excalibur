// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis.Inbox;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="RedisInboxExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.3): Redis unit tests.
/// Tests verify inbox extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Redis")]
[Trait("Feature", "DependencyInjection")]
public sealed class RedisInboxExtensionsShould
{
	#region AddRedisInboxStore with Action Tests

	[Fact]
	public void AddRedisInboxStore_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisInboxStore(options => { }));
	}

	[Fact]
	public void AddRedisInboxStore_WithAction_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisInboxStore((Action<RedisInboxOptions>)null!));
	}

	[Fact]
	public void AddRedisInboxStore_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRedisInboxStore(options =>
		{
			options.ConnectionString = "localhost:6379";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddRedisInboxStore_WithAction_RegistersRedisInboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRedisInboxStore(options =>
		{
			options.ConnectionString = "localhost:6379";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(RedisInboxStore));
	}

	[Fact]
	public void AddRedisInboxStore_WithAction_RegistersIInboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRedisInboxStore(options =>
		{
			options.ConnectionString = "localhost:6379";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IInboxStore));
	}

	#endregion

	#region AddRedisInboxStore with ConnectionString Tests

	[Fact]
	public void AddRedisInboxStore_WithConnectionString_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisInboxStore("localhost:6379"));
	}

	[Fact]
	public void AddRedisInboxStore_WithConnectionString_ThrowsArgumentException_WhenConnectionStringIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddRedisInboxStore((string)null!));
	}

	[Fact]
	public void AddRedisInboxStore_WithConnectionString_ThrowsArgumentException_WhenConnectionStringIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddRedisInboxStore(string.Empty));
	}

	[Fact]
	public void AddRedisInboxStore_WithConnectionString_ThrowsArgumentException_WhenConnectionStringIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddRedisInboxStore("   "));
	}

	[Fact]
	public void AddRedisInboxStore_WithConnectionString_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRedisInboxStore("localhost:6379");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddRedisInboxStore with ConnectionProvider Tests

	[Fact]
	public void AddRedisInboxStore_WithConnectionProvider_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services!.AddRedisInboxStore(_ => null!, options => { }));
	}

	[Fact]
	public void AddRedisInboxStore_WithConnectionProvider_ThrowsArgumentNullException_WhenConnectionProviderIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisInboxStore(null!, options => { }));
	}

	[Fact]
	public void AddRedisInboxStore_WithConnectionProvider_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisInboxStore(_ => null!, null!));
	}

	[Fact]
	public void AddRedisInboxStore_WithConnectionProvider_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRedisInboxStore(
			_ => null!,
			options => { options.ConnectionString = "test"; });

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(RedisInboxExtensions).IsAbstract.ShouldBeTrue();
		typeof(RedisInboxExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(RedisInboxExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
