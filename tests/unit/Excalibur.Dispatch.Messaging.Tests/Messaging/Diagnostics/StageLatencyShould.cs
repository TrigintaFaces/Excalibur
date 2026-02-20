// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Messaging.Diagnostics;

/// <summary>
/// Unit tests for <see cref="StageLatency"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Diagnostics")]
[Trait("Priority", "0")]
public sealed class StageLatencyShould
{
	#region Default Value Tests

	[Fact]
	public void Default_StageName_IsEmpty()
	{
		// Arrange & Act
		var latency = new StageLatency();

		// Assert
		latency.StageName.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_MinLatencyMs_IsZero()
	{
		// Arrange & Act
		var latency = new StageLatency();

		// Assert
		latency.MinLatencyMs.ShouldBe(0);
	}

	[Fact]
	public void Default_MaxLatencyMs_IsZero()
	{
		// Arrange & Act
		var latency = new StageLatency();

		// Assert
		latency.MaxLatencyMs.ShouldBe(0);
	}

	[Fact]
	public void Default_AverageLatencyMs_IsZero()
	{
		// Arrange & Act
		var latency = new StageLatency();

		// Assert
		latency.AverageLatencyMs.ShouldBe(0);
	}

	[Fact]
	public void Default_MedianLatencyMs_IsZero()
	{
		// Arrange & Act
		var latency = new StageLatency();

		// Assert
		latency.MedianLatencyMs.ShouldBe(0);
	}

	[Fact]
	public void Default_P95LatencyMs_IsZero()
	{
		// Arrange & Act
		var latency = new StageLatency();

		// Assert
		latency.P95LatencyMs.ShouldBe(0);
	}

	[Fact]
	public void Default_P99LatencyMs_IsZero()
	{
		// Arrange & Act
		var latency = new StageLatency();

		// Assert
		latency.P99LatencyMs.ShouldBe(0);
	}

	[Fact]
	public void Default_SampleCount_IsZero()
	{
		// Arrange & Act
		var latency = new StageLatency();

		// Assert
		latency.SampleCount.ShouldBe(0);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void StageName_CanBeSet()
	{
		// Arrange
		var latency = new StageLatency();

		// Act
		latency.StageName = "Validation";

		// Assert
		latency.StageName.ShouldBe("Validation");
	}

	[Fact]
	public void MinLatencyMs_CanBeSet()
	{
		// Arrange
		var latency = new StageLatency();

		// Act
		latency.MinLatencyMs = 1.5;

		// Assert
		latency.MinLatencyMs.ShouldBe(1.5);
	}

	[Fact]
	public void MaxLatencyMs_CanBeSet()
	{
		// Arrange
		var latency = new StageLatency();

		// Act
		latency.MaxLatencyMs = 100.5;

		// Assert
		latency.MaxLatencyMs.ShouldBe(100.5);
	}

	[Fact]
	public void SampleCount_CanBeSet()
	{
		// Arrange
		var latency = new StageLatency();

		// Act
		latency.SampleCount = 1000;

		// Assert
		latency.SampleCount.ShouldBe(1000);
	}

	#endregion

	#region Factory Method Tests

	[Fact]
	public void Create_SetsStageName()
	{
		// Act
		var latency = StageLatency.Create("TestStage");

		// Assert
		latency.StageName.ShouldBe("TestStage");
	}

	[Fact]
	public void Create_SetsStartTimeTicks()
	{
		// Act
		var latency = StageLatency.Create("TestStage");

		// Assert
		latency.MeasurementStartTimeTicks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Create_MeasurementStartTime_IsUtc()
	{
		// Act
		var latency = StageLatency.Create("TestStage");

		// Assert
		latency.MeasurementStartTime.Kind.ShouldBe(DateTimeKind.Utc);
	}

	#endregion

	#region CompleteMeasurement Tests

	[Fact]
	public void CompleteMeasurement_SetsEndTimeTicks()
	{
		// Arrange
		var latency = StageLatency.Create("TestStage");

		// Act
		latency.CompleteMeasurement();

		// Assert
		latency.MeasurementEndTimeTicks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompleteMeasurement_EndTimeAfterStartTime()
	{
		// Arrange
		var latency = StageLatency.Create("TestStage");

		// Act
		latency.CompleteMeasurement();

		// Assert
		latency.MeasurementEndTimeTicks.ShouldBeGreaterThanOrEqualTo(latency.MeasurementStartTimeTicks);
	}

	[Fact]
	public void MeasurementDuration_IsNonNegative()
	{
		// Arrange
		var latency = StageLatency.Create("TestStage");

		// Act
		latency.CompleteMeasurement();

		// Assert
		latency.MeasurementDuration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var latency = new StageLatency
		{
			StageName = "Handler",
			MinLatencyMs = 1.0,
			MaxLatencyMs = 50.0,
			AverageLatencyMs = 10.0,
			MedianLatencyMs = 8.0,
			P95LatencyMs = 40.0,
			P99LatencyMs = 48.0,
			SampleCount = 500,
		};

		// Assert
		latency.StageName.ShouldBe("Handler");
		latency.MinLatencyMs.ShouldBe(1.0);
		latency.MaxLatencyMs.ShouldBe(50.0);
		latency.AverageLatencyMs.ShouldBe(10.0);
		latency.MedianLatencyMs.ShouldBe(8.0);
		latency.P95LatencyMs.ShouldBe(40.0);
		latency.P99LatencyMs.ShouldBe(48.0);
		latency.SampleCount.ShouldBe(500);
	}

	#endregion
}
