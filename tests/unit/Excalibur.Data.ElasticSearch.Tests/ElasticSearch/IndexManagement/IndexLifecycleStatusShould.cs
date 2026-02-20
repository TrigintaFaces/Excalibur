// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexLifecycleStatus"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify property initialization and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexLifecycleStatusShould
{
	#region Required Property Tests

	[Fact]
	public void IndexName_IsRequired()
	{
		// Arrange & Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "events-2026.01",
			Phase = "hot"
		};

		// Assert
		status.IndexName.ShouldBe("events-2026.01");
	}

	[Fact]
	public void Phase_IsRequired()
	{
		// Arrange & Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "events",
			Phase = "warm"
		};

		// Assert
		status.Phase.ShouldBe("warm");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void PolicyName_DefaultsToNull()
	{
		// Arrange & Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "test",
			Phase = "hot"
		};

		// Assert
		status.PolicyName.ShouldBeNull();
	}

	[Fact]
	public void Age_DefaultsToNull()
	{
		// Arrange & Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "test",
			Phase = "hot"
		};

		// Assert
		status.Age.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "events-2025.12",
			Phase = "cold",
			PolicyName = "events-lifecycle-policy",
			Age = TimeSpan.FromDays(45)
		};

		// Assert
		status.IndexName.ShouldBe("events-2025.12");
		status.Phase.ShouldBe("cold");
		status.PolicyName.ShouldBe("events-lifecycle-policy");
		status.Age.ShouldBe(TimeSpan.FromDays(45));
	}

	#endregion

	#region Phase Value Tests

	[Theory]
	[InlineData("hot")]
	[InlineData("warm")]
	[InlineData("cold")]
	[InlineData("delete")]
	public void Phase_AcceptsValidValues(string phase)
	{
		// Arrange & Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "test",
			Phase = phase
		};

		// Assert
		status.Phase.ShouldBe(phase);
	}

	#endregion

	#region Age Tests

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(30)]
	[InlineData(365)]
	public void Age_AcceptsVariousTimeSpans(int days)
	{
		// Arrange
		var expectedAge = TimeSpan.FromDays(days);

		// Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "test",
			Phase = "warm",
			Age = expectedAge
		};

		// Assert
		status.Age.ShouldBe(expectedAge);
	}

	#endregion
}
