// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Npgsql;

namespace Excalibur.EventSourcing.Tests.Postgres.Builders;

/// <summary>
/// Unit tests for <see cref="PostgresEventSourcingBuilder"/> argument validation guards.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresEventSourcingBuilderValidationShould : UnitTestBase
{
	private static PostgresEventSourcingBuilder CreateBuilder() =>
		new(new PostgresEventSourcingOptions());

	// --- ConnectionString guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.ConnectionString(invalidValue!));
	}

	// --- DataSourceFactory guards ---

	[Fact]
	public void DataSourceFactory_ThrowOnNull()
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.DataSourceFactory(null!));
	}

	// --- DataSource guards ---

	[Fact]
	public void DataSource_ThrowOnNull()
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.DataSource(null!));
	}

	// --- ConnectionStringName guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.ConnectionStringName(invalidValue!));
	}

	// --- BindConfiguration guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
	}

	// --- Feature method guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void EventStoreSchema_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.EventStoreSchema(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void EventStoreTable_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.EventStoreTable(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SnapshotStoreSchema_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.SnapshotStoreSchema(invalidValue!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SnapshotStoreTable_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentException>(() => builder.SnapshotStoreTable(invalidValue!));
	}
}
