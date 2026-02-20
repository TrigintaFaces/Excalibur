// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="DeletePhaseConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify delete phase-specific properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class DeletePhaseConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void WaitForSnapshotPolicy_DefaultsToNull()
	{
		// Arrange & Act
		var config = new DeletePhaseConfiguration();

		// Assert
		config.WaitForSnapshotPolicy.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var config = new DeletePhaseConfiguration
		{
			MinAge = TimeSpan.FromDays(365),
			WaitForSnapshotPolicy = "daily-snapshots"
		};

		// Assert
		config.MinAge.ShouldBe(TimeSpan.FromDays(365));
		config.WaitForSnapshotPolicy.ShouldBe("daily-snapshots");
	}

	#endregion

	#region Wait For Snapshot Policy Tests

	[Theory]
	[InlineData("daily-snapshots")]
	[InlineData("weekly-backup")]
	[InlineData("monthly-archive")]
	public void WaitForSnapshotPolicy_AcceptsVariousPolicyNames(string policyName)
	{
		// Arrange & Act
		var config = new DeletePhaseConfiguration
		{
			WaitForSnapshotPolicy = policyName
		};

		// Assert
		config.WaitForSnapshotPolicy.ShouldBe(policyName);
	}

	#endregion

	#region MinAge Tests

	[Theory]
	[InlineData(90)]
	[InlineData(180)]
	[InlineData(365)]
	public void MinAge_TypicallyLongRetention(int days)
	{
		// Arrange & Act
		var config = new DeletePhaseConfiguration
		{
			MinAge = TimeSpan.FromDays(days)
		};

		// Assert
		config.MinAge.ShouldBe(TimeSpan.FromDays(days));
	}

	#endregion

	#region Deletion Without Snapshot Tests

	[Fact]
	public void CanDeleteWithoutWaitingForSnapshot()
	{
		// Arrange & Act - Deletion without waiting for snapshot
		var config = new DeletePhaseConfiguration
		{
			MinAge = TimeSpan.FromDays(30)
		};

		// Assert
		config.WaitForSnapshotPolicy.ShouldBeNull();
		config.MinAge.ShouldBe(TimeSpan.FromDays(30));
	}

	#endregion
}
