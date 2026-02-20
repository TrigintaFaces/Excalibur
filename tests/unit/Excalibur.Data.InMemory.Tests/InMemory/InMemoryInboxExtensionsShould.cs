// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryInboxExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DataInMemory")]
[Trait("Priority", "0")]
public sealed class InMemoryInboxExtensionsShould : UnitTestBase
{
	private static ServiceCollection CreateServicesWithLogging()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		return services;
	}

	#region AddInMemoryInboxStore Tests

	[Fact]
	public void AddInMemoryInboxStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryInboxStore());
	}

	[Fact]
	public void AddInMemoryInboxStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddInMemoryInboxStore();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddInMemoryInboxStore_RegistersInMemoryInboxStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryInboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<InMemoryInboxStore>();
		store.ShouldNotBeNull();
	}

	[Fact]
	public void AddInMemoryInboxStore_RegistersIInboxStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryInboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<IInboxStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemoryInboxStore>();
	}

	[Fact]
	public void AddInMemoryInboxStore_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryInboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<InMemoryInboxOptions>>();
		options.ShouldNotBeNull();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void AddInMemoryInboxStore_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedRetention = TimeSpan.FromHours(2);

		// Act
		_ = services.AddInMemoryInboxStore(opt =>
		{
			opt.RetentionPeriod = expectedRetention;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryInboxOptions>>();
		options.Value.RetentionPeriod.ShouldBe(expectedRetention);
	}

	[Fact]
	public void AddInMemoryInboxStore_IsSingleton()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryInboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store1 = provider.GetRequiredService<InMemoryInboxStore>();
		var store2 = provider.GetRequiredService<InMemoryInboxStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void AddInMemoryInboxStore_IsIdempotent()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryInboxStore();
		_ = services.AddInMemoryInboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var stores = provider.GetServices<InMemoryInboxStore>().ToList();
		stores.Count.ShouldBe(1);
	}

	#endregion

	#region UseInMemoryInboxStore Tests

	[Fact]
	public void UseInMemoryInboxStore_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseInMemoryInboxStore());
	}

	[Fact]
	public void UseInMemoryInboxStore_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseInMemoryInboxStore();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseInMemoryInboxStore_RegistersServices()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemoryInboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<InMemoryInboxStore>();
		store.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemoryInboxStore_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		var expectedRetention = TimeSpan.FromMinutes(30);

		// Act
		_ = builder.UseInMemoryInboxStore(opt =>
		{
			opt.RetentionPeriod = expectedRetention;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryInboxOptions>>();
		options.Value.RetentionPeriod.ShouldBe(expectedRetention);
	}

	#endregion
}
