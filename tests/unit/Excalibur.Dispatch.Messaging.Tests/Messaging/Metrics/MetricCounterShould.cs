// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricCounter{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class MetricCounterShould : UnitTestBase
{
	private readonly Meter _meter;
	private readonly List<Measurement<long>> _measurements = [];
	private readonly MeterListener _listener;

	public MetricCounterShould()
	{
		_meter = new Meter($"TestMeter.{Guid.NewGuid():N}");
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			_measurements.Add(new Measurement<long>(measurement, tags));
		});
		_listener.Start();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_listener.Dispose();
			_meter.Dispose();
		}

		base.Dispose(disposing);
	}

	#region Constructor Tests

	[Fact]
	public void CreateWithNameOnly()
	{
		// Act
		var counter = new MetricCounter<long>(_meter, "test_counter");

		// Assert - should not throw
		Should.NotThrow(() => counter.Add(1));
	}

	[Fact]
	public void CreateWithNameAndUnit()
	{
		// Act
		var counter = new MetricCounter<long>(_meter, "bytes_total", "bytes");

		// Assert - should not throw
		Should.NotThrow(() => counter.Add(1024));
	}

	[Fact]
	public void CreateWithNameUnitAndDescription()
	{
		// Act
		var counter = new MetricCounter<long>(_meter, "requests_total", "requests", "Total number of requests");

		// Assert - should not throw
		Should.NotThrow(() => counter.Add(1));
	}

	#endregion

	#region Add Tests

	[Fact]
	public void AddValueToCounter()
	{
		// Arrange
		var counter = new MetricCounter<long>(_meter, "add_test");

		// Act
		counter.Add(100);
		_listener.RecordObservableInstruments();

		// Assert - measurement was recorded (may need manual verification in real scenarios)
		_measurements.ShouldNotBeEmpty();
	}

	[Fact]
	public void AddMultipleValues()
	{
		// Arrange
		var counter = new MetricCounter<long>(_meter, "add_multiple_test");

		// Act
		counter.Add(10);
		counter.Add(20);
		counter.Add(30);
		_listener.RecordObservableInstruments();

		// Assert
		_measurements.Count.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public void AddZeroValue()
	{
		// Arrange
		var counter = new MetricCounter<long>(_meter, "add_zero_test");

		// Act & Assert - should not throw
		Should.NotThrow(() => counter.Add(0));
	}

	[Fact]
	public void AddWithTags()
	{
		// Arrange
		var counter = new MetricCounter<long>(_meter, "tagged_counter");
		var tags = new TagList { { "method", "GET" }, { "status", 200 } };

		// Act
		counter.Add(1, tags);
		_listener.RecordObservableInstruments();

		// Assert
		_measurements.ShouldNotBeEmpty();
	}

	[Fact]
	public void AddWithEmptyTags()
	{
		// Arrange
		var counter = new MetricCounter<long>(_meter, "empty_tags_counter");

		// Act & Assert - should not throw
		Should.NotThrow(() => counter.Add(1, default));
	}

	#endregion

	#region Generic Type Tests

	[Fact]
	public void SupportIntType()
	{
		// Arrange
		using var meter = new Meter("IntTestMeter");
		var counter = new MetricCounter<int>(meter, "int_counter");

		// Act & Assert - should not throw
		Should.NotThrow(() => counter.Add(42));
	}

	[Fact]
	public void SupportByteType()
	{
		// Arrange
		using var meter = new Meter("ByteTestMeter");
		var counter = new MetricCounter<byte>(meter, "byte_counter");

		// Act & Assert - should not throw
		Should.NotThrow(() => counter.Add(255));
	}

	[Fact]
	public void SupportShortType()
	{
		// Arrange
		using var meter = new Meter("ShortTestMeter");
		var counter = new MetricCounter<short>(meter, "short_counter");

		// Act & Assert - should not throw
		Should.NotThrow(() => counter.Add(1000));
	}

	[Fact]
	public void SupportDoubleType()
	{
		// Arrange
		using var meter = new Meter("DoubleTestMeter");
		var counter = new MetricCounter<double>(meter, "double_counter");

		// Act & Assert - should not throw
		Should.NotThrow(() => counter.Add(3.14159));
	}

	[Fact]
	public void SupportDecimalType()
	{
		// Arrange
		using var meter = new Meter("DecimalTestMeter");
		var counter = new MetricCounter<decimal>(meter, "decimal_counter");

		// Act & Assert - should not throw
		Should.NotThrow(() => counter.Add(99.99m));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void TrackHttpRequestsWithStatusCodes()
	{
		// Arrange
		var counter = new MetricCounter<long>(_meter, "http_requests_total", "requests", "Total HTTP requests");

		// Act - Simulate traffic
		counter.Add(1, new TagList { { "method", "GET" }, { "status", 200 } });
		counter.Add(1, new TagList { { "method", "GET" }, { "status", 200 } });
		counter.Add(1, new TagList { { "method", "POST" }, { "status", 201 } });
		counter.Add(1, new TagList { { "method", "GET" }, { "status", 404 } });
		_listener.RecordObservableInstruments();

		// Assert
		_measurements.Count.ShouldBeGreaterThanOrEqualTo(4);
	}

	[Fact]
	public void TrackBytesProcessed()
	{
		// Arrange
		var counter = new MetricCounter<long>(_meter, "bytes_processed_total", "bytes", "Total bytes processed");

		// Act
		counter.Add(1024, new TagList { { "direction", "inbound" } });
		counter.Add(2048, new TagList { { "direction", "outbound" } });
		_listener.RecordObservableInstruments();

		// Assert
		_measurements.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	#endregion
}
