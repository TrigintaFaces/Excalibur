// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Unit tests for <see cref="CdcBuilderInMemoryExtensions"/>.
/// Tests the UseInMemory extension methods with pre-configured stores.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcBuilderInMemoryExtensionsShould : UnitTestBase
{
	#region UseInMemory with Store Tests

	[Fact]
	public void UseInMemory_WithStore_RegistersProvidedStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var store = new InMemoryCdcStore();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(store);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var resolvedStore = provider.GetService<IInMemoryCdcStore>();
		resolvedStore.ShouldBeSameAs(store);
	}

	[Fact]
	public void UseInMemory_WithStore_RegistersProcessor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var store = new InMemoryCdcStore();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(store);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var processor = provider.GetService<IInMemoryCdcProcessor>();
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemory_WithStore_AllowsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var store = new InMemoryCdcStore();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(store, inmemory =>
			{
				_ = inmemory.BatchSize(25)
						.ProcessorId("custom-processor")
						.PreserveHistory(true);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.BatchSize.ShouldBe(25);
		options.Value.ProcessorId.ShouldBe("custom-processor");
		options.Value.PreserveHistory.ShouldBeTrue();
	}

	[Fact]
	public void UseInMemory_WithStore_ProcessorUsesProvidedStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var store = new InMemoryCdcStore();
		store.AddChange(InMemoryCdcChange.Insert("dbo.Test", new CdcDataChange { ColumnName = "Id", NewValue = 1 }));

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(store);
		});
		var provider = services.BuildServiceProvider();

		// Assert - the store has our change
		var resolvedStore = provider.GetRequiredService<IInMemoryCdcStore>();
		resolvedStore.GetPendingCount().ShouldBe(1);
	}

	[Fact]
	public void UseInMemory_WithStore_ThrowsOnNullBuilder()
	{
		// Arrange
		ICdcBuilder builder = null!;
		var store = new InMemoryCdcStore();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseInMemory(store));
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

	[Fact]
	public void UseInMemory_WithStore_WorksWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var store = new InMemoryCdcStore();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(store);
		});
		var provider = services.BuildServiceProvider();

		// Assert - defaults are applied
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.ProcessorId.ShouldBe("inmemory-cdc");
		options.Value.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void UseInMemory_WithStore_ValidatesOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var store = new InMemoryCdcStore();

		// Act & Assert - should throw due to invalid batch size
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UseInMemory(store, inmemory =>
				{
					_ = inmemory.BatchSize(0);
				});
			}));
	}

	#endregion

	#region UseInMemory Integration Tests

	[Fact]
	public async Task UseInMemory_ProcessorCanProcessChangesFromStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var store = new InMemoryCdcStore();
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }));
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 }));

		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(store, inmemory =>
			{
				_ = inmemory.BatchSize(10);
			});
		});

		var provider = services.BuildServiceProvider();
		var processor = provider.GetRequiredService<IInMemoryCdcProcessor>();

		var processedIds = new List<object?>();

		// Act
		var count = await processor.ProcessChangesAsync((change, _) =>
		{
			processedIds.Add(change.Changes[0].NewValue);
			return Task.CompletedTask;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		count.ShouldBe(2);
		processedIds.ShouldContain(1);
		processedIds.ShouldContain(2);
	}

	[Fact]
	public void UseInMemory_ChainsWithOtherBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var store = new InMemoryCdcStore();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder
				.UseInMemory(store, inmemory =>
				{
					_ = inmemory.BatchSize(50);
				})
				.TrackTable("dbo.Orders", table =>
				{
					_ = table.MapAll<OrderChangedEvent>();
				})
				.WithRecovery(r => r.MaxAttempts(3))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert - InMemory options
		var inmemoryOptions = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		inmemoryOptions.Value.BatchSize.ShouldBe(50);

		// Assert - Core CDC options
		var cdcOptions = provider.GetRequiredService<IOptions<CdcOptions>>();
		cdcOptions.Value.TrackedTables.Count.ShouldBe(1);
		cdcOptions.Value.MaxRecoveryAttempts.ShouldBe(3);
		cdcOptions.Value.EnableBackgroundProcessing.ShouldBeTrue();
	}

	#endregion

	// Test event type
	private sealed class OrderChangedEvent { }
}
