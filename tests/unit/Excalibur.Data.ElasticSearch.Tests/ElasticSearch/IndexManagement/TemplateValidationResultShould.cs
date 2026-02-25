// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="TemplateValidationResult"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify validation result properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class TemplateValidationResultShould
{
	#region Required Property Tests

	[Fact]
	public void IsValid_IsRequired()
	{
		// Arrange & Act
		var result = new TemplateValidationResult { IsValid = true };

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void Errors_DefaultsToEmptyCollection()
	{
		// Arrange & Act
		var result = new TemplateValidationResult { IsValid = true };

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	#endregion

	#region Valid Template Tests

	[Fact]
	public void ValidTemplate_HasNoErrors()
	{
		// Arrange & Act
		var result = new TemplateValidationResult
		{
			IsValid = true
		};

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	#endregion

	#region Invalid Template Tests

	[Fact]
	public void InvalidTemplate_HasErrors()
	{
		// Arrange & Act
		var result = new TemplateValidationResult
		{
			IsValid = false,
			Errors = ["Invalid mapping type", "Unknown field type"]
		};

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count().ShouldBe(2);
		result.Errors.ShouldContain("Invalid mapping type");
		result.Errors.ShouldContain("Unknown field type");
	}

	[Fact]
	public void InvalidTemplate_CanHaveSingleError()
	{
		// Arrange & Act
		var result = new TemplateValidationResult
		{
			IsValid = false,
			Errors = ["Template name is required"]
		};

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count().ShouldBe(1);
	}

	[Fact]
	public void InvalidTemplate_CanHaveMultipleErrors()
	{
		// Arrange & Act
		var result = new TemplateValidationResult
		{
			IsValid = false,
			Errors = [
				"Missing required field: index_patterns",
				"Invalid shard count: -1",
				"Unknown analyzer: custom_analyzer",
				"Duplicate field mapping: timestamp"
			]
		};

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count().ShouldBe(4);
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		var errors = new[] { "Error 1", "Error 2" };

		// Act
		var result = new TemplateValidationResult
		{
			IsValid = false,
			Errors = errors
		};

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count().ShouldBe(2);
	}

	#endregion
}
