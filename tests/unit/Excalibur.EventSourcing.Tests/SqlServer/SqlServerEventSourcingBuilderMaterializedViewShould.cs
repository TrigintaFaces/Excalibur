// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Tests for <see cref="SqlServerEventSourcingBuilder"/> UseMaterializedViewStore methods.
/// Covers builder state, fluent chaining, null guards, and DI registration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerEventSourcingBuilderMaterializedViewShould
{
	private static SqlServerEventSourcingBuilder CreateBuilder()
	{
		return new SqlServerEventSourcingBuilder(new SqlServerEventSourcingOptions());
	}

	// ═══════════════════════════════════════════════════
	// UseMaterializedViewStore() — default overload
	// ═══════════════════════════════════════════════════

	[Fact]
	public void EnableMaterializedViewStoreWithDefaults()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseMaterializedViewStore();

		// Assert
		builder.EnableMaterializedViewStore.ShouldBeTrue();
		builder.MaterializedViewTableName.ShouldBeNull(); // defaults applied at DI registration
		builder.MaterializedViewPositionTableName.ShouldBeNull();
	}

	[Fact]
	public void ReturnBuilderForFluentChaining_DefaultOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseMaterializedViewStore();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	// ═══════════════════════════════════════════════════
	// UseMaterializedViewStore(string, string) — custom overload
	// ═══════════════════════════════════════════════════

	[Fact]
	public void EnableMaterializedViewStoreWithCustomTableNames()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseMaterializedViewStore("MyViews", "MyPositions");

		// Assert
		builder.EnableMaterializedViewStore.ShouldBeTrue();
		builder.MaterializedViewTableName.ShouldBe("MyViews");
		builder.MaterializedViewPositionTableName.ShouldBe("MyPositions");
	}

	[Fact]
	public void ReturnBuilderForFluentChaining_CustomOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseMaterializedViewStore("V", "P");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ThrowArgumentException_WhenViewTableNameIsNull()
	{
		var builder = CreateBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseMaterializedViewStore(null!, "Positions"));
	}

	[Fact]
	public void ThrowArgumentException_WhenViewTableNameIsWhitespace()
	{
		var builder = CreateBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseMaterializedViewStore("  ", "Positions"));
	}

	[Fact]
	public void ThrowArgumentException_WhenPositionTableNameIsNull()
	{
		var builder = CreateBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseMaterializedViewStore("Views", null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenPositionTableNameIsWhitespace()
	{
		var builder = CreateBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseMaterializedViewStore("Views", "  "));
	}

	// ═══════════════════════════════════════════════════
	// Last-wins semantics
	// ═══════════════════════════════════════════════════

	[Fact]
	public void CustomOverloadClearsTableNames_WhenDefaultOverloadCalledAfter()
	{
		// Arrange — custom first, then default (last-wins)
		var builder = CreateBuilder();

		// Act
		builder.UseMaterializedViewStore("CustomViews", "CustomPositions");
		builder.UseMaterializedViewStore(); // default overload resets table names

		// Assert
		builder.EnableMaterializedViewStore.ShouldBeTrue();
		builder.MaterializedViewTableName.ShouldBeNull();
		builder.MaterializedViewPositionTableName.ShouldBeNull();
	}

	[Fact]
	public void DefaultOverloadOverriddenByCustom()
	{
		// Arrange — default first, then custom
		var builder = CreateBuilder();

		// Act
		builder.UseMaterializedViewStore();
		builder.UseMaterializedViewStore("OverriddenViews", "OverriddenPositions");

		// Assert
		builder.EnableMaterializedViewStore.ShouldBeTrue();
		builder.MaterializedViewTableName.ShouldBe("OverriddenViews");
		builder.MaterializedViewPositionTableName.ShouldBe("OverriddenPositions");
	}

	// ═══════════════════════════════════════════════════
	// Integration with UseSqlServer builder chain
	// ═══════════════════════════════════════════════════

	[Fact]
	public void FullFluentChainWithMaterializedViewStore()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act — full chain as a consumer would write it
		var result = builder
			.ConnectionString("Server=localhost;Database=EventStore")
			.EventStoreSchema("es")
			.EventStoreTable("Events")
			.SnapshotStoreSchema("es")
			.SnapshotStoreTable("Snapshots")
			.UseMaterializedViewStore("MatViews", "MatPositions");

		// Assert
		result.ShouldBeSameAs(builder);
		builder.EnableMaterializedViewStore.ShouldBeTrue();
		builder.MaterializedViewTableName.ShouldBe("MatViews");
		builder.MaterializedViewPositionTableName.ShouldBe("MatPositions");
	}

	// ═══════════════════════════════════════════════════
	// DI registration via UseSqlServer
	// ═══════════════════════════════════════════════════

	[Fact]
	public void RegisterIMaterializedViewStore_WhenUseMaterializedViewStoreEnabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Minimal IEventSourcingBuilder stub — we need to verify DI registration
		services.AddLogging();
		services.AddOptions();

		// Act — use the real extension method chain
		var esBuilder = new Excalibur.EventSourcing.DependencyInjection.ExcaliburEventSourcingBuilder(services);
		esBuilder.UseSqlServer(sql =>
		{
			sql.ConnectionString("Server=localhost;Database=EventStore;TrustServerCertificate=True")
			   .UseMaterializedViewStore();
		});

		// Assert — IMaterializedViewStore should be registered
		var descriptor = services.FirstOrDefault(
			s => s.ServiceType == typeof(Excalibur.EventSourcing.IMaterializedViewStore));
		descriptor.ShouldNotBeNull();
		descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void CarryTheSchemaFromTheBuilderIntoTheRegisteredStore()
	{
		// Arrange -- the builder facade is the only surface a consumer who configures through UseSqlServer
		// has. A schemaName it accepts and drops would leave every statement on [dbo], which is exactly the
		// defect a signature-only test cannot see.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddOptions();

		// Act
		var esBuilder = new Excalibur.EventSourcing.DependencyInjection.ExcaliburEventSourcingBuilder(services);
		esBuilder.UseSqlServer(sql =>
		{
			sql.ConnectionString("Server=localhost;Database=EventStore;TrustServerCertificate=True")
			   .UseMaterializedViewStore("MatViews", "MatPositions", "reporting");
		});

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<Excalibur.EventSourcing.IMaterializedViewStore>();

		// Assert -- both tables the store addresses carry the schema the builder was given.
		Qualified(store, "_qualifiedViewTable").ShouldBe("[reporting].[MatViews]");
		Qualified(store, "_qualifiedPositionTable").ShouldBe("[reporting].[MatPositions]");
	}

	[Fact]
	public void DefaultTheSchemaToDbo_WhenTheBuilderDoesNotSetIt()
	{
		// Arrange -- the liveness half: threading the parameter must not move a consumer who omits it.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddOptions();

		// Act
		var esBuilder = new Excalibur.EventSourcing.DependencyInjection.ExcaliburEventSourcingBuilder(services);
		esBuilder.UseSqlServer(sql =>
		{
			sql.ConnectionString("Server=localhost;Database=EventStore;TrustServerCertificate=True")
			   .UseMaterializedViewStore("MatViews", "MatPositions");
		});

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<Excalibur.EventSourcing.IMaterializedViewStore>();

		// Assert
		Qualified(store, "_qualifiedViewTable").ShouldBe("[dbo].[MatViews]");
		Qualified(store, "_qualifiedPositionTable").ShouldBe("[dbo].[MatPositions]");
	}

	private static string Qualified(object store, string fieldName) =>
		(string)store.GetType()
			.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(store)!;

	[Fact]
	public void NotRegisterIMaterializedViewStore_WhenUseMaterializedViewStoreNotCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddOptions();

		// Act — no UseMaterializedViewStore call
		var esBuilder = new Excalibur.EventSourcing.DependencyInjection.ExcaliburEventSourcingBuilder(services);
		esBuilder.UseSqlServer(sql =>
		{
			sql.ConnectionString("Server=localhost;Database=EventStore;TrustServerCertificate=True");
		});

		// Assert — IMaterializedViewStore should NOT be registered
		var descriptor = services.FirstOrDefault(
			s => s.ServiceType == typeof(Excalibur.EventSourcing.IMaterializedViewStore));
		descriptor.ShouldBeNull();
	}
}
