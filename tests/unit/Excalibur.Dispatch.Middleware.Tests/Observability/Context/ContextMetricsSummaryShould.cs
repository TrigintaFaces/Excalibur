// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextMetricsSummary"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextMetricsSummaryShould
{
	[Fact]
	public void HaveZeroTotalContextsProcessed_ByDefault()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.TotalContextsProcessed.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroContextsPreservedSuccessfully_ByDefault()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.ContextsPreservedSuccessfully.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroPreservationRate_ByDefault()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.PreservationRate.ShouldBe(0.0);
	}

	[Fact]
	public void HaveZeroActiveContexts_ByDefault()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.ActiveContexts.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroMaxLineageDepth_ByDefault()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.MaxLineageDepth.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultTimestamp_ByDefault()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.Timestamp.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void AllowSettingTotalContextsProcessed()
	{
		// Arrange
		var summary = new ContextMetricsSummary();

		// Act
		summary.TotalContextsProcessed = 1000;

		// Assert
		summary.TotalContextsProcessed.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingContextsPreservedSuccessfully()
	{
		// Arrange
		var summary = new ContextMetricsSummary();

		// Act
		summary.ContextsPreservedSuccessfully = 950;

		// Assert
		summary.ContextsPreservedSuccessfully.ShouldBe(950);
	}

	[Fact]
	public void AllowSettingPreservationRate()
	{
		// Arrange
		var summary = new ContextMetricsSummary();

		// Act
		summary.PreservationRate = 0.95;

		// Assert
		summary.PreservationRate.ShouldBe(0.95);
	}

	[Fact]
	public void AllowSettingActiveContexts()
	{
		// Arrange
		var summary = new ContextMetricsSummary();

		// Act
		summary.ActiveContexts = 100;

		// Assert
		summary.ActiveContexts.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingMaxLineageDepth()
	{
		// Arrange
		var summary = new ContextMetricsSummary();

		// Act
		summary.MaxLineageDepth = 10;

		// Assert
		summary.MaxLineageDepth.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var summary = new ContextMetricsSummary();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		summary.Timestamp = timestamp;

		// Assert
		summary.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var summary = new ContextMetricsSummary
		{
			TotalContextsProcessed = 10000,
			ContextsPreservedSuccessfully = 9500,
			PreservationRate = 0.95,
			ActiveContexts = 50,
			MaxLineageDepth = 8,
			Timestamp = timestamp,
		};

		// Assert
		summary.TotalContextsProcessed.ShouldBe(10000);
		summary.ContextsPreservedSuccessfully.ShouldBe(9500);
		summary.PreservationRate.ShouldBe(0.95);
		summary.ActiveContexts.ShouldBe(50);
		summary.MaxLineageDepth.ShouldBe(8);
		summary.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowLargeTotalContextsProcessed()
	{
		// Arrange
		var summary = new ContextMetricsSummary();

		// Act
		summary.TotalContextsProcessed = long.MaxValue;

		// Assert
		summary.TotalContextsProcessed.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void AllowPreservationRateOfOne()
	{
		// Arrange
		var summary = new ContextMetricsSummary();

		// Act
		summary.PreservationRate = 1.0;

		// Assert
		summary.PreservationRate.ShouldBe(1.0);
	}
}
