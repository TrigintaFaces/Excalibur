// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Saga.Telemetry;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Telemetry;

/// <summary>
/// Unit tests for <see cref="SagaMetrics"/> validating OpenTelemetry metrics
/// for saga operations (counters, histograms, and gauges).
/// </summary>
/// <remarks>
/// <para>
/// Sprint 218 - OpenTelemetry Metrics &amp; Process Manager.
/// Task: 9blyh (SAGA-015: Unit Tests - OTel Metrics).
/// </para>
/// <para>
/// These tests verify:
/// <list type="bullet">
///   <item>Counter increments for saga lifecycle events (started, completed, failed, compensated)</item>
///   <item>Histogram recordings for duration metrics (saga duration, handler duration)</item>
///   <item>Gauge behavior for active saga count tracking</item>
///   <item>All metrics include the required saga_type tag (AD-218-4)</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Sprint", "218")]
public sealed class SagaMetricsShould : IDisposable
{
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> _recordedMeasurements = new();
	private readonly object _lock = new();

	public SagaMetricsShould()
	{
		// Reset any state from previous tests
		SagaMetrics.ResetActiveCounts();

		_meterListener = new MeterListener
		{
			InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Meter.Name == SagaMetrics.MeterName)
				{
					listener.EnableMeasurementEvents(instrument);
				}
			}
		};

		// Subscribe to counter measurements
		_meterListener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
		_meterListener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);

		_meterListener.Start();
	}

	public void Dispose()
	{
		_meterListener.Dispose();
		SagaMetrics.ResetActiveCounts();
	}

	private void OnMeasurementRecorded<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
		where T : struct
	{
		lock (_lock)
		{
			_recordedMeasurements.Add((instrument.Name, value, tags.ToArray()));
		}
	}

	private IEnumerable<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> GetMeasurements(string name)
	{
		lock (_lock)
		{
			return _recordedMeasurements.Where(m => m.Name == name).ToList();
		}
	}

	private static string UniqueSagaType(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

	private static bool HasSagaTypeTag(KeyValuePair<string, object?>[] tags, string sagaType) =>
		tags.Any(tag => tag.Key == "saga_type" && string.Equals(tag.Value as string, sagaType, StringComparison.Ordinal));

	private List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> GetMeasurementsForSaga(string metricName, string sagaType)
	{
		var measurements = GetMeasurements(metricName)
			.Where(m => HasSagaTypeTag(m.Tags, sagaType))
			.ToList();
		measurements.ShouldNotBeEmpty();
		return measurements;
	}

	#region Meter Configuration Tests

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

	#endregion Meter Configuration Tests

	#region Counter Tests

	[Fact]
	public void IncrementSagaStartedCounter()
	{
		var sagaType = UniqueSagaType("OrderSaga");

		// Act
		SagaMetrics.RecordSagaStarted(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.started_total", sagaType);
		measurements.ShouldContain(m => (long)m.Value == 1L);
	}

	[Fact]
	public void IncrementSagaCompletedCounter()
	{
		var sagaType = UniqueSagaType("OrderSaga");

		// Act
		SagaMetrics.RecordSagaCompleted(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.completed_total", sagaType);
		measurements.ShouldContain(m => (long)m.Value == 1L);
	}

	[Fact]
	public void IncrementSagaFailedCounter()
	{
		var sagaType = UniqueSagaType("PaymentSaga");

		// Act
		SagaMetrics.RecordSagaFailed(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.failed_total", sagaType);
		measurements.ShouldContain(m => (long)m.Value == 1L);
	}

	[Fact]
	public void IncrementSagaCompensatedCounter()
	{
		var sagaType = UniqueSagaType("ShippingSaga");

		// Act
		SagaMetrics.RecordSagaCompensated(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.compensated_total", sagaType);
		measurements.ShouldContain(m => (long)m.Value == 1L);
	}

	[Fact]
	public void AccumulateMultipleCounterIncrements()
	{
		var sagaType = UniqueSagaType("TestSaga");

		// Act
		SagaMetrics.RecordSagaStarted(sagaType);
		SagaMetrics.RecordSagaStarted(sagaType);
		SagaMetrics.RecordSagaStarted(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.started_total", sagaType);
		measurements.Count.ShouldBe(3);
		measurements.All(m => (long)m.Value == 1L).ShouldBeTrue();
	}

	#endregion Counter Tests

	#region Histogram Tests

	[Fact]
	public void RecordSagaDuration()
	{
		var sagaType = UniqueSagaType("OrderSaga");

		// Act
		SagaMetrics.RecordSagaDuration(sagaType, 1234.5);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.duration_ms", sagaType);
		measurements.ShouldContain(m => (double)m.Value == 1234.5);
	}

	[Fact]
	public void RecordHandlerDuration()
	{
		var sagaType = UniqueSagaType("OrderSaga");

		// Act
		SagaMetrics.RecordHandlerDuration(sagaType, 56.7);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.handler_duration_ms", sagaType);
		measurements.ShouldContain(m => (double)m.Value == 56.7);
	}

	[Fact]
	public void RecordZeroDuration()
	{
		var sagaType = UniqueSagaType("FastSaga");

		// Act
		SagaMetrics.RecordSagaDuration(sagaType, 0.0);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.duration_ms", sagaType);
		measurements.ShouldContain(m => (double)m.Value == 0.0);
	}

	#endregion Histogram Tests

	#region Active Gauge Tests

	[Fact]
	public void IncrementActiveCount()
	{
		// Act
		SagaMetrics.IncrementActive("OrderSaga");

		// Assert
		SagaMetrics.GetActiveCount("OrderSaga").ShouldBe(1);
	}

	[Fact]
	public void DecrementActiveCount()
	{
		// Arrange
		SagaMetrics.IncrementActive("OrderSaga");
		SagaMetrics.IncrementActive("OrderSaga");

		// Act
		SagaMetrics.DecrementActive("OrderSaga");

		// Assert
		SagaMetrics.GetActiveCount("OrderSaga").ShouldBe(1);
	}

	[Fact]
	public void NotGoNegativeOnDecrement()
	{
		// Act - Decrement without any prior increments
		SagaMetrics.DecrementActive("NeverStartedSaga");

		// Assert - Should be 0, not negative
		SagaMetrics.GetActiveCount("NeverStartedSaga").ShouldBe(0);
	}

	[Fact]
	public void TrackMultipleSagaTypesIndependently()
	{
		// Act
		SagaMetrics.IncrementActive("OrderSaga");
		SagaMetrics.IncrementActive("OrderSaga");
		SagaMetrics.IncrementActive("PaymentSaga");

		// Assert
		SagaMetrics.GetActiveCount("OrderSaga").ShouldBe(2);
		SagaMetrics.GetActiveCount("PaymentSaga").ShouldBe(1);
	}

	[Fact]
	public void ResetActiveCountsCorrectly()
	{
		// Arrange
		SagaMetrics.IncrementActive("OrderSaga");
		SagaMetrics.IncrementActive("PaymentSaga");

		// Act
		SagaMetrics.ResetActiveCounts();

		// Assert
		SagaMetrics.GetActiveCount("OrderSaga").ShouldBe(0);
		SagaMetrics.GetActiveCount("PaymentSaga").ShouldBe(0);
	}

	[Fact]
	public void ReturnZeroForUnknownSagaType()
	{
		// Act & Assert
		SagaMetrics.GetActiveCount("UnknownSaga").ShouldBe(0);
	}

	#endregion Active Gauge Tests

	#region Tag Verification Tests (AD-218-4)

	[Fact]
	public void IncludeSagaTypeTagOnStartedCounter()
	{
		var sagaType = UniqueSagaType("OrderSaga");

		// Act
		SagaMetrics.RecordSagaStarted(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.started_total", sagaType);
		measurements.ShouldContain(m => HasSagaTypeTag(m.Tags, sagaType));
	}

	[Fact]
	public void IncludeSagaTypeTagOnCompletedCounter()
	{
		var sagaType = UniqueSagaType("OrderSaga");

		// Act
		SagaMetrics.RecordSagaCompleted(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.completed_total", sagaType);
		measurements.ShouldContain(m => HasSagaTypeTag(m.Tags, sagaType));
	}

	[Fact]
	public void IncludeSagaTypeTagOnFailedCounter()
	{
		var sagaType = UniqueSagaType("PaymentSaga");

		// Act
		SagaMetrics.RecordSagaFailed(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.failed_total", sagaType);
		measurements.ShouldContain(m => HasSagaTypeTag(m.Tags, sagaType));
	}

	[Fact]
	public void IncludeSagaTypeTagOnCompensatedCounter()
	{
		var sagaType = UniqueSagaType("ShippingSaga");

		// Act
		SagaMetrics.RecordSagaCompensated(sagaType);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.compensated_total", sagaType);
		measurements.ShouldContain(m => HasSagaTypeTag(m.Tags, sagaType));
	}

	[Fact]
	public void IncludeSagaTypeTagOnDurationHistogram()
	{
		var sagaType = UniqueSagaType("OrderSaga");

		// Act
		SagaMetrics.RecordSagaDuration(sagaType, 100.0);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.duration_ms", sagaType);
		measurements.ShouldContain(m => HasSagaTypeTag(m.Tags, sagaType));
	}

	[Fact]
	public void IncludeSagaTypeTagOnHandlerDurationHistogram()
	{
		var sagaType = UniqueSagaType("OrderSaga");

		// Act
		SagaMetrics.RecordHandlerDuration(sagaType, 50.0);

		// Assert
		var measurements = GetMeasurementsForSaga("dispatch.saga.handler_duration_ms", sagaType);
		measurements.ShouldContain(m => HasSagaTypeTag(m.Tags, sagaType));
	}

	#endregion Tag Verification Tests (AD-218-4)
}
