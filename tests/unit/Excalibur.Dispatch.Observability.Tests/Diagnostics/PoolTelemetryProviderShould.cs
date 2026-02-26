// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Pooling.Telemetry;

namespace Excalibur.Dispatch.Observability.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="PoolTelemetryProvider"/>.
/// Validates pool telemetry metrics recording for rent/return operations
/// and duration measurements via System.Diagnostics.Metrics.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "PoolTelemetry")]
public sealed class PoolTelemetryProviderShould : IDisposable
{
	private readonly string _defaultMeterName = $"Excalibur.Dispatch.Pooling.{Guid.NewGuid():N}";
	private readonly string _customMeterName = $"Test.Custom.Pooling.{Guid.NewGuid():N}";

	private PoolTelemetryProvider? _sut;
	private MeterListener? _listener;

	public void Dispose()
	{
		_listener?.Dispose();
		_sut?.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void CreateInstanceWithDefaultMeterName()
	{
		// Arrange & Act
		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Assert
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateInstanceWithCustomMeterName()
	{
		// Arrange & Act
		_sut = new PoolTelemetryProvider(_customMeterName);

		// Assert
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateMeterWithDefaultName()
	{
		// Arrange
		string? capturedMeterName = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			// Only capture from our expected meter to avoid cross-test interference
			// when other tests publish instruments from different meters in parallel
			if (instrument.Meter.Name == _defaultMeterName)
			{
				capturedMeterName = instrument.Meter.Name;
			}

			listener.EnableMeasurementEvents(instrument);
		};
		_listener.Start();

		// Act
		_sut = new PoolTelemetryProvider(_defaultMeterName);
		_sut.RecordRent("test-pool", 1.0); // Trigger instrument publication

		// Assert
		capturedMeterName.ShouldBe(_defaultMeterName);
	}

	[Fact]
	public void CreateMeterWithCustomName()
	{
		// Arrange
		string? capturedMeterName = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			// Only capture from our expected meter to avoid cross-test interference
			// when other tests publish instruments from different meters in parallel
			if (instrument.Meter.Name == _customMeterName)
			{
				capturedMeterName = instrument.Meter.Name;
			}

			listener.EnableMeasurementEvents(instrument);
		};
		_listener.Start();

		// Act
		_sut = new PoolTelemetryProvider(_customMeterName);
		_sut.RecordRent("test-pool", 1.0);

		// Assert
		capturedMeterName.ShouldBe(_customMeterName);
	}

	[Fact]
	public void CreateMeterWithVersion()
	{
		// Arrange
		string? capturedVersion = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			// Only capture from our expected meter to avoid cross-test interference
			if (instrument.Meter.Name == _defaultMeterName)
			{
				capturedVersion = instrument.Meter.Version;
			}

			listener.EnableMeasurementEvents(instrument);
		};
		_listener.Start();

		// Act
		_sut = new PoolTelemetryProvider(_defaultMeterName);
		_sut.RecordRent("test-pool", 1.0);

		// Assert
		capturedVersion.ShouldBe("1.0.0");
	}

	#endregion

	#region Instrument Registration Tests

	[Fact]
	public void RegisterRentCounterInstrument()
	{
		// Arrange
		var instrumentNames = new ConcurrentBag<string>();
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName)
			{
				instrumentNames.Add(instrument.Name);
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.Start();

		// Act
		_sut = new PoolTelemetryProvider(_defaultMeterName);
		_sut.RecordRent("test-pool", 1.0);

		// Assert
		instrumentNames.ShouldContain("dispatch.pool.rent.count");
	}

	[Fact]
	public void RegisterReturnCounterInstrument()
	{
		// Arrange
		var instrumentNames = new ConcurrentBag<string>();
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName)
			{
				instrumentNames.Add(instrument.Name);
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.Start();

		// Act
		_sut = new PoolTelemetryProvider(_defaultMeterName);
		_sut.RecordReturn("test-pool");

		// Assert
		instrumentNames.ShouldContain("dispatch.pool.return.count");
	}

