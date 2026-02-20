// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Snapshots;

using Excalibur.Data.Postgres;

namespace Excalibur.Data.Tests.Postgres.Snapshots;

/// <summary>
/// Unit tests for <see cref="PostgresSnapshotStoreOptions"/> configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostgresSnapshotStoreOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new PostgresSnapshotStoreOptions();

		// Assert
		options.SchemaName.ShouldBe("public");
		options.TableName.ShouldBe("snapshots");
	}

	[Fact]
	public void SchemaName_CanBeCustomized()
	{
		// Arrange & Act
		var options = new PostgresSnapshotStoreOptions
		{
			SchemaName = "event_store"
		};

		// Assert
		options.SchemaName.ShouldBe("event_store");
	}

	[Fact]
	public void TableName_CanBeCustomized()
	{
		// Arrange & Act
		var options = new PostgresSnapshotStoreOptions
		{
			TableName = "aggregate_snapshots"
		};

		// Assert
		options.TableName.ShouldBe("aggregate_snapshots");
	}

	[Fact]
	public void AllProperties_CanBeSetViaInitializer()
	{
		// Arrange & Act
		var options = new PostgresSnapshotStoreOptions
		{
			SchemaName = "custom_schema",
			TableName = "custom_snapshots"
		};

		// Assert
		options.SchemaName.ShouldBe("custom_schema");
		options.TableName.ShouldBe("custom_snapshots");
	}
}
