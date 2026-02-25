// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.EventSourcing.SqlServer.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerMigratorOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 515: Migration infrastructure tests.
/// Tests verify SQL Server migrator options defaults and property setters.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Migrations")]
[Trait("Feature", "SqlServer")]
public sealed class SqlServerMigratorOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullConnectionStringByDefault()
	{
		// Arrange & Act
		var options = new SqlServerMigratorOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMigrationAssemblyByDefault()
	{
		// Arrange & Act
		var options = new SqlServerMigratorOptions();

		// Assert
		options.MigrationAssembly.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMigrationNamespaceByDefault()
	{
		// Arrange & Act
		var options = new SqlServerMigratorOptions();

		// Assert
		options.MigrationNamespace.ShouldBeNull();
	}

	[Fact]
	public void HaveAutoMigrateOnStartupDisabledByDefault()
	{
		// Arrange & Act
		var options = new SqlServerMigratorOptions();

		// Assert
		options.AutoMigrateOnStartup.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=Test;Trusted_Connection=true;";

		// Act
		var options = new SqlServerMigratorOptions
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
		var options = new SqlServerMigratorOptions
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
		var options = new SqlServerMigratorOptions
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
		var options = new SqlServerMigratorOptions
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
		var connectionString = "Server=localhost;Database=EventStore;Trusted_Connection=true;";
		var assembly = Assembly.GetExecutingAssembly();
		var migrationNamespace = "MyApp.Migrations";

		// Act
		var options = new SqlServerMigratorOptions
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
		typeof(SqlServerMigratorOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(SqlServerMigratorOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
