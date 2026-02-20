// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="MigrationResult"/>.
/// </summary>
/// <remarks>
/// Sprint 515: Migration infrastructure tests.
/// Tests verify migration result creation and factory methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Migrations")]
[Trait("Feature", "Abstractions")]
public sealed class MigrationResultShould
{
	#region Factory Method Tests - Succeeded

	[Fact]
	public void Succeeded_CreateSuccessResult()
	{
		// Arrange
		var appliedMigrations = new List<AppliedMigration>
		{
			new()
			{
				MigrationId = "20260205120000_CreateEventsTable",
				AppliedAt = DateTimeOffset.UtcNow
			}
		};

		// Act
		var result = MigrationResult.Succeeded(appliedMigrations);

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public void Succeeded_IncludeAppliedMigrations()
	{
		// Arrange
		var appliedMigrations = new List<AppliedMigration>
		{
			new()
			{
				MigrationId = "20260205120000_CreateEventsTable",
				AppliedAt = DateTimeOffset.UtcNow
			},
			new()
			{
				MigrationId = "20260205130000_CreateSnapshotsTable",
				AppliedAt = DateTimeOffset.UtcNow
			}
		};

		// Act
		var result = MigrationResult.Succeeded(appliedMigrations);

		// Assert
		result.AppliedMigrations.Count.ShouldBe(2);
		result.AppliedMigrations[0].MigrationId.ShouldBe("20260205120000_CreateEventsTable");
		result.AppliedMigrations[1].MigrationId.ShouldBe("20260205130000_CreateSnapshotsTable");
	}

	[Fact]
	public void Succeeded_HaveNullErrorMessage()
	{
		// Arrange
		var appliedMigrations = new List<AppliedMigration>();

		// Act
		var result = MigrationResult.Succeeded(appliedMigrations);

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void Succeeded_HaveNullException()
	{
		// Arrange
		var appliedMigrations = new List<AppliedMigration>();

		// Act
		var result = MigrationResult.Succeeded(appliedMigrations);

		// Assert
		result.Exception.ShouldBeNull();
	}

	#endregion

	#region Factory Method Tests - NoMigrationsPending

	[Fact]
	public void NoMigrationsPending_CreateSuccessResult()
	{
		// Act
		var result = MigrationResult.NoMigrationsPending();

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public void NoMigrationsPending_HaveEmptyAppliedMigrations()
	{
		// Act
		var result = MigrationResult.NoMigrationsPending();

		// Assert
		result.AppliedMigrations.ShouldBeEmpty();
	}

	[Fact]
	public void NoMigrationsPending_HaveNullErrorMessage()
	{
		// Act
		var result = MigrationResult.NoMigrationsPending();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void NoMigrationsPending_HaveNullException()
	{
		// Act
		var result = MigrationResult.NoMigrationsPending();

		// Assert
		result.Exception.ShouldBeNull();
	}

	#endregion

	#region Factory Method Tests - Failed

	[Fact]
	public void Failed_CreateFailureResult()
	{
		// Act
		var result = MigrationResult.Failed("Migration failed");

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public void Failed_IncludeErrorMessage()
	{
		// Act
		var result = MigrationResult.Failed("Migration failed: table already exists");

		// Assert
		result.ErrorMessage.ShouldBe("Migration failed: table already exists");
	}

	[Fact]
	public void Failed_IncludeException_WhenProvided()
	{
		// Arrange
		var exception = new InvalidOperationException("Table already exists");

		// Act
		var result = MigrationResult.Failed("Migration failed", exception);

		// Assert
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void Failed_HaveNullException_WhenNotProvided()
	{
		// Act
		var result = MigrationResult.Failed("Migration failed");

		// Assert
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void Failed_HaveEmptyAppliedMigrations()
	{
		// Act
		var result = MigrationResult.Failed("Migration failed");

		// Assert
		result.AppliedMigrations.ShouldBeEmpty();
	}

	#endregion

	#region Default Property Tests

	[Fact]
	public void HaveEmptyAppliedMigrationsByDefault()
	{
		// Act
		var result = new MigrationResult { Success = true };

		// Assert
		result.AppliedMigrations.ShouldBeEmpty();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(MigrationResult).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BeRecord()
	{
		// Assert - Records are classes
		typeof(MigrationResult).IsClass.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(MigrationResult).IsPublic.ShouldBeTrue();
	}

	#endregion
}
