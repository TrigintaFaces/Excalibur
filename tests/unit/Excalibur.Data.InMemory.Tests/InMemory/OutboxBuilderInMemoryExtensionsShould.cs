// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="OutboxBuilderInMemoryExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DataInMemory")]
[Trait("Priority", "0")]
public sealed class OutboxBuilderInMemoryExtensionsShould : UnitTestBase
{
	private static ServiceCollection CreateServicesWithLogging()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		return services;
	}

	#region UseInMemory Tests

	[Fact]
	public void UseInMemory_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IOutboxBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseInMemory());
	}

	[Fact]
	public void UseInMemory_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseInMemory();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseInMemory_RegistersInMemoryOutboxStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemory();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<InMemoryOutboxStore>();
		store.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_RegistersIOutboxStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemory();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<IOutboxStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemoryOutboxStore>();
	}

	[Fact]
	public void UseInMemory_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemory();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<InMemoryOutboxOptions>>();
		options.ShouldNotBeNull();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_UsesDefaultValues_WhenNoConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemory();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(10000);
		options.Value.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void UseInMemory_AppliesMaxMessagesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		const int expectedMaxMessages = 5000;

		// Act
		_ = builder.UseInMemory(inmemory =>
		{
			inmemory.MaxMessages(expectedMaxMessages);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(expectedMaxMessages);
	}

	[Fact]
	public void UseInMemory_AppliesRetentionPeriodConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		var expectedRetention = TimeSpan.FromHours(12);

		// Act
		_ = builder.UseInMemory(inmemory =>
		{
			inmemory.RetentionPeriod(expectedRetention);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.DefaultRetentionPeriod.ShouldBe(expectedRetention);
	}

	[Fact]
	public void UseInMemory_AppliesMultipleConfigurations()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		const int expectedMaxMessages = 2500;
		var expectedRetention = TimeSpan.FromMinutes(30);

		// Act
		_ = builder.UseInMemory(inmemory =>
		{
			inmemory.MaxMessages(expectedMaxMessages)
					.RetentionPeriod(expectedRetention);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(expectedMaxMessages);
		options.Value.DefaultRetentionPeriod.ShouldBe(expectedRetention);
	}

	[Fact]
	public void UseInMemory_IsSingleton()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemory();
		var provider = services.BuildServiceProvider();

		// Assert
		var store1 = provider.GetRequiredService<InMemoryOutboxStore>();
		var store2 = provider.GetRequiredService<InMemoryOutboxStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void UseInMemory_IsIdempotent()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemory();
		_ = builder.UseInMemory();
		var provider = services.BuildServiceProvider();

		// Assert
		var stores = provider.GetServices<InMemoryOutboxStore>().ToList();
		stores.Count.ShouldBe(1);
	}

	[Fact]
	public void UseInMemory_AcceptsUnlimitedMaxMessages()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemory(inmemory =>
		{
			inmemory.MaxMessages(0); // Zero means unlimited
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(0);
	}

	#endregion
}
