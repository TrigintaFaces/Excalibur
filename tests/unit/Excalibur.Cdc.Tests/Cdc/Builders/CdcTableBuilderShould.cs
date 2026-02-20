// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ICdcTableBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the table tracking configuration for CDC.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcTableBuilderShould : UnitTestBase
{
	[Fact]
	public void MapInsert_RegistersInsertEventMapping()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.MapInsert<OrderCreatedEvent>();
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Insert);
		tableConfig.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(OrderCreatedEvent));
	}

	[Fact]
	public void MapUpdate_RegistersUpdateEventMapping()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.MapUpdate<OrderUpdatedEvent>();
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Update);
		tableConfig.EventMappings[CdcChangeType.Update].ShouldBe(typeof(OrderUpdatedEvent));
	}

	[Fact]
	public void MapDelete_RegistersDeleteEventMapping()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.MapDelete<OrderDeletedEvent>();
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Delete);
		tableConfig.EventMappings[CdcChangeType.Delete].ShouldBe(typeof(OrderDeletedEvent));
	}

	[Fact]
	public void MapAll_RegistersAllEventMappings()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.MapAll<OrderChangedEvent>();
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Insert);
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Update);
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Delete);
		tableConfig.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(OrderChangedEvent));
		tableConfig.EventMappings[CdcChangeType.Update].ShouldBe(typeof(OrderChangedEvent));
		tableConfig.EventMappings[CdcChangeType.Delete].ShouldBe(typeof(OrderChangedEvent));
	}

	[Fact]
	public void MapAll_CanBeCombinedWithSpecificMappings()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.MapInsert<OrderCreatedEvent>()
					 .MapAll<OrderChangedEvent>(); // This should override Insert
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		// MapAll should have overwritten the Insert mapping
		tableConfig.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(OrderChangedEvent));
		tableConfig.EventMappings[CdcChangeType.Update].ShouldBe(typeof(OrderChangedEvent));
		tableConfig.EventMappings[CdcChangeType.Delete].ShouldBe(typeof(OrderChangedEvent));
	}

	[Fact]
	public void TableBuilder_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.MapInsert<OrderCreatedEvent>()
					 .MapUpdate<OrderUpdatedEvent>()
					 .MapDelete<OrderDeletedEvent>();
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.EventMappings.Count.ShouldBe(3);
		tableConfig.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(OrderCreatedEvent));
		tableConfig.EventMappings[CdcChangeType.Update].ShouldBe(typeof(OrderUpdatedEvent));
		tableConfig.EventMappings[CdcChangeType.Delete].ShouldBe(typeof(OrderDeletedEvent));
	}

	[Fact]
	public void CaptureInstance_ThrowsOnNullName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.TrackTable("dbo.Orders", table =>
				{
					_ = table.CaptureInstance(null!);
				});
			}));
	}

	[Fact]
	public void CaptureInstance_ThrowsOnEmptyName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.TrackTable("dbo.Orders", table =>
				{
					_ = table.CaptureInstance("");
				});
			}));
	}

	[Fact]
	public void CaptureInstance_ThrowsOnWhitespaceName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.TrackTable("dbo.Orders", table =>
				{
					_ = table.CaptureInstance("   ");
				});
			}));
	}

	[Fact]
	public void CaptureInstance_SetsCaptureName()
	{
		// Arrange
		var services = new ServiceCollection();
		const string captureInstance = "dbo_Orders_Audit";

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.CaptureInstance(captureInstance);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.CaptureInstance.ShouldBe(captureInstance);
	}

	[Fact]
	public void WithFilter_ThrowsOnNullPredicate()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.TrackTable("dbo.Orders", table =>
				{
					_ = table.WithFilter(null!);
				});
			}));
	}

	[Fact]
	public void WithFilter_SetsFilterPredicate()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<CdcDataChange, bool> filter = change => change.ColumnName != "UpdatedAt";

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.WithFilter(filter);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		_ = tableConfig.Filter.ShouldNotBeNull();
	}

	[Fact]
	public void TableBuilder_FullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", table =>
			{
				_ = table.MapInsert<OrderCreatedEvent>()
					 .MapUpdate<OrderUpdatedEvent>()
					 .MapDelete<OrderDeletedEvent>()
					 .CaptureInstance("dbo_Orders_CT")
					 .WithFilter(change => true);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = options.Value.TrackedTables.Single();
		tableConfig.TableName.ShouldBe("dbo.Orders");
		tableConfig.CaptureInstance.ShouldBe("dbo_Orders_CT");
		tableConfig.EventMappings.Count.ShouldBe(3);
		_ = tableConfig.Filter.ShouldNotBeNull();
	}

	// Test event types
	private sealed class OrderCreatedEvent { }
	private sealed class OrderUpdatedEvent { }
	private sealed class OrderDeletedEvent { }
	private sealed class OrderChangedEvent { }
}
