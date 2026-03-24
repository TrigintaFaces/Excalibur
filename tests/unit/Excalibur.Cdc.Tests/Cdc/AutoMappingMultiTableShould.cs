// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

namespace Excalibur.Cdc.Tests.Cdc;

/// <summary>
/// Tests for multi-table auto-mapping registration after TryAddEnumerable fix.
/// Validates that each table with <c>HasEventMappers</c> gets its own
/// <see cref="IDataChangeHandler"/> registered via <c>AddSingleton</c>
/// instead of <c>TryAddEnumerable</c> (which treated factory-delegate registrations
/// as indistinguishable).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AutoMappingMultiTableShould : UnitTestBase
{
	private const string ConnectionString =
		"Server=localhost;Database=TestDb;Encrypt=false;TrustServerCertificate=true";

	[Fact]
	public void RegisterTwoHandlers_WhenTwoTablesHaveDifferentMappers()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.UseSqlServer(sql => sql.ConnectionString(ConnectionString).DatabaseName("Db"))
				.TrackTable("dbo.Orders", t => t.MapInsert<OrderEvent, OrderMapper>())
				.TrackTable("dbo.Products", t => t.MapInsert<ProductEvent, ProductMapper>());
		});

		// Assert
		var handlerCount = services.Count(sd => sd.ServiceType == typeof(IDataChangeHandler));
		handlerCount.ShouldBe(2);
	}

	[Fact]
	public void RegisterThreeHandlers_WhenThreeTablesHaveMappers()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.UseSqlServer(sql => sql.ConnectionString(ConnectionString).DatabaseName("Db"))
				.TrackTable("dbo.Orders", t => t.MapInsert<OrderEvent, OrderMapper>())
				.TrackTable("dbo.Products", t => t.MapInsert<ProductEvent, ProductMapper>())
				.TrackTable("dbo.Customers", t => t.MapDelete<OrderEvent, OrderMapper>());
		});

		// Assert
		var handlerCount = services.Count(sd => sd.ServiceType == typeof(IDataChangeHandler));
		handlerCount.ShouldBe(3);
	}

	[Fact]
	public void RegisterOnlyOneHandler_WhenOneTableHasMapper_AndOneTableUsesOldMapInsert()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- MapInsert<T>() (no mapper) does NOT set HasEventMappers
		services.AddCdcProcessor(cdc =>
		{
			cdc.UseSqlServer(sql => sql.ConnectionString(ConnectionString).DatabaseName("Db"))
				.TrackTable("dbo.Orders", t => t.MapInsert<OrderEvent, OrderMapper>())
				.TrackTable("dbo.Products", t => t.MapInsert<ProductEvent>());
		});

		// Assert
		var handlerCount = services.Count(sd => sd.ServiceType == typeof(IDataChangeHandler));
		handlerCount.ShouldBe(1);
	}

	[Fact]
	public void RegisterSingleHandler_ForSingleTableWithMapper()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.UseSqlServer(sql => sql.ConnectionString(ConnectionString).DatabaseName("Db"))
				.TrackTable("dbo.Orders", t => t.MapInsert<OrderEvent, OrderMapper>());
		});

		// Assert
		var handlerCount = services.Count(sd => sd.ServiceType == typeof(IDataChangeHandler));
		handlerCount.ShouldBe(1);
	}

	[Fact]
	public void RegisterZeroAutoMappingHandlers_WhenNoTablesHaveEventMappers()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- old-style MapInsert<T>() without mapper type
		services.AddCdcProcessor(cdc =>
		{
			cdc.UseSqlServer(sql => sql.ConnectionString(ConnectionString).DatabaseName("Db"))
				.TrackTable("dbo.Orders", t => t.MapInsert<OrderEvent>())
				.TrackTable("dbo.Products", t => t.MapUpdate<ProductEvent>());
		});

		// Assert
		var handlerCount = services.Count(sd => sd.ServiceType == typeof(IDataChangeHandler));
		handlerCount.ShouldBe(0);
	}

	[Fact]
	public void RegisterBothManualAndAutoMappingHandlers_WhenBothConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var manualHandler = A.Fake<IDataChangeHandler>();

		// Register a manual handler first
		services.AddSingleton(manualHandler);

		// Act -- add auto-mapping for a different table
		services.AddCdcProcessor(cdc =>
		{
			cdc.UseSqlServer(sql => sql.ConnectionString(ConnectionString).DatabaseName("Db"))
				.TrackTable("dbo.Orders", t => t.MapInsert<OrderEvent, OrderMapper>());
		});

		// Assert -- 1 manual + 1 auto-mapping = 2
		var handlerCount = services.Count(sd => sd.ServiceType == typeof(IDataChangeHandler));
		handlerCount.ShouldBe(2);
	}

	[Fact]
	public void RegisterHandlerPerTable_NotPerChangeType_WhenTableHasMultipleMappers()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- one table with insert+update+delete mappers should still be ONE handler
		services.AddCdcProcessor(cdc =>
		{
			cdc.UseSqlServer(sql => sql.ConnectionString(ConnectionString).DatabaseName("Db"))
				.TrackTable("dbo.Orders", t =>
					t.MapInsert<OrderEvent, OrderMapper>()
					 .MapUpdate<OrderEvent, OrderMapper>()
					 .MapDelete<OrderEvent, OrderMapper>());
		});

		// Assert -- one handler per table, not per change type
		var handlerCount = services.Count(sd => sd.ServiceType == typeof(IDataChangeHandler));
		handlerCount.ShouldBe(1);
	}

	[Fact]
	public void RegisterAllAutoMappingHandlersAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.UseSqlServer(sql => sql.ConnectionString(ConnectionString).DatabaseName("Db"))
				.TrackTable("dbo.Orders", t => t.MapInsert<OrderEvent, OrderMapper>())
				.TrackTable("dbo.Products", t => t.MapInsert<ProductEvent, ProductMapper>());
		});

		// Assert -- all auto-mapping handlers should be registered as Singleton
		var handlerDescriptors = services
			.Where(sd => sd.ServiceType == typeof(IDataChangeHandler))
			.ToList();

		handlerDescriptors.Count.ShouldBe(2);
		handlerDescriptors.ShouldAllBe(sd => sd.Lifetime == ServiceLifetime.Singleton);
	}

	// ---- Test Infrastructure ----

	private sealed class OrderEvent
	{
		public int Id { get; init; }
	}

	private sealed class ProductEvent
	{
		public int Id { get; init; }
	}

	private sealed class OrderMapper : ICdcEventMapper<OrderEvent>
	{
		public OrderEvent Map(IReadOnlyList<CdcDataChange> changes, CdcChangeType changeType)
			=> new() { Id = changes.GetValue<int>("Id") };
	}

	private sealed class ProductMapper : ICdcEventMapper<ProductEvent>
	{
		public ProductEvent Map(IReadOnlyList<CdcDataChange> changes, CdcChangeType changeType)
			=> new() { Id = changes.GetValue<int>("Id") };
	}
}
