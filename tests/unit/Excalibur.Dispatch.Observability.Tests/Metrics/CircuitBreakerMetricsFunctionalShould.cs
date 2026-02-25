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
	private readonly Dictionary<string, List<(object Value, KeyValuePair<string, object?>[] Tags)>> _recorded = new(StringComparer.Ordinal);

	public CircuitBreakerMetricsFunctionalShould()
	{
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == CircuitBreakerMetrics.MeterName)
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
		_metrics.RecordStateChange("order-service", "Closed", "Open");

		var entries = GetRecorded("dispatch.circuitbreaker.state_changes");
		entries.ShouldNotBeEmpty();
		entries.Any(static entry => (long)entry.Value == 1).ShouldBeTrue();
		entries.Any(entry => entry.Tags.Any(tag =>
			tag.Key == "circuit_name" &&
			string.Equals(tag.Value as string, "order-service", StringComparison.Ordinal)))
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
		_metrics.RecordRejection("payment-circuit");

		var entries = GetRecorded("dispatch.circuitbreaker.rejections");
		entries.ShouldNotBeEmpty();
		entries.Any(entry => entry.Tags.Any(tag =>
			tag.Key == "circuit_name" &&
			string.Equals(tag.Value as string, "payment-circuit", StringComparison.Ordinal)))
			.ShouldBeTrue();
	}

	[Fact]
	public void RecordFailure_EmitsCounterWithExceptionType()
	{
		_metrics.RecordFailure("api-circuit", "TimeoutException");

		var entries = GetRecorded("dispatch.circuitbreaker.failures");
		entries.ShouldNotBeEmpty();
		var (_, tags) = entries[0];
		tags.ShouldContain(t => t.Key == "circuit_name" && (string)t.Value! == "api-circuit");
		tags.ShouldContain(t => t.Key == "exception_type" && (string)t.Value! == "TimeoutException");
	}

	[Fact]
	public void RecordSuccess_EmitsCounterWithCircuitName()
	{
		_metrics.RecordSuccess("db-circuit");

		var entries = GetRecorded("dispatch.circuitbreaker.successes");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries[0];
		((long)value).ShouldBe(1);
		tags.ShouldContain(t => t.Key == "circuit_name" && (string)t.Value! == "db-circuit");
	}

	[Fact]
	public void UpdateState_ReportsViaObservableGauge()
	{
		_metrics.UpdateState("test-circuit", 1); // Open

		var entries = GetRecorded("dispatch.circuitbreaker.state");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries[0];
		((int)value).ShouldBe(1);
		tags.ShouldContain(t => t.Key == "circuit_name" && (string)t.Value! == "test-circuit");
	}

	[Fact]
	public void TrackMultipleCircuitStates()
	{
		_metrics.UpdateState("circuit-a", 0); // Closed
		_metrics.UpdateState("circuit-b", 1); // Open
		_metrics.UpdateState("circuit-c", 2); // HalfOpen

		var entries = GetRecorded("dispatch.circuitbreaker.state");
		entries.Count.ShouldBe(3);
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
		if (!_recorded.TryGetValue(instrumentName, out var list))
		{
			list = [];
			_recorded[instrumentName] = list;
		}

		list.Add((value, tags.ToArray()));
	}

	private List<(object Value, KeyValuePair<string, object?>[] Tags)> GetRecorded(string instrumentName)
	{
		_listener.RecordObservableInstruments();
		return _recorded.GetValueOrDefault(instrumentName) ?? [];
	}
}
