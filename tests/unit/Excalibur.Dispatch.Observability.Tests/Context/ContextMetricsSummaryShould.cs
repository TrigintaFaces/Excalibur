// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextMetricsSummary"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextMetricsSummaryShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultTotalContextsProcessed()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.TotalContextsProcessed.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultContextsPreservedSuccessfully()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.ContextsPreservedSuccessfully.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultPreservationRate()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.PreservationRate.ShouldBe(0.0);
	}

	[Fact]
	public void HaveDefaultActiveContexts()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.ActiveContexts.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultMaxLineageDepth()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.MaxLineageDepth.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary();

		// Assert
		summary.Timestamp.ShouldBe(default(DateTimeOffset));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingTotalContextsProcessed()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary
		{
			TotalContextsProcessed = 1000,
		};

		// Assert
		summary.TotalContextsProcessed.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingContextsPreservedSuccessfully()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary
		{
			ContextsPreservedSuccessfully = 950,
		};

		// Assert
		summary.ContextsPreservedSuccessfully.ShouldBe(950);
	}

	[Fact]
	public void AllowSettingPreservationRate()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary
		{
			PreservationRate = 0.95,
		};

		// Assert
		summary.PreservationRate.ShouldBe(0.95);
	}

	[Fact]
	public void AllowSettingActiveContexts()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary
		{
			ActiveContexts = 50,
		};

		// Assert
		summary.ActiveContexts.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingMaxLineageDepth()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary
		{
			MaxLineageDepth = 10,
		};

		// Assert
		summary.MaxLineageDepth.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var summary = new ContextMetricsSummary
		{
			Timestamp = timestamp,
		};

		// Assert
		summary.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var summary = new ContextMetricsSummary
		{
			TotalContextsProcessed = 10000,
			ContextsPreservedSuccessfully = 9500,
			PreservationRate = 0.95,
			ActiveContexts = 100,
			MaxLineageDepth = 15,
			Timestamp = timestamp,
		};

		// Assert
		summary.TotalContextsProcessed.ShouldBe(10000);
		summary.ContextsPreservedSuccessfully.ShouldBe(9500);
		summary.PreservationRate.ShouldBe(0.95);
		summary.ActiveContexts.ShouldBe(100);
		summary.MaxLineageDepth.ShouldBe(15);
		summary.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void SupportZeroPreservationRate()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary
		{
			TotalContextsProcessed = 100,
			ContextsPreservedSuccessfully = 0,
			PreservationRate = 0.0,
		};

		// Assert
		summary.PreservationRate.ShouldBe(0.0);
	}

	[Fact]
	public void SupportFullPreservationRate()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary
		{
			TotalContextsProcessed = 100,
			ContextsPreservedSuccessfully = 100,
			PreservationRate = 1.0,
		};

		// Assert
		summary.PreservationRate.ShouldBe(1.0);
	}

	[Fact]
	public void SupportLargeNumbers()
	{
		// Arrange & Act
		var summary = new ContextMetricsSummary
		{
			TotalContextsProcessed = long.MaxValue,
			ContextsPreservedSuccessfully = long.MaxValue - 1,
			ActiveContexts = 1_000_000,
			MaxLineageDepth = 1000,
		};

		// Assert
		summary.TotalContextsProcessed.ShouldBe(long.MaxValue);
		summary.ContextsPreservedSuccessfully.ShouldBe(long.MaxValue - 1);
		summary.ActiveContexts.ShouldBe(1_000_000);
		summary.MaxLineageDepth.ShouldBe(1000);
	}

	#endregion
}
