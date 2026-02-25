// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexManagementOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify default values and nested settings initialization.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexManagementOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Enabled_DefaultsToTrue()
	{
		// Arrange & Act
		var settings = new IndexManagementOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void DefaultTemplate_IsNotNull()
	{
		// Arrange & Act
		var settings = new IndexManagementOptions();

		// Assert
		settings.DefaultTemplate.ShouldNotBeNull();
	}

	[Fact]
	public void Lifecycle_IsNotNull()
	{
		// Arrange & Act
		var settings = new IndexManagementOptions();

		// Assert
		settings.Lifecycle.ShouldNotBeNull();
	}

	[Fact]
	public void Optimization_IsNotNull()
	{
		// Arrange & Act
		var settings = new IndexManagementOptions();

		// Assert
		settings.Optimization.ShouldNotBeNull();
	}

	#endregion

	#region Nested Settings Tests

	[Fact]
	public void Lifecycle_HasDefaultValues()
	{
		// Arrange
		var settings = new IndexManagementOptions();

		// Assert
		settings.Lifecycle.Enabled.ShouldBeTrue();
		settings.Lifecycle.HotPhaseDuration.ShouldBe(TimeSpan.FromDays(7));
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void Enabled_CanBeSetToFalse()
	{
		// Arrange & Act
		var settings = new IndexManagementOptions { Enabled = false };

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void NestedSettings_CanBeReplaced()
	{
		// Arrange
		var customLifecycle = new LifecycleManagementOptions
		{
			Enabled = false,
			HotPhaseDuration = TimeSpan.FromDays(14)
		};

		// Act
		var settings = new IndexManagementOptions
		{
			Lifecycle = customLifecycle
		};

		// Assert
		settings.Lifecycle.Enabled.ShouldBeFalse();
		settings.Lifecycle.HotPhaseDuration.ShouldBe(TimeSpan.FromDays(14));
	}

	#endregion
}
