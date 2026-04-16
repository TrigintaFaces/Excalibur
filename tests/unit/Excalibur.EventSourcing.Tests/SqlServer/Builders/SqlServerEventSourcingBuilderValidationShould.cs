// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="SqlServerEventSourcingBuilder"/> argument validation guards.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventSourcingBuilderValidationShould : UnitTestBase
{
	private static SqlServerEventSourcingBuilder CreateBuilder() =>
		new(new SqlServerEventSourcingOptions());

	// --- ConnectionString guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.ConnectionString(invalidValue!));
	}

	// --- ConnectionFactory guards ---

	[Fact]
	public void ConnectionFactory_ThrowOnNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.ConnectionFactory(null!));
	}

	// --- ConnectionStringName guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.ConnectionStringName(invalidValue!));
	}

	// --- BindConfiguration guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
	}

	// --- Feature method guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void EventStoreSchema_ThrowOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.EventStoreSchema(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void EventStoreTable_ThrowOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.EventStoreTable(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SnapshotStoreSchema_ThrowOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.SnapshotStoreSchema(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SnapshotStoreTable_ThrowOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.SnapshotStoreTable(invalidValue!));
	}
}
