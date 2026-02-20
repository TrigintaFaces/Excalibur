// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Views;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="MaterializedViewOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 516: Materialized Views foundation tests.
/// Tests verify options defaults and property setters.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Options")]
public sealed class MaterializedViewOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveCatchUpOnStartupDisabledByDefault()
	{
		// Arrange & Act
		var options = new MaterializedViewOptions();

		// Assert
		options.CatchUpOnStartup.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultBatchSizeOfOneHundred()
	{
		// Arrange & Act
		var options = new MaterializedViewOptions();

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultBatchDelayOfTenMilliseconds()
	{
		// Arrange & Act
		var options = new MaterializedViewOptions();

		// Assert
		options.BatchDelay.ShouldBe(TimeSpan.FromMilliseconds(10));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowEnablingCatchUpOnStartup()
	{
		// Act
		var options = new MaterializedViewOptions
		{
			CatchUpOnStartup = true
		};

		// Assert
		options.CatchUpOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		// Act
		var options = new MaterializedViewOptions
		{
			BatchSize = 200
		};

		// Assert
		options.BatchSize.ShouldBe(200);
	}

	[Fact]
	public void AllowSettingBatchDelay()
	{
		// Act
		var options = new MaterializedViewOptions
		{
			BatchDelay = TimeSpan.FromSeconds(1)
		};

		// Assert
		options.BatchDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion

	#region Complex Configuration Tests

	[Fact]
	public void SupportFullConfiguration()
	{
		// Act
		var options = new MaterializedViewOptions
		{
			CatchUpOnStartup = true,
			BatchSize = 500,
			BatchDelay = TimeSpan.FromMilliseconds(50)
		};

		// Assert
		options.CatchUpOnStartup.ShouldBeTrue();
		options.BatchSize.ShouldBe(500);
		options.BatchDelay.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(MaterializedViewOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(MaterializedViewOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
