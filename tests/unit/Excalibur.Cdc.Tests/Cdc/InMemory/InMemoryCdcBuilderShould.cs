// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Unit tests for <see cref="IInMemoryCdcBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the ADR-098 Microsoft-style fluent builder pattern implementation
/// for the in-memory CDC provider (for testing scenarios).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCdcBuilderShould : UnitTestBase
{
	[Fact]
	public void UseInMemory_ThrowsOnNullBuilder()
	{
		// Arrange
		ICdcBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseInMemory());
	}

	[Fact]
	public void UseInMemory_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		ICdcBuilder? capturedResult = null;

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			capturedResult = builder.UseInMemory();
		});

		// Assert
		_ = capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_RegistersInMemoryCdcOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<InMemoryCdcOptions>>();
		_ = options.ShouldNotBeNull();
		_ = options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_RegistersIInMemoryCdcStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory();
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IInMemoryCdcStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseInMemory_RegistersIInMemoryCdcProcessor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory();
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IInMemoryCdcProcessor) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseInMemory_ConfiguresProcessorId()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		const string processorId = "test-processor";

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.ProcessorId(processorId);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.ProcessorId.ShouldBe(processorId);
	}

	[Fact]
	public void UseInMemory_ConfiguresBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.BatchSize(50);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.BatchSize.ShouldBe(50);
	}

	[Fact]
	public void UseInMemory_ConfiguresAutoFlush()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.AutoFlush(false);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.AutoFlush.ShouldBeFalse();
	}

	[Fact]
	public void UseInMemory_ConfiguresPreserveHistory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.PreserveHistory(true);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.PreserveHistory.ShouldBeTrue();
	}

	[Fact]
	public void UseInMemory_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.ProcessorId("test")
						.BatchSize(25)
						.AutoFlush(true)
						.PreserveHistory(false);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.ProcessorId.ShouldBe("test");
		options.Value.BatchSize.ShouldBe(25);
		options.Value.AutoFlush.ShouldBeTrue();
		options.Value.PreserveHistory.ShouldBeFalse();
	}

	[Fact]
	public void UseInMemory_CombinesWithCoreBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder
				.UseInMemory(inmemory =>
				{
					_ = inmemory.BatchSize(10);
				})
				.TrackTable("dbo.Orders", table =>
				{
					_ = table.MapAll<OrderChangedEvent>();
				})
				.WithRecovery(r => r.MaxAttempts(5))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert - InMemory options
		var inmemoryOptions = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		inmemoryOptions.Value.BatchSize.ShouldBe(10);

		// Assert - Core CDC options
		var cdcOptions = provider.GetRequiredService<IOptions<CdcOptions>>();
		cdcOptions.Value.TrackedTables.Count.ShouldBe(1);
		cdcOptions.Value.MaxRecoveryAttempts.ShouldBe(5);
		cdcOptions.Value.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void UseInMemory_WorksWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory();
		});
		var provider = services.BuildServiceProvider();

		// Assert - defaults are applied
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		_ = options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_ResolvesStoreFromFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory();
		});
		var provider = services.BuildServiceProvider();

		// Assert - actually resolve the store to execute the factory lambda
		var store = provider.GetRequiredService<IInMemoryCdcStore>();
		_ = store.ShouldNotBeNull();
		store.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public void UseInMemory_ResolvesProcessorFromFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory();
		});
		var provider = services.BuildServiceProvider();

		// Assert - actually resolve the processor to execute the factory lambda
		var processor = provider.GetRequiredService<IInMemoryCdcProcessor>();
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_WithConfigure_ResolvesStoreFromFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.BatchSize(25);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert - actually resolve the store to execute the factory lambda
		var store = provider.GetRequiredService<IInMemoryCdcStore>();
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_WithConfigure_ResolvesProcessorFromFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.ProcessorId("custom-id");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert - actually resolve the processor to execute the factory lambda
		var processor = provider.GetRequiredService<IInMemoryCdcProcessor>();
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_WithStore_ThrowsOnNullStore()
	{
		// Arrange
		var services = new ServiceCollection();
		IInMemoryCdcStore store = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UseInMemory(store);
			}));
	}

	// Test event type
	private sealed class OrderChangedEvent { }
}
