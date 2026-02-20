// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.EventSourcing.Abstractions;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemorySnapshotExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DataInMemory")]
[Trait("Priority", "0")]
public sealed class InMemorySnapshotExtensionsShould : UnitTestBase
{
	private static ServiceCollection CreateServicesWithLogging()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		return services;
	}

	#region AddInMemorySnapshotStore Tests

	[Fact]
	public void AddInMemorySnapshotStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInMemorySnapshotStore());
	}

	[Fact]
	public void AddInMemorySnapshotStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddInMemorySnapshotStore();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddInMemorySnapshotStore_RegistersInMemorySnapshotStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemorySnapshotStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<InMemorySnapshotStore>();
		store.ShouldNotBeNull();
	}

	[Fact]
	public void AddInMemorySnapshotStore_RegistersISnapshotStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemorySnapshotStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<ISnapshotStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemorySnapshotStore>();
	}

	[Fact]
	public void AddInMemorySnapshotStore_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemorySnapshotStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<InMemorySnapshotOptions>>();
		options.ShouldNotBeNull();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void AddInMemorySnapshotStore_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		const int expectedMaxSnapshots = 500;

		// Act
		_ = services.AddInMemorySnapshotStore(opt =>
		{
			opt.MaxSnapshotsPerAggregate = expectedMaxSnapshots;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemorySnapshotOptions>>();
		options.Value.MaxSnapshotsPerAggregate.ShouldBe(expectedMaxSnapshots);
	}

	[Fact]
	public void AddInMemorySnapshotStore_IsSingleton()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemorySnapshotStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store1 = provider.GetRequiredService<InMemorySnapshotStore>();
		var store2 = provider.GetRequiredService<InMemorySnapshotStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void AddInMemorySnapshotStore_IsIdempotent()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemorySnapshotStore();
		_ = services.AddInMemorySnapshotStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var stores = provider.GetServices<InMemorySnapshotStore>().ToList();
		stores.Count.ShouldBe(1);
	}

	#endregion

	#region UseInMemorySnapshotStore Tests

	[Fact]
	public void UseInMemorySnapshotStore_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseInMemorySnapshotStore());
	}

	[Fact]
	public void UseInMemorySnapshotStore_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseInMemorySnapshotStore();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseInMemorySnapshotStore_RegistersServices()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemorySnapshotStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<InMemorySnapshotStore>();
		store.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemorySnapshotStore_AppliesConfiguration()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		const int expectedMaxSnapshots = 250;

		// Act
		_ = builder.UseInMemorySnapshotStore(opt =>
		{
			opt.MaxSnapshotsPerAggregate = expectedMaxSnapshots;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemorySnapshotOptions>>();
		options.Value.MaxSnapshotsPerAggregate.ShouldBe(expectedMaxSnapshots);
	}

	#endregion
}
