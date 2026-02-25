// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Telemetry;

namespace Excalibur.Saga.Tests.Telemetry;

/// <summary>
/// Unit tests for <see cref="SagaMetrics"/>.
/// Verifies metrics recording and active saga tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaMetricsShould : IDisposable
{
	private const string TestSagaType = "TestSagaType";

	public SagaMetricsShould()
	{
		// Reset state before each test
		SagaMetrics.ResetActiveCounts();
	}

	public void Dispose()
	{
		// Clean up after each test
		SagaMetrics.ResetActiveCounts();
	}

	#region Constants Tests

	[Fact]
	public void HaveCorrectMeterName()
	{
		// Assert
		SagaMetrics.MeterName.ShouldBe("Excalibur.Dispatch.Sagas");
	}

	[Fact]
	public void HaveCorrectMeterVersion()
	{
		// Assert
		SagaMetrics.MeterVersion.ShouldBe("1.0.0");
	}

	#endregion

	#region RecordSagaStarted Tests

	[Fact]
	public void RecordSagaStarted_DoesNotThrow()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaStarted(TestSagaType));
	}

	[Fact]
	public void RecordSagaStarted_AcceptsEmptyString()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaStarted(string.Empty));
	}

	#endregion

	#region RecordSagaCompleted Tests

	[Fact]
	public void RecordSagaCompleted_DoesNotThrow()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaCompleted(TestSagaType));
	}

	[Fact]
	public void RecordSagaCompleted_AcceptsEmptyString()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaCompleted(string.Empty));
	}

	#endregion

	#region RecordSagaFailed Tests

	[Fact]
	public void RecordSagaFailed_DoesNotThrow()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaFailed(TestSagaType));
	}

	[Fact]
	public void RecordSagaFailed_AcceptsEmptyString()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaFailed(string.Empty));
	}

	#endregion

	#region RecordSagaCompensated Tests

	[Fact]
	public void RecordSagaCompensated_DoesNotThrow()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaCompensated(TestSagaType));
	}

	[Fact]
	public void RecordSagaCompensated_AcceptsEmptyString()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaCompensated(string.Empty));
	}

	#endregion

	#region RecordSagaDuration Tests

	[Fact]
	public void RecordSagaDuration_DoesNotThrow()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaDuration(TestSagaType, 100.0));
	}

	[Fact]
	public void RecordSagaDuration_AcceptsZeroDuration()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaDuration(TestSagaType, 0.0));
	}

	[Fact]
	public void RecordSagaDuration_AcceptsNegativeDuration()
	{
		// Act & Assert - should not throw (negative values are allowed by histograms)
		Should.NotThrow(() => SagaMetrics.RecordSagaDuration(TestSagaType, -1.0));
	}

	[Fact]
	public void RecordSagaDuration_AcceptsLargeDuration()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordSagaDuration(TestSagaType, 3600000.0)); // 1 hour in ms
	}

	#endregion

	#region RecordHandlerDuration Tests

	[Fact]
	public void RecordHandlerDuration_DoesNotThrow()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordHandlerDuration(TestSagaType, 50.0));
	}

	[Fact]
	public void RecordHandlerDuration_AcceptsZeroDuration()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaMetrics.RecordHandlerDuration(TestSagaType, 0.0));
	}

	#endregion

	#region IncrementActive Tests

	[Fact]
	public void IncrementActive_IncreasesCount()
	{
		// Arrange
		var initialCount = SagaMetrics.GetActiveCount(TestSagaType);

		// Act
		SagaMetrics.IncrementActive(TestSagaType);

		// Assert
		SagaMetrics.GetActiveCount(TestSagaType).ShouldBe(initialCount + 1);
	}

	[Fact]
	public void IncrementActive_IncreasesCountMultipleTimes()
	{
		// Act
		SagaMetrics.IncrementActive(TestSagaType);
		SagaMetrics.IncrementActive(TestSagaType);
		SagaMetrics.IncrementActive(TestSagaType);

		// Assert
		SagaMetrics.GetActiveCount(TestSagaType).ShouldBe(3);
	}

	[Fact]
	public void IncrementActive_TracksDifferentSagaTypesSeparately()
	{
		// Act
		SagaMetrics.IncrementActive("SagaTypeA");
		SagaMetrics.IncrementActive("SagaTypeA");
		SagaMetrics.IncrementActive("SagaTypeB");

		// Assert
		SagaMetrics.GetActiveCount("SagaTypeA").ShouldBe(2);
		SagaMetrics.GetActiveCount("SagaTypeB").ShouldBe(1);
	}

	#endregion

	#region DecrementActive Tests

	[Fact]
	public void DecrementActive_DecreasesCount()
	{
		// Arrange
		SagaMetrics.IncrementActive(TestSagaType);
		SagaMetrics.IncrementActive(TestSagaType);

		// Act
		SagaMetrics.DecrementActive(TestSagaType);

		// Assert
		SagaMetrics.GetActiveCount(TestSagaType).ShouldBe(1);
	}

	[Fact]
	public void DecrementActive_DoesNotGoBelowZero()
	{
		// Act
		SagaMetrics.DecrementActive(TestSagaType);
		SagaMetrics.DecrementActive(TestSagaType);

		// Assert
		SagaMetrics.GetActiveCount(TestSagaType).ShouldBe(0);
	}

	[Fact]
	public void DecrementActive_InitializesToZeroForUnknownType()
	{
		// Act
		SagaMetrics.DecrementActive("UnknownSagaType");

		// Assert
		SagaMetrics.GetActiveCount("UnknownSagaType").ShouldBe(0);
	}

	#endregion

	#region GetActiveCount Tests

	[Fact]
	public void GetActiveCount_ReturnsZeroForUnknownType()
	{
		// Act
		var count = SagaMetrics.GetActiveCount("UnknownSagaType");

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public void GetActiveCount_ReturnsCorrectCountAfterOperations()
	{
		// Arrange
		SagaMetrics.IncrementActive(TestSagaType);
		SagaMetrics.IncrementActive(TestSagaType);
		SagaMetrics.IncrementActive(TestSagaType);
		SagaMetrics.DecrementActive(TestSagaType);

		// Act
		var count = SagaMetrics.GetActiveCount(TestSagaType);

		// Assert
		count.ShouldBe(2);
	}

	#endregion

	#region ResetActiveCounts Tests

	[Fact]
	public void ResetActiveCounts_ClearsAllCounts()
	{
		// Arrange
		SagaMetrics.IncrementActive("SagaTypeA");
		SagaMetrics.IncrementActive("SagaTypeB");
		SagaMetrics.IncrementActive("SagaTypeC");

		// Act
		SagaMetrics.ResetActiveCounts();

		// Assert
		SagaMetrics.GetActiveCount("SagaTypeA").ShouldBe(0);
		SagaMetrics.GetActiveCount("SagaTypeB").ShouldBe(0);
		SagaMetrics.GetActiveCount("SagaTypeC").ShouldBe(0);
	}

	[Fact]
	public void ResetActiveCounts_CanBeCalledMultipleTimes()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			SagaMetrics.ResetActiveCounts();
			SagaMetrics.ResetActiveCounts();
			SagaMetrics.ResetActiveCounts();
		});
	}

	#endregion

	#region Workflow Tests

	[Fact]
	public void FullSagaWorkflow_TracksMetricsCorrectly()
	{
		// Simulates a complete saga workflow

		// Start saga
		SagaMetrics.RecordSagaStarted(TestSagaType);
		SagaMetrics.IncrementActive(TestSagaType);

		// Record handler execution
		SagaMetrics.RecordHandlerDuration(TestSagaType, 25.0);
		SagaMetrics.RecordHandlerDuration(TestSagaType, 30.0);

		// Assert active count during execution
		SagaMetrics.GetActiveCount(TestSagaType).ShouldBe(1);

		// Complete saga
		SagaMetrics.RecordSagaCompleted(TestSagaType);
		SagaMetrics.RecordSagaDuration(TestSagaType, 150.0);
		SagaMetrics.DecrementActive(TestSagaType);

		// Assert active count after completion
		SagaMetrics.GetActiveCount(TestSagaType).ShouldBe(0);
	}

	[Fact]
	public void FailedSagaWorkflow_TracksMetricsCorrectly()
	{
		// Simulates a failed saga workflow

		// Start saga
		SagaMetrics.RecordSagaStarted(TestSagaType);
		SagaMetrics.IncrementActive(TestSagaType);

		// Saga fails
		SagaMetrics.RecordSagaFailed(TestSagaType);
		SagaMetrics.RecordSagaDuration(TestSagaType, 50.0);
		SagaMetrics.DecrementActive(TestSagaType);

		// Assert
		SagaMetrics.GetActiveCount(TestSagaType).ShouldBe(0);
	}

	[Fact]
	public void CompensatedSagaWorkflow_TracksMetricsCorrectly()
	{
		// Simulates a compensated saga workflow

		// Start saga
		SagaMetrics.RecordSagaStarted(TestSagaType);
		SagaMetrics.IncrementActive(TestSagaType);

		// Saga is compensated
		SagaMetrics.RecordSagaCompensated(TestSagaType);
		SagaMetrics.RecordSagaDuration(TestSagaType, 200.0);
		SagaMetrics.DecrementActive(TestSagaType);

		// Assert
		SagaMetrics.GetActiveCount(TestSagaType).ShouldBe(0);
	}

	#endregion
}
