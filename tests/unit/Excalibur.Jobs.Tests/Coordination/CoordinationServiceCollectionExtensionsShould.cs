// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Unit tests for <see cref="CoordinationServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class CoordinationServiceCollectionExtensionsShould
{
	// --- AddJobCoordinationRedis (connection string) ---

	[Fact]
	public void AddJobCoordinationRedisThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordinationRedis("localhost:6379"));
	}

	[Fact]
	public void AddJobCoordinationRedisThrowWhenConnectionStringIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis((string)null!));
	}

	[Fact]
	public void AddJobCoordinationRedisThrowWhenConnectionStringIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis(""));
	}

	[Fact]
	public void AddJobCoordinationRedisThrowWhenConnectionStringIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis("   "));
	}

	// --- AddJobCoordinationRedis (connection multiplexer) ---

	[Fact]
	public void AddJobCoordinationRedisWithMultiplexerThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var multiplexer = FakeItEasy.A.Fake<StackExchange.Redis.IConnectionMultiplexer>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordinationRedis(multiplexer));
	}

	[Fact]
	public void AddJobCoordinationRedisWithMultiplexerThrowWhenMultiplexerIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordinationRedis((StackExchange.Redis.IConnectionMultiplexer)null!));
	}
}
