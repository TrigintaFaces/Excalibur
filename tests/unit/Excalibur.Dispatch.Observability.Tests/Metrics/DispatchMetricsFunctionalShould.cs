// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Functional tests for <see cref="DispatchMetrics"/> verifying actual metric instrument behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class DispatchMetricsFunctionalShould : IDisposable
{
	private readonly DispatchMetrics _metrics = new();
	private readonly MeterListener _listener = new();
	private readonly object _recordingGate = new();
	private readonly Dictionary<string, List<(object Value, KeyValuePair<string, object?>[] Tags)>> _recorded = new(StringComparer.Ordinal);

	public DispatchMetricsFunctionalShould()
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

		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
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
		_metrics.Meter.Name.ShouldBe(DispatchMetrics.MeterName);
	}

	[Fact]
	public void RecordMessageProcessed_EmitsCounterWithTags()
	{
		var messageType = $"TestCommand-{Guid.NewGuid():N}";
		var handlerType = $"TestHandler-{Guid.NewGuid():N}";
		_metrics.RecordMessageProcessed(messageType, handlerType);

		var entries = GetRecorded("dispatch.messages.processed");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && string.Equals(t.Value as string, messageType, StringComparison.Ordinal)) &&
			e.Tags.Any(t => t.Key == "handler_type" && string.Equals(t.Value as string, handlerType, StringComparison.Ordinal)));
		((long)value).ShouldBe(1);
		tags.ShouldContain(t => t.Key == "message_type" && string.Equals(t.Value as string, messageType, StringComparison.Ordinal));
		tags.ShouldContain(t => t.Key == "handler_type" && string.Equals(t.Value as string, handlerType, StringComparison.Ordinal));
	}

	[Fact]
	public void RecordProcessingDuration_EmitsHistogramWithSuccessTag()
	{
		var messageType = $"MyMessage-{Guid.NewGuid():N}";
		_metrics.RecordProcessingDuration(42.5, messageType, success: true);

		var entries = GetRecorded("dispatch.messages.duration");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && string.Equals(t.Value as string, messageType, StringComparison.Ordinal)) &&
			e.Tags.Any(t => t.Key == "success" && (bool)t.Value!));
		((double)value).ShouldBe(42.5);
		tags.ShouldContain(t => t.Key == "message_type" && string.Equals(t.Value as string, messageType, StringComparison.Ordinal));
		tags.ShouldContain(t => t.Key == "success" && (bool)t.Value! == true);
	}

	[Fact]
	public void RecordProcessingDuration_EmitsHistogramWithFailureTag()
	{
		_metrics.RecordProcessingDuration(100.0, "FailMessage", success: false);

		var entries = GetRecorded("dispatch.messages.duration");
		entries.ShouldNotBeEmpty();
		var (_, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && (string)t.Value! == "FailMessage"));
		tags.ShouldContain(t => t.Key == "success" && (bool)t.Value! == false);
	}

	[Fact]
	public void RecordMessagePublished_EmitsCounterWithDestination()
	{
		var messageType = $"OrderCreated-{Guid.NewGuid():N}";
		var destination = $"orders-queue-{Guid.NewGuid():N}";
		_metrics.RecordMessagePublished(messageType, destination);

		var entries = GetRecorded("dispatch.messages.published");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && string.Equals(t.Value as string, messageType, StringComparison.Ordinal)) &&
			e.Tags.Any(t => t.Key == "destination" && string.Equals(t.Value as string, destination, StringComparison.Ordinal)));
		((long)value).ShouldBe(1);
		tags.ShouldContain(t => t.Key == "destination" && string.Equals(t.Value as string, destination, StringComparison.Ordinal));
	}

	[Fact]
	public void RecordMessageFailed_EmitsCounterWithErrorTypeAndRetry()
	{
		var messageType = $"TestMessage-{Guid.NewGuid():N}";
		var errorType = $"TimeoutException-{Guid.NewGuid():N}";
		_metrics.RecordMessageFailed(messageType, errorType, retryAttempt: 3);

		var entries = GetRecorded("dispatch.messages.failed");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && string.Equals(t.Value as string, messageType, StringComparison.Ordinal)) &&
			e.Tags.Any(t => t.Key == "error_type" && string.Equals(t.Value as string, errorType, StringComparison.Ordinal)) &&
			e.Tags.Any(t => t.Key == "retry_attempt" && (int)t.Value! == 3));
		((long)value).ShouldBe(1);
		tags.ShouldContain(t => t.Key == "error_type" && string.Equals(t.Value as string, errorType, StringComparison.Ordinal));
		tags.ShouldContain(t => t.Key == "retry_attempt" && (int)t.Value! == 3);
	}

	[Fact]
	public void UpdateActiveSessions_EmitsUpDownCounter()
	{
		_metrics.UpdateActiveSessions(1);
		_metrics.UpdateActiveSessions(1);
		_metrics.UpdateActiveSessions(-1);

		var entries = GetRecorded("dispatch.sessions.active", minimumCount: 3);
		entries.Count.ShouldBe(3);
	}

	[Fact]
	public void RecordMultipleProcessedMessages_AccumulatesCorrectly()
	{
		_metrics.RecordMessageProcessed("A", "HandlerA");
		_metrics.RecordMessageProcessed("B", "HandlerB");
		_metrics.RecordMessageProcessed("A", "HandlerA");

		var entries = GetRecorded("dispatch.messages.processed", minimumCount: 3);
		entries.Count.ShouldBe(3);
	}

	[Fact]
	public void AcceptAdditionalTags_OnRecordMessageProcessed()
	{
		var messageType = $"TestMsg-{Guid.NewGuid():N}";
		var handlerType = $"TestHandler-{Guid.NewGuid():N}";
		var region = $"us-east-{Random.Shared.Next(1, 10)}";
		_metrics.RecordMessageProcessed(messageType, handlerType, ("region", region));

		var entries = GetRecorded("dispatch.messages.processed");
		entries.ShouldNotBeEmpty();
		var (_, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && string.Equals(t.Value as string, messageType, StringComparison.Ordinal)) &&
			e.Tags.Any(t => t.Key == "handler_type" && string.Equals(t.Value as string, handlerType, StringComparison.Ordinal)));
		tags.ShouldContain(t => t.Key == "region" && string.Equals(t.Value as string, region, StringComparison.Ordinal));
	}

	[Fact]
	public void ThrowOnNullTags_ForRecordMessageProcessed()
	{
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordMessageProcessed("Msg", "Handler", null!));
	}

	[Fact]
	public void SupportMeterFactoryConstructor()
	{
		var factory = new TestMeterFactory();
		using var metricsWithFactory = new DispatchMetrics(factory);

		metricsWithFactory.Meter.ShouldNotBeNull();
		metricsWithFactory.Meter.Name.ShouldBe(DispatchMetrics.MeterName);
	}

	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() => new DispatchMetrics((IMeterFactory)null!));
	}

	[Fact]
	public void NotDisposeMeter_WhenCreatedFromFactory()
	{
		// When created from IMeterFactory, the factory owns the meter lifecycle
		var factory = new TestMeterFactory();
		var metricsWithFactory = new DispatchMetrics(factory);
		metricsWithFactory.Dispose();

		// Should not throw - meter still usable since factory owns it
		metricsWithFactory.Meter.ShouldNotBeNull();
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
