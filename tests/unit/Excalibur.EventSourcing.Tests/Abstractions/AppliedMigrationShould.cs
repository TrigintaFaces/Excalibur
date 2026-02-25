// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="AppliedMigration"/>.
/// </summary>
/// <remarks>
/// Sprint 515: Migration infrastructure tests.
/// Tests verify applied migration record creation and properties.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Migrations")]
[Trait("Feature", "Abstractions")]
public sealed class AppliedMigrationShould
{
	#region Creation Tests

	[Fact]
	public void RequireMigrationId()
	{
		// Arrange
		var migrationId = "20260205120000_CreateEventsTable";
		var appliedAt = DateTimeOffset.UtcNow;

		// Act
		var migration = new AppliedMigration
		{
			MigrationId = migrationId,
			AppliedAt = appliedAt
		};

		// Assert
		migration.MigrationId.ShouldBe(migrationId);
	}

	[Fact]
	public void RequireAppliedAt()
	{
		// Arrange
		var migrationId = "20260205120000_CreateEventsTable";
		var appliedAt = DateTimeOffset.UtcNow;

		// Act
		var migration = new AppliedMigration
		{
			MigrationId = migrationId,
			AppliedAt = appliedAt
		};

		// Assert
		migration.AppliedAt.ShouldBe(appliedAt);
	}

	[Fact]
	public void HaveNullDescriptionByDefault()
	{
		// Arrange
		var migration = new AppliedMigration
		{
			MigrationId = "20260205120000_CreateEventsTable",
			AppliedAt = DateTimeOffset.UtcNow
		};

		// Assert
		migration.Description.ShouldBeNull();
	}

	[Fact]
	public void HaveNullChecksumByDefault()
	{
		// Arrange
		var migration = new AppliedMigration
		{
			MigrationId = "20260205120000_CreateEventsTable",
			AppliedAt = DateTimeOffset.UtcNow
		};

		// Assert
		migration.Checksum.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingDescription()
	{
		// Arrange
		var description = "Creates the events table for event sourcing";

		// Act
		var migration = new AppliedMigration
		{
			MigrationId = "20260205120000_CreateEventsTable",
			AppliedAt = DateTimeOffset.UtcNow,
			Description = description
		};

		// Assert
		migration.Description.ShouldBe(description);
	}

	[Fact]
	public void AllowSettingChecksum()
	{
		// Arrange
		var checksum = "ABC123DEF456";

		// Act
		var migration = new AppliedMigration
		{
			MigrationId = "20260205120000_CreateEventsTable",
			AppliedAt = DateTimeOffset.UtcNow,
			Checksum = checksum
		};

		// Assert
		migration.Checksum.ShouldBe(checksum);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void BeEqualWhenAllPropertiesMatch()
	{
		// Arrange
		var appliedAt = DateTimeOffset.UtcNow;
		var migration1 = new AppliedMigration
		{
			MigrationId = "20260205120000_CreateEventsTable",
			AppliedAt = appliedAt,
			Description = "Test",
			Checksum = "ABC123"
		};
		var migration2 = new AppliedMigration
		{
			MigrationId = "20260205120000_CreateEventsTable",
			AppliedAt = appliedAt,
			Description = "Test",
			Checksum = "ABC123"
		};

		// Assert
		migration1.ShouldBe(migration2);
	}

	[Fact]
	public void NotBeEqualWhenMigrationIdDiffers()
	{
		// Arrange
		var appliedAt = DateTimeOffset.UtcNow;
		var migration1 = new AppliedMigration
		{
			MigrationId = "20260205120000_CreateEventsTable",
			AppliedAt = appliedAt
		};
		var migration2 = new AppliedMigration
		{
			MigrationId = "20260205130000_CreateSnapshotsTable",
			AppliedAt = appliedAt
		};

		// Assert
		migration1.ShouldNotBe(migration2);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(AppliedMigration).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BeRecord()
	{
		// Assert - Records are classes
		typeof(AppliedMigration).IsClass.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(AppliedMigration).IsPublic.ShouldBeTrue();
	}

	#endregion
}