	[Fact]
	public void RegisterRentDurationHistogramInstrument()
	{
		// Arrange
		var instrumentNames = new ConcurrentBag<string>();
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName)
			{
				instrumentNames.Add(instrument.Name);
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.Start();

		// Act
		_sut = new PoolTelemetryProvider(_defaultMeterName);
		_sut.RecordRent("test-pool", 5.0);

		// Assert
		instrumentNames.ShouldContain("dispatch.pool.rent.duration");
	}

	[Fact]
	public void RegisterRentDurationHistogramWithMillisecondUnit()
	{
		// Arrange
		string? capturedUnit = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.duration")
			{
				capturedUnit = instrument.Unit;
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.Start();

		// Act
		_sut = new PoolTelemetryProvider(_defaultMeterName);
		_sut.RecordRent("test-pool", 5.0);

		// Assert
		capturedUnit.ShouldBe("ms");
	}

	#endregion

	#region RecordRent Tests

	[Fact]
	public void RecordRent_IncrementRentCounter()
	{
		// Arrange
		long capturedValue = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count")
			{
				Interlocked.Add(ref capturedValue, measurement);
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent("my-pool", 2.5);

		// Assert
		capturedValue.ShouldBe(1);
	}

	[Fact]
	public void RecordRent_RecordDurationHistogram()
	{
		// Arrange
		double capturedDuration = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.duration")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.duration")
			{
				capturedDuration = measurement;
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent("my-pool", 42.7);

		// Assert
		capturedDuration.ShouldBe(42.7);
	}

	[Fact]
	public void RecordRent_IncludePoolNameTag()
	{
		// Arrange
		string? capturedPoolTag = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count")
			{
				foreach (var tag in tags)
				{
					if (tag.Key == "pool")
					{
						capturedPoolTag = tag.Value?.ToString();
					}
				}
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent("orders-pool", 1.0);

		// Assert
		capturedPoolTag.ShouldBe("orders-pool");
	}

	[Fact]
	public void RecordRent_IncludePoolNameTagOnDuration()
	{
		// Arrange
		string? capturedPoolTag = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.duration")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.duration")
			{
				foreach (var tag in tags)
				{
					if (tag.Key == "pool")
					{
						capturedPoolTag = tag.Value?.ToString();
					}
				}
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent("events-pool", 10.5);

		// Assert
		capturedPoolTag.ShouldBe("events-pool");
	}

	[Fact]
	public void RecordRent_AccumulateMultipleCalls()
	{
		// Arrange
		long totalCount = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count")
			{
				Interlocked.Add(ref totalCount, measurement);
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent("pool-a", 1.0);
		_sut.RecordRent("pool-a", 2.0);
		_sut.RecordRent("pool-b", 3.0);

		// Assert
		totalCount.ShouldBe(3);
	}

	[Fact]
	public void RecordRent_WithZeroDuration()
	{
		// Arrange
		double capturedDuration = -1;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.duration")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.duration")
			{
				capturedDuration = measurement;
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent("fast-pool", 0.0);

		// Assert
		capturedDuration.ShouldBe(0.0);
	}

	[Fact]
	public void RecordRent_WithVeryLargeDuration()
	{
		// Arrange
		double capturedDuration = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.duration")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.duration")
			{
				capturedDuration = measurement;
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent("slow-pool", 60000.0);

		// Assert
		capturedDuration.ShouldBe(60000.0);
	}

	[Theory]
	[InlineData("pool-alpha")]
	[InlineData("pool-beta")]
	[InlineData("connections")]
	[InlineData("handlers")]
	public void RecordRent_WithVariousPoolNames(string poolName)
	{
		// Arrange
		string? capturedPool = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count")
			{
				foreach (var tag in tags)
				{
					if (tag.Key == "pool")
					{
						capturedPool = tag.Value?.ToString();
					}
				}
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent(poolName, 1.0);

		// Assert
		capturedPool.ShouldBe(poolName);
	}

	#endregion

	#region RecordReturn Tests

	[Fact]
	public void RecordReturn_IncrementReturnCounter()
	{
		// Arrange
		long capturedValue = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.return.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.return.count")
			{
				Interlocked.Add(ref capturedValue, measurement);
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordReturn("my-pool");

		// Assert
		capturedValue.ShouldBe(1);
	}

	[Fact]
	public void RecordReturn_IncludePoolNameTag()
	{
		// Arrange
		string? capturedPoolTag = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.return.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.return.count")
			{
				foreach (var tag in tags)
				{
					if (tag.Key == "pool")
					{
						capturedPoolTag = tag.Value?.ToString();
					}
				}
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordReturn("orders-pool");

		// Assert
		capturedPoolTag.ShouldBe("orders-pool");
	}

	[Fact]
	public void RecordReturn_AccumulateMultipleCalls()
	{
		// Arrange
		long totalCount = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.return.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.return.count")
			{
				Interlocked.Add(ref totalCount, measurement);
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordReturn("pool-a");
		_sut.RecordReturn("pool-a");
		_sut.RecordReturn("pool-b");

		// Assert
		totalCount.ShouldBe(3);
	}

	[Theory]
	[InlineData("pool-alpha")]
	[InlineData("pool-beta")]
	[InlineData("connections")]
	[InlineData("handlers")]
	public void RecordReturn_WithVariousPoolNames(string poolName)
	{
		// Arrange
		string? capturedPool = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.return.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.return.count")
			{
				foreach (var tag in tags)
				{
					if (tag.Key == "pool")
					{
						capturedPool = tag.Value?.ToString();
					}
				}
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordReturn(poolName);

		// Assert
		capturedPool.ShouldBe(poolName);
	}

	#endregion

	#region Combined Rent/Return Workflow Tests

	[Fact]
	public void TrackRentAndReturnInSameWorkflow()
	{
		// Arrange
		long rentCount = 0;
		long returnCount = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count")
			{
				Interlocked.Add(ref rentCount, measurement);
			}
			else if (instrument.Name == "dispatch.pool.return.count")
			{
				Interlocked.Add(ref returnCount, measurement);
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act - Simulate a typical rent-use-return cycle
		_sut.RecordRent("worker-pool", 0.5);
		_sut.RecordRent("worker-pool", 0.3);
		_sut.RecordReturn("worker-pool");
		_sut.RecordReturn("worker-pool");

		// Assert
		rentCount.ShouldBe(2);
		returnCount.ShouldBe(2);
	}

	[Fact]
	public void TrackMultiplePoolsSeparately()
	{
		// Arrange
		var tagValues = new ConcurrentBag<string>();
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count")
			{
				foreach (var tag in tags)
				{
					if (tag.Key == "pool" && tag.Value is string s)
					{
						tagValues.Add(s);
					}
				}
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act
		_sut.RecordRent("pool-a", 1.0);
		_sut.RecordRent("pool-b", 2.0);
		_sut.RecordRent("pool-c", 3.0);

		// Assert
		tagValues.ShouldContain("pool-a");
		tagValues.ShouldContain("pool-b");
		tagValues.ShouldContain("pool-c");
		tagValues.Count.ShouldBe(3);
	}

	#endregion

	#region Disposal Tests

	[Fact]
	public void ImplementIDisposable()
	{
		// Arrange
		var provider = new PoolTelemetryProvider(_defaultMeterName);

		// Assert
		provider.ShouldBeAssignableTo<IDisposable>();

		// Cleanup
		provider.Dispose();
	}

	[Fact]
	public void DisposeWithoutError()
	{
		// Arrange
		var provider = new PoolTelemetryProvider(_defaultMeterName);

		// Act & Assert
		Should.NotThrow(() => provider.Dispose());
	}

	[Fact]
	public void DisposeMultipleTimesWithoutError()
	{
		// Arrange
		var provider = new PoolTelemetryProvider(_defaultMeterName);

		// Act & Assert - Double dispose should be safe
		Should.NotThrow(() =>
		{
			provider.Dispose();
			provider.Dispose();
		});
	}

	[Fact]
	public void StopEmittingMetricsAfterDispose()
	{
		// Arrange
		long postDisposeCount = 0;
		var disposed = false;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count" && disposed)
			{
				Interlocked.Add(ref postDisposeCount, measurement);
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act - Record before dispose, then dispose, then attempt to record
		_sut.RecordRent("test-pool", 1.0);
		_sut.Dispose();
		disposed = true;

		// After disposal, the meter is disposed, so recording should not emit
		// Note: Calling RecordRent after Dispose may or may not throw depending
		// on the counter implementation; we just verify no new measurements were emitted
		// The counter Add call on a disposed meter is a no-op in System.Diagnostics.Metrics
		_sut.RecordRent("test-pool", 1.0);

		// Assert
		postDisposeCount.ShouldBe(0);

		// Prevent double dispose in test cleanup
		_sut = null;
	}

	#endregion

	#region Thread-Safety Tests

	[Fact]
	public async Task RecordRent_Concurrently_WithoutErrors()
	{
		// Arrange
		_sut = new PoolTelemetryProvider(_defaultMeterName);
		var exceptions = new ConcurrentBag<Exception>();

		// Act - Concurrent rent recordings from multiple threads
		var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
		{
			try
			{
				for (var j = 0; j < 100; j++)
				{
					_sut.RecordRent($"pool-{i}", j * 0.1);
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		exceptions.ShouldBeEmpty();
	}

	[Fact]
	public async Task RecordReturn_Concurrently_WithoutErrors()
	{
		// Arrange
		_sut = new PoolTelemetryProvider(_defaultMeterName);
		var exceptions = new ConcurrentBag<Exception>();

		// Act - Concurrent return recordings from multiple threads
		var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
		{
			try
			{
				for (var j = 0; j < 100; j++)
				{
					_sut.RecordReturn($"pool-{i}");
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		exceptions.ShouldBeEmpty();
	}

	[Fact]
	public async Task MixedRentAndReturn_Concurrently_WithoutErrors()
	{
		// Arrange
		long totalRents = 0;
		long totalReturns = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count")
			{
				Interlocked.Add(ref totalRents, measurement);
			}
			else if (instrument.Name == "dispatch.pool.return.count")
			{
				Interlocked.Add(ref totalReturns, measurement);
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);
		var exceptions = new ConcurrentBag<Exception>();

		// Act - Mixed concurrent rent and return operations
		var rentTasks = Enumerable.Range(0, 5).Select(i => Task.Run(() =>
		{
			try
			{
				for (var j = 0; j < 50; j++)
				{
					_sut.RecordRent($"pool-{i}", j * 0.5);
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}));

		var returnTasks = Enumerable.Range(0, 5).Select(i => Task.Run(() =>
		{
			try
			{
				for (var j = 0; j < 50; j++)
				{
					_sut.RecordReturn($"pool-{i}");
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}));

		await Task.WhenAll(rentTasks.Concat(returnTasks)).ConfigureAwait(false);

		// Assert
		exceptions.ShouldBeEmpty();
		totalRents.ShouldBe(250); // 5 threads x 50 operations
		totalReturns.ShouldBe(250); // 5 threads x 50 operations
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void RecordRent_WithEmptyPoolName()
	{
		// Arrange
		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act & Assert - Should not throw
		Should.NotThrow(() => _sut.RecordRent(string.Empty, 1.0));
	}

	[Fact]
	public void RecordReturn_WithEmptyPoolName()
	{
		// Arrange
		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act & Assert - Should not throw
		Should.NotThrow(() => _sut.RecordReturn(string.Empty));
	}

	[Fact]
	public void RecordRent_WithNegativeDuration()
	{
		// Arrange
		double capturedDuration = 0;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.duration")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.duration")
			{
				capturedDuration = measurement;
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);

		// Act - Negative duration (edge case; should still record)
		_sut.RecordRent("pool", -1.0);

		// Assert
		capturedDuration.ShouldBe(-1.0);
	}

	[Fact]
	public void RecordRent_WithSpecialCharactersInPoolName()
	{
		// Arrange
		string? capturedPool = null;
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == _defaultMeterName && instrument.Name == "dispatch.pool.rent.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "dispatch.pool.rent.count")
			{
				foreach (var tag in tags)
				{
					if (tag.Key == "pool")
					{
						capturedPool = tag.Value?.ToString();
					}
				}
			}
		});
		_listener.Start();

		_sut = new PoolTelemetryProvider(_defaultMeterName);
		const string specialName = "pool/with:special.chars-and_underscores";

		// Act
		_sut.RecordRent(specialName, 1.0);

		// Assert
		capturedPool.ShouldBe(specialName);
	}

	#endregion

	#region Sealed Class Verification

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(PoolTelemetryProvider).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void NotBeAbstract()
	{
		// Assert
		typeof(PoolTelemetryProvider).IsAbstract.ShouldBeFalse();
	}

	#endregion
}

