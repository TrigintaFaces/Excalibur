// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using StackExchange.Redis;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Depth tests for <see cref="CoordinationServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class CoordinationServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddJobCoordinationRedis_WithConnectionString_ThrowsOnNullServices()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services!.AddJobCoordinationRedis("localhost"));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithConnectionString_ThrowsOnNullConnectionString()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis((string)null!));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithConnectionString_ThrowsOnEmptyConnectionString()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis(""));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithMultiplexer_ThrowsOnNullServices()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services!.AddJobCoordinationRedis(A.Fake<IConnectionMultiplexer>()));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithMultiplexer_ThrowsOnNullMultiplexer()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordinationRedis((IConnectionMultiplexer)null!));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithMultiplexer_RegistersCoordinatorInterfaces()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._))
			.Returns(A.Fake<IDatabase>());

		// Act
		services.AddJobCoordinationRedis(multiplexer);

		// Assert -- sub-interfaces registered, composite IJobCoordinator deleted
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobLockProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobRegistry));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobDistributor));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithMultiplexer_ReturnsSameServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var multiplexer = A.Fake<IConnectionMultiplexer>();

		// Act
		var result = services.AddJobCoordinationRedis(multiplexer);

		// Assert
		result.ShouldBeSameAs(services);
	}
}
