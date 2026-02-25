// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="TemplateMigrationResult"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify migration result properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class TemplateMigrationResultShould
{
	#region Required Property Tests

	[Fact]
	public void IsSuccessful_IsRequired()
	{
		// Arrange & Act
		var result = new TemplateMigrationResult { IsSuccessful = true };

		// Assert
		result.IsSuccessful.ShouldBeTrue();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void Errors_DefaultsToEmptyCollection()
	{
		// Arrange & Act
		var result = new TemplateMigrationResult { IsSuccessful = true };

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Warnings_DefaultsToEmptyCollection()
	{
		// Arrange & Act
		var result = new TemplateMigrationResult { IsSuccessful = true };

		// Assert
		result.Warnings.ShouldBeEmpty();
	}

	#endregion

	#region Successful Migration Tests

	[Fact]
	public void SuccessfulMigration_HasNoErrorsOrWarnings()
	{
		// Arrange & Act
		var result = new TemplateMigrationResult
		{
			IsSuccessful = true
		};

		// Assert
		result.IsSuccessful.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
		result.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public void SuccessfulMigration_CanHaveWarnings()
	{
		// Arrange & Act
		var result = new TemplateMigrationResult
		{
			IsSuccessful = true,
			Warnings = ["Deprecated field removed", "Default value applied"]
		};

		// Assert
		result.IsSuccessful.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
		result.Warnings.Count().ShouldBe(2);
	}

	#endregion

	#region Failed Migration Tests

	[Fact]
	public void FailedMigration_HasErrors()
	{
		// Arrange & Act
		var result = new TemplateMigrationResult
		{
			IsSuccessful = false,
			Errors = ["Template not found", "Permission denied"]
		};

		// Assert
		result.IsSuccessful.ShouldBeFalse();
		result.Errors.Count().ShouldBe(2);
		result.Errors.ShouldContain("Template not found");
		result.Errors.ShouldContain("Permission denied");
	}

	[Fact]
	public void FailedMigration_CanHaveBothErrorsAndWarnings()
	{
		// Arrange & Act
		var result = new TemplateMigrationResult
		{
			IsSuccessful = false,
			Errors = ["Migration failed"],
			Warnings = ["Partial progress made"]
		};

		// Assert
		result.IsSuccessful.ShouldBeFalse();
		result.Errors.Count().ShouldBe(1);
		result.Warnings.Count().ShouldBe(1);
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		var errors = new[] { "Error 1", "Error 2" };
		var warnings = new[] { "Warning 1", "Warning 2", "Warning 3" };

		// Act
		var result = new TemplateMigrationResult
		{
			IsSuccessful = false,
			Errors = errors,
			Warnings = warnings
		};

		// Assert
		result.IsSuccessful.ShouldBeFalse();
		result.Errors.Count().ShouldBe(2);
		result.Warnings.Count().ShouldBe(3);
	}

	#endregion
}
