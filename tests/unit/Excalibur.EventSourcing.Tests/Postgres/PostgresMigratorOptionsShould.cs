// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.EventSourcing.Postgres.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Postgres;

/// <summary>
/// Unit tests for <see cref="PostgresMigratorOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 515: Migration infrastructure tests.
/// Tests verify Postgres migrator options defaults and property setters.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Migrations")]
[Trait("Feature", "Postgres")]
public sealed class PostgresMigratorOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullConnectionStringByDefault()
	{
		// Arrange & Act
		var options = new PostgresMigratorOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMigrationAssemblyByDefault()
	{
		// Arrange & Act
		var options = new PostgresMigratorOptions();

		// Assert
		options.MigrationAssembly.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMigrationNamespaceByDefault()
	{
		// Arrange & Act
		var options = new PostgresMigratorOptions();

		// Assert
		options.MigrationNamespace.ShouldBeNull();
	}

	[Fact]
	public void HaveAutoMigrateOnStartupDisabledByDefault()
	{
		// Arrange & Act
		var options = new PostgresMigratorOptions();

		// Assert
		options.AutoMigrateOnStartup.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=Test;Username=postgres;Password=secret";

		// Act
		var options = new PostgresMigratorOptions
		{
			ConnectionString = connectionString
		};

		// Assert
		options.ConnectionString.ShouldBe(connectionString);
	}

	[Fact]
	public void AllowSettingMigrationAssembly()
	{
		// Arrange
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		var options = new PostgresMigratorOptions
		{
			MigrationAssembly = assembly
		};

		// Assert
		options.MigrationAssembly.ShouldBe(assembly);
	}

	[Fact]
	public void AllowSettingMigrationNamespace()
	{
		// Arrange
		var migrationNamespace = "MyApp.Migrations";

		// Act
		var options = new PostgresMigratorOptions
		{
			MigrationNamespace = migrationNamespace
		};

		// Assert
		options.MigrationNamespace.ShouldBe(migrationNamespace);
	}

	[Fact]
	public void AllowEnablingAutoMigrateOnStartup()
	{
		// Act
		var options = new PostgresMigratorOptions
		{
			AutoMigrateOnStartup = true
		};

		// Assert
		options.AutoMigrateOnStartup.ShouldBeTrue();
	}

	#endregion

	#region Complex Configuration Tests

	[Fact]
	public void SupportFullConfiguration()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=EventStore;Username=postgres;Password=secret";
		var assembly = Assembly.GetExecutingAssembly();
		var migrationNamespace = "MyApp.Migrations";

		// Act
		var options = new PostgresMigratorOptions
		{
			ConnectionString = connectionString,
			MigrationAssembly = assembly,
			MigrationNamespace = migrationNamespace,
			AutoMigrateOnStartup = true
		};

		// Assert
		options.ConnectionString.ShouldBe(connectionString);
		options.MigrationAssembly.ShouldBe(assembly);
		options.MigrationNamespace.ShouldBe(migrationNamespace);
		options.AutoMigrateOnStartup.ShouldBeTrue();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(PostgresMigratorOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(PostgresMigratorOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
