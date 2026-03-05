// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Functional tests for <see cref="CircuitBreakerMetrics"/> verifying actual metric instrument behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class CircuitBreakerMetricsFunctionalShould : IDisposable
{
	private readonly CircuitBreakerMetrics _metrics = new();
	private readonly MeterListener _listener = new();
	private readonly object _recordingGate = new();
	private readonly Dictionary<string, List<(object Value, KeyValuePair<string, object?>[] Tags)>> _recorded = new(StringComparer.Ordinal);

	public CircuitBreakerMetricsFunctionalShould()
	{
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (ReferenceEquals(instrument.Meter, _metrics.Meter))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
		{
			RecordMeasurement(instrument.Name, measurement, tags);
		});

		_listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, _) =>
		{
			RecordMeasurement(instrument.Name, measurement, tags);
		});

		_listener.Start();
	}

	public void Dispose()
	{
		_listener.Dispose();
		_metrics.Dispose();
	}

	[Fact]
	public void UseCorrectMeterName()
	{
		_metrics.Meter.Name.ShouldBe("Excalibur.Dispatch.CircuitBreaker");
	}

	[Fact]
	public void RecordStateChange_EmitsCounterWithTransitionTags()
	{
		var circuitName = $"order-service-{Guid.NewGuid():N}";
		_metrics.RecordStateChange(circuitName, "Closed", "Open");

		var entries = GetRecorded("dispatch.circuitbreaker.state_changes");
		entries.ShouldNotBeEmpty();
		entries.Any(static entry => (long)entry.Value == 1).ShouldBeTrue();
		entries.Any(entry => entry.Tags.Any(tag =>
			tag.Key == "circuit_name" &&
			string.Equals(tag.Value as string, circuitName, StringComparison.Ordinal)))
			.ShouldBeTrue();
		entries.Any(entry => entry.Tags.Any(tag =>
			tag.Key == "previous_state" &&
			string.Equals(tag.Value as string, "Closed", StringComparison.Ordinal)))
			.ShouldBeTrue();
		entries.Any(entry => entry.Tags.Any(tag =>
			tag.Key == "new_state" &&
			string.Equals(tag.Value as string, "Open", StringComparison.Ordinal)))
			.ShouldBeTrue();
	}

	[Fact]
	public void RecordRejection_EmitsCounterWithCircuitName()
	{
		var circuitName = $"payment-circuit-{Guid.NewGuid():N}";
		_metrics.RecordRejection(circuitName);

		var entries = GetRecorded("dispatch.circuitbreaker.rejections");
		entries.ShouldNotBeEmpty();
		entries.Any(entry => entry.Tags.Any(tag =>
			tag.Key == "circuit_name" &&
			string.Equals(tag.Value as string, circuitName, StringComparison.Ordinal)))
			.ShouldBeTrue();
	}

	[Fact]
	public void RecordFailure_EmitsCounterWithExceptionType()
	{
		var circuitName = $"api-circuit-{Guid.NewGuid():N}";
		_metrics.RecordFailure(circuitName, "TimeoutException");

		var entries = GetRecorded("dispatch.circuitbreaker.failures");
		entries.ShouldNotBeEmpty();
		entries.Any(entry =>
				entry.Tags.Any(t => t.Key == "circuit_name" && string.Equals(t.Value as string, circuitName, StringComparison.Ordinal)) &&
				entry.Tags.Any(t => t.Key == "exception_type" && string.Equals(t.Value as string, "TimeoutException", StringComparison.Ordinal)))
			.ShouldBeTrue();
	}

	[Fact]
	public void RecordSuccess_EmitsCounterWithCircuitName()
	{
		var circuitName = $"db-circuit-{Guid.NewGuid():N}";
		_metrics.RecordSuccess(circuitName);

		var entries = GetRecorded("dispatch.circuitbreaker.successes");
		entries.ShouldNotBeEmpty();
		entries.Any(entry =>
				(long)entry.Value == 1 &&
				entry.Tags.Any(t => t.Key == "circuit_name" && string.Equals(t.Value as string, circuitName, StringComparison.Ordinal)))
			.ShouldBeTrue();
	}

	[Fact]
	public void UpdateState_ReportsViaObservableGauge()
	{
		var circuitName = $"test-circuit-{Guid.NewGuid():N}";
		_metrics.UpdateState(circuitName, 1); // Open

		var entries = GetRecorded("dispatch.circuitbreaker.state");
		entries.ShouldNotBeEmpty();
		entries.Any(entry =>
				(int)entry.Value == 1 &&
				entry.Tags.Any(t => t.Key == "circuit_name" && string.Equals(t.Value as string, circuitName, StringComparison.Ordinal)))
			.ShouldBeTrue();
	}

	[Fact]
	public void TrackMultipleCircuitStates()
	{
		var suffix = Guid.NewGuid().ToString("N");
		var circuitA = $"circuit-a-{suffix}";
		var circuitB = $"circuit-b-{suffix}";
		var circuitC = $"circuit-c-{suffix}";

		_metrics.UpdateState(circuitA, 0); // Closed
		_metrics.UpdateState(circuitB, 1); // Open
		_metrics.UpdateState(circuitC, 2); // HalfOpen

		var entries = GetRecorded("dispatch.circuitbreaker.state", minimumCount: 3);
		entries.Any(entry => (int)entry.Value == 0 &&
			entry.Tags.Any(tag => tag.Key == "circuit_name" && string.Equals(tag.Value as string, circuitA, StringComparison.Ordinal))).ShouldBeTrue();
		entries.Any(entry => (int)entry.Value == 1 &&
			entry.Tags.Any(tag => tag.Key == "circuit_name" && string.Equals(tag.Value as string, circuitB, StringComparison.Ordinal))).ShouldBeTrue();
		entries.Any(entry => (int)entry.Value == 2 &&
			entry.Tags.Any(tag => tag.Key == "circuit_name" && string.Equals(tag.Value as string, circuitC, StringComparison.Ordinal))).ShouldBeTrue();
	}

	[Fact]
	public void SupportMeterFactoryConstructor()
	{
		var factory = new TestMeterFactory();
		using var metricsWithFactory = new CircuitBreakerMetrics(factory);

		metricsWithFactory.Meter.ShouldNotBeNull();
		metricsWithFactory.Meter.Name.ShouldBe(CircuitBreakerMetrics.MeterName);
	}

	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() => new CircuitBreakerMetrics((IMeterFactory)null!));
	}

	private void RecordMeasurement(string instrumentName, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
	{
		lock (_recordingGate)
		{
			if (!_recorded.TryGetValue(instrumentName, out var list))
			{
				list = [];
				_recorded[instrumentName] = list;
			}

			list.Add((value, tags.ToArray()));
		}
	}

	private List<(object Value, KeyValuePair<string, object?>[] Tags)> GetRecorded(string instrumentName, int minimumCount = 1)
	{
		var observed = global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() =>
			{
				_listener.RecordObservableInstruments();
				lock (_recordingGate)
				{
					return _recorded.TryGetValue(instrumentName, out var list) && list.Count >= minimumCount;
				}
			},
			TimeSpan.FromSeconds(2),
			TimeSpan.FromMilliseconds(10)).GetAwaiter().GetResult();

		observed.ShouldBeTrue($"Expected at least {minimumCount} metric samples for '{instrumentName}'.");
		lock (_recordingGate)
		{
			return (_recorded.GetValueOrDefault(instrumentName) ?? []).ToList();
		}
	}
}
