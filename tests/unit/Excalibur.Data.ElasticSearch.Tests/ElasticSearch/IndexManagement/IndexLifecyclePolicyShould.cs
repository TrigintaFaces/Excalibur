// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexLifecyclePolicy"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify lifecycle policy phase configurations.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexLifecyclePolicyShould
{
	#region Default Value Tests

	[Fact]
	public void Hot_DefaultsToNull()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy();

		// Assert
		policy.Hot.ShouldBeNull();
	}

	[Fact]
	public void Warm_DefaultsToNull()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy();

		// Assert
		policy.Warm.ShouldBeNull();
	}

	[Fact]
	public void Cold_DefaultsToNull()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy();

		// Assert
		policy.Cold.ShouldBeNull();
	}

	[Fact]
	public void Delete_DefaultsToNull()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy();

		// Assert
		policy.Delete.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy
		{
			Hot = new HotPhaseConfiguration
			{
				Priority = 100,
				Rollover = new RolloverConditions { MaxAge = TimeSpan.FromDays(7) }
			},
			Warm = new WarmPhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(7),
				NumberOfReplicas = 1
			},
			Cold = new ColdPhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(30),
				NumberOfReplicas = 0
			},
			Delete = new DeletePhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(90)
			}
		};

		// Assert
		policy.Hot.ShouldNotBeNull();
		policy.Warm.ShouldNotBeNull();
		policy.Cold.ShouldNotBeNull();
		policy.Delete.ShouldNotBeNull();
	}

	#endregion

	#region Hot Phase Only Policy Tests

	[Fact]
	public void HotPhaseOnlyPolicy_IsValid()
	{
		// Arrange & Act - Simple policy with only hot phase
		var policy = new IndexLifecyclePolicy
		{
			Hot = new HotPhaseConfiguration
			{
				Priority = 100,
				Rollover = new RolloverConditions
				{
					MaxAge = TimeSpan.FromDays(30),
					MaxSize = "50gb"
				}
			}
		};

		// Assert
		policy.Hot.ShouldNotBeNull();
		policy.Warm.ShouldBeNull();
		policy.Cold.ShouldBeNull();
		policy.Delete.ShouldBeNull();
	}

	#endregion

	#region Full Lifecycle Policy Tests

	[Fact]
	public void FullLifecyclePolicy_HasAllPhases()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy
		{
			Hot = new HotPhaseConfiguration
			{
				Priority = 100,
				Rollover = new RolloverConditions { MaxDocs = 1000000 }
			},
			Warm = new WarmPhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(7),
				Priority = 50,
				ShrinkNumberOfShards = 1
			},
			Cold = new ColdPhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(30),
				Priority = 10
			},
			Delete = new DeletePhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(365),
				WaitForSnapshotPolicy = "daily-backup"
			}
		};

		// Assert - Verify phase transitions make sense (each phase MinAge > previous)
		policy.Hot.Priority.ShouldBe(100);
		policy.Warm.MinAge.ShouldBe(TimeSpan.FromDays(7));
		policy.Cold.MinAge.ShouldBe(TimeSpan.FromDays(30));
		policy.Delete.MinAge.ShouldBe(TimeSpan.FromDays(365));
	}

	#endregion

	#region Partial Lifecycle Policy Tests

	[Fact]
	public void PartialPolicy_SkipsWarmPhase()
	{
		// Arrange & Act - Policy without warm phase
		var policy = new IndexLifecyclePolicy
		{
			Hot = new HotPhaseConfiguration { Priority = 100 },
			Cold = new ColdPhaseConfiguration { MinAge = TimeSpan.FromDays(30) },
			Delete = new DeletePhaseConfiguration { MinAge = TimeSpan.FromDays(90) }
		};

		// Assert
		policy.Hot.ShouldNotBeNull();
		policy.Warm.ShouldBeNull();
		policy.Cold.ShouldNotBeNull();
		policy.Delete.ShouldNotBeNull();
	}

	[Fact]
	public void PartialPolicy_SkipsColdPhase()
	{
		// Arrange & Act - Policy without cold phase
		var policy = new IndexLifecyclePolicy
		{
			Hot = new HotPhaseConfiguration { Priority = 100 },
			Warm = new WarmPhaseConfiguration { MinAge = TimeSpan.FromDays(7) },
			Delete = new DeletePhaseConfiguration { MinAge = TimeSpan.FromDays(30) }
		};

		// Assert
		policy.Hot.ShouldNotBeNull();
		policy.Warm.ShouldNotBeNull();
		policy.Cold.ShouldBeNull();
		policy.Delete.ShouldNotBeNull();
	}

	#endregion
}
