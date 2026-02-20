// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexRolloverResult"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify rollover result properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexRolloverResultShould
{
	#region Required Property Tests

	[Fact]
	public void IsSuccessful_IsRequired()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = true
		};

		// Assert
		result.IsSuccessful.ShouldBeTrue();
	}

	[Fact]
	public void RolledOver_IsRequired()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = false
		};

		// Assert
		result.RolledOver.ShouldBeFalse();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void OldIndex_DefaultsToNull()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = true
		};

		// Assert
		result.OldIndex.ShouldBeNull();
	}

	[Fact]
	public void NewIndex_DefaultsToNull()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = true
		};

		// Assert
		result.NewIndex.ShouldBeNull();
	}

	[Fact]
	public void Errors_DefaultsToEmptyCollection()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = true
		};

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	#endregion

	#region Successful Rollover Tests

	[Fact]
	public void SuccessfulRollover_HasCorrectProperties()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = true,
			OldIndex = "events-000001",
			NewIndex = "events-000002"
		};

		// Assert
		result.IsSuccessful.ShouldBeTrue();
		result.RolledOver.ShouldBeTrue();
		result.OldIndex.ShouldBe("events-000001");
		result.NewIndex.ShouldBe("events-000002");
		result.Errors.ShouldBeEmpty();
	}

	#endregion

	#region Conditions Not Met Tests

	[Fact]
	public void ConditionsNotMet_RolledOverIsFalse()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = false,
			OldIndex = "events-000001"
		};

		// Assert
		result.IsSuccessful.ShouldBeTrue();
		result.RolledOver.ShouldBeFalse();
		result.NewIndex.ShouldBeNull();
	}

	#endregion

	#region Failed Rollover Tests

	[Fact]
	public void FailedRollover_HasErrors()
	{
		// Arrange
		var errors = new[] { "Index not found", "Permission denied" };

		// Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = false,
			RolledOver = false,
			OldIndex = "events-000001",
			Errors = errors
		};

		// Assert
		result.IsSuccessful.ShouldBeFalse();
		result.RolledOver.ShouldBeFalse();
		result.Errors.ShouldBe(errors);
	}

	[Fact]
	public void FailedRollover_NoNewIndex()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = false,
			RolledOver = false,
			Errors = ["Connection timeout"]
		};

		// Assert
		result.NewIndex.ShouldBeNull();
		result.Errors.Count().ShouldBe(1);
	}

	#endregion

	#region Index Naming Pattern Tests

	[Theory]
	[InlineData("events-000001", "events-000002")]
	[InlineData("logs-2026.02.01", "logs-2026.02.02")]
	[InlineData("metrics-hot-1", "metrics-hot-2")]
	public void IndexNames_SupportVariousPatterns(string oldIndex, string newIndex)
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = true,
			OldIndex = oldIndex,
			NewIndex = newIndex
		};

		// Assert
		result.OldIndex.ShouldBe(oldIndex);
		result.NewIndex.ShouldBe(newIndex);
	}

	#endregion
}
