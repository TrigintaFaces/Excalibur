// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis.Outbox;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="RedisOutboxExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.3): Redis unit tests.
/// Tests verify outbox extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Redis")]
[Trait("Feature", "DependencyInjection")]
public sealed class RedisOutboxExtensionsShould
{
	#region AddRedisOutboxStore with Action Tests

	[Fact]
	public void AddRedisOutboxStore_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisOutboxStore(options => { }));
	}

	[Fact]
	public void AddRedisOutboxStore_WithAction_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisOutboxStore((Action<RedisOutboxOptions>)null!));
	}

	[Fact]
	public void AddRedisOutboxStore_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRedisOutboxStore(options =>
		{
			options.ConnectionString = "localhost:6379";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddRedisOutboxStore_WithAction_RegistersRedisOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRedisOutboxStore(options =>
		{
			options.ConnectionString = "localhost:6379";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(RedisOutboxStore));
	}

	[Fact]
	public void AddRedisOutboxStore_WithAction_RegistersIOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRedisOutboxStore(options =>
		{
			options.ConnectionString = "localhost:6379";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IOutboxStore));
	}

	#endregion

	#region AddRedisOutboxStore with ConnectionString Tests

	[Fact]
	public void AddRedisOutboxStore_WithConnectionString_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisOutboxStore("localhost:6379"));
	}

	[Fact]
	public void AddRedisOutboxStore_WithConnectionString_ThrowsArgumentException_WhenConnectionStringIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddRedisOutboxStore((string)null!));
	}

	[Fact]
	public void AddRedisOutboxStore_WithConnectionString_ThrowsArgumentException_WhenConnectionStringIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddRedisOutboxStore(string.Empty));
	}

	[Fact]
	public void AddRedisOutboxStore_WithConnectionString_ThrowsArgumentException_WhenConnectionStringIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddRedisOutboxStore("   "));
	}

	[Fact]
	public void AddRedisOutboxStore_WithConnectionString_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRedisOutboxStore("localhost:6379");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddRedisOutboxStore with ConnectionProvider Tests

	[Fact]
	public void AddRedisOutboxStore_WithConnectionProvider_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services!.AddRedisOutboxStore(_ => null!, options => { }));
	}

	[Fact]
	public void AddRedisOutboxStore_WithConnectionProvider_ThrowsArgumentNullException_WhenConnectionProviderIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisOutboxStore(null!, options => { }));
	}

	[Fact]
	public void AddRedisOutboxStore_WithConnectionProvider_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisOutboxStore(_ => null!, null!));
	}

	[Fact]
	public void AddRedisOutboxStore_WithConnectionProvider_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRedisOutboxStore(
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
		typeof(RedisOutboxExtensions).IsAbstract.ShouldBeTrue();
		typeof(RedisOutboxExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(RedisOutboxExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
