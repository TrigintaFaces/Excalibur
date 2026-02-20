// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.InMemory.Outbox;
using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Excalibur.Data.InMemory;
namespace Excalibur.Data.Tests.InMemory.Builders;

/// <summary>
/// Unit tests for <see cref="IInMemoryOutboxBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the ADR-098 Microsoft-style fluent builder pattern implementation
/// for the in-memory outbox provider (for testing scenarios).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryOutboxBuilderShould : UnitTestBase
{
	[Fact]
	public void UseInMemory_ThrowsOnNullBuilder()
	{
		// Arrange
		IOutboxBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseInMemory());
	}

	[Fact]
	public void UseInMemory_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		IOutboxBuilder? capturedResult = null;

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			capturedResult = builder.UseInMemory();
		});

		// Assert
		_ = capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_RegistersInMemoryOutboxOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<InMemoryOutboxOptions>>();
		_ = options.ShouldNotBeNull();
		_ = options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_RegistersIOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory();
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseInMemory_ConfiguresMaxMessages()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.MaxMessages(500);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(500);
	}

	[Fact]
	public void UseInMemory_ConfiguresRetentionPeriod()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedPeriod = TimeSpan.FromHours(12);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.RetentionPeriod(expectedPeriod);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.DefaultRetentionPeriod.ShouldBe(expectedPeriod);
	}

	[Fact]
	public void UseInMemory_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.MaxMessages(1000)
						.RetentionPeriod(TimeSpan.FromHours(2));
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(1000);
		options.Value.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromHours(2));
	}

	[Fact]
	public void UseInMemory_CombinesWithCoreBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.UseInMemory(inmemory =>
				{
					_ = inmemory.MaxMessages(200);
				})
				.WithProcessing(p => p.BatchSize(50).PollingInterval(TimeSpan.FromSeconds(1)))
				.WithCleanup(c => c.EnableAutoCleanup(true).RetentionPeriod(TimeSpan.FromMinutes(30)));
		});
		var provider = services.BuildServiceProvider();

		// Assert - InMemory options
		var inmemoryOptions = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		inmemoryOptions.Value.MaxMessages.ShouldBe(200);

		// Assert - Core outbox options
		var outboxOptions = provider.GetRequiredService<OutboxOptions>();
		outboxOptions.BatchSize.ShouldBe(50);
		outboxOptions.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
		outboxOptions.EnableAutomaticCleanup.ShouldBeTrue();
		outboxOptions.MessageRetentionPeriod.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void UseInMemory_WorksWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory();
		});
		var provider = services.BuildServiceProvider();

		// Assert - defaults are applied
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		_ = options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_AllowsZeroMaxMessages_AsUnlimited()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.MaxMessages(0); // Zero means unlimited
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(0);
	}
}
