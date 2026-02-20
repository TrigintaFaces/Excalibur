// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="TemplateMigrationOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify migration options defaults and configurations.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class TemplateMigrationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void ValidateBeforeMigration_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new TemplateMigrationOptions();

		// Assert
		options.ValidateBeforeMigration.ShouldBeTrue();
	}

	[Fact]
	public void CreateBackup_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new TemplateMigrationOptions();

		// Assert
		options.CreateBackup.ShouldBeTrue();
	}

	[Fact]
	public void MigrationTimeout_DefaultsToFiveMinutes()
	{
		// Arrange & Act
		var options = new TemplateMigrationOptions();

		// Assert
		options.MigrationTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var options = new TemplateMigrationOptions
		{
			ValidateBeforeMigration = false,
			CreateBackup = false,
			MigrationTimeout = TimeSpan.FromMinutes(10)
		};

		// Assert
		options.ValidateBeforeMigration.ShouldBeFalse();
		options.CreateBackup.ShouldBeFalse();
		options.MigrationTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	#endregion

	#region Validation Option Tests

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void ValidateBeforeMigration_CanBeSetExplicitly(bool validate)
	{
		// Arrange & Act
		var options = new TemplateMigrationOptions
		{
			ValidateBeforeMigration = validate
		};

		// Assert
		options.ValidateBeforeMigration.ShouldBe(validate);
	}

	#endregion

	#region Backup Option Tests

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void CreateBackup_CanBeSetExplicitly(bool backup)
	{
		// Arrange & Act
		var options = new TemplateMigrationOptions
		{
			CreateBackup = backup
		};

		// Assert
		options.CreateBackup.ShouldBe(backup);
	}

	#endregion

	#region Timeout Tests

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(30)]
	public void MigrationTimeout_AcceptsVariousMinutes(int minutes)
	{
		// Arrange & Act
		var options = new TemplateMigrationOptions
		{
			MigrationTimeout = TimeSpan.FromMinutes(minutes)
		};

		// Assert
		options.MigrationTimeout.ShouldBe(TimeSpan.FromMinutes(minutes));
	}

	#endregion

	#region Safe Migration Configuration Tests

	[Fact]
	public void SafeMigration_HasDefaultSafeSettings()
	{
		// Arrange & Act - Default settings are safe
		var options = new TemplateMigrationOptions();

		// Assert
		options.ValidateBeforeMigration.ShouldBeTrue(); // Validate first
		options.CreateBackup.ShouldBeTrue(); // Create backup
		options.MigrationTimeout.ShouldBe(TimeSpan.FromMinutes(5)); // Reasonable timeout
	}

	#endregion

	#region Fast Migration Configuration Tests

	[Fact]
	public void FastMigration_SkipsValidationAndBackup()
	{
		// Arrange & Act - Fast but less safe configuration
		var options = new TemplateMigrationOptions
		{
			ValidateBeforeMigration = false,
			CreateBackup = false,
			MigrationTimeout = TimeSpan.FromMinutes(1)
		};

		// Assert
		options.ValidateBeforeMigration.ShouldBeFalse();
		options.CreateBackup.ShouldBeFalse();
		options.MigrationTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion
}
