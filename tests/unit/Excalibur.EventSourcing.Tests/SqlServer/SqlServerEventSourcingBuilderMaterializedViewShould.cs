// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
