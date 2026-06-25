// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Regression lock for <c>p9w9vk</c> (Sprint 846, MS-D): <see cref="CircuitBreakerMiddleware"/> MUST emit OTel
/// metrics on circuit-breaker state transitions and rejections — the single most important resilience signal,
/// previously only a Warning log + Activity tag (no metric to alert on). Binds the verbatim MS-D contract:
/// meter <c>Excalibur.Dispatch.CircuitBreakerMiddleware</c>; counter <c>dispatch.circuit_breaker.transitions</c>
/// (tags <c>circuit.key</c>/<c>from_state</c>/<c>to_state</c>) + <c>dispatch.circuit_breaker.rejections</c>
/// (tag <c>circuit.key</c>); lowercase state values <c>closed</c>/<c>open</c>/<c>half_open</c>.
/// </summary>
/// <remarks>
/// Non-vacuity: every metric assertion is <b>RED on the pre-fix middleware</b> (which emits no <c>Meter</c>/
/// <c>Counter</c> at all — only logs/Activity tags), GREEN once the counters are wired. Each test uses a distinct
/// <c>circuit.key</c> so assertions are isolated from the process-wide static meter (no cross-test pollution).
/// Transitions are driven deterministically via <see cref="CircuitBreakerOptions"/> (threshold + OpenDuration),
/// never wall-clock waits.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class CircuitBreakerMiddlewareMetricsShould
{
    private const string MeterName = "Excalibur.Dispatch.CircuitBreakerMiddleware";
    private const string TransitionsCounter = "dispatch.circuit_breaker.transitions";
    private const string RejectionsCounter = "dispatch.circuit_breaker.rejections";

    private static readonly ITelemetrySanitizer Sanitizer = A.Fake<ITelemetrySanitizer>();

    private static CircuitBreakerMiddleware CreateSut(CircuitBreakerOptions options, ILogger<CircuitBreakerMiddleware>? logger = null)
    {
        A.CallTo(() => Sanitizer.SanitizeTag(A<string>._, A<string?>._)).ReturnsLazily(call => call.GetArgument<string?>(1));
        return new CircuitBreakerMiddleware(
            Microsoft.Extensions.Options.Options.Create(options),
            Sanitizer,
            logger ?? new ListLogger<CircuitBreakerMiddleware>());
    }

    private static CircuitBreakerOptions Options(string key, int failureThreshold = 1, int successThreshold = 1, TimeSpan? openDuration = null) =>
        new()
        {
            FailureThreshold = failureThreshold,
            SuccessThreshold = successThreshold,
            OpenDuration = openDuration ?? TimeSpan.FromSeconds(60),
            CircuitKeySelector = _ => key,
        };

    private static async ValueTask<IMessageResult> DriveFailureAsync(CircuitBreakerMiddleware sut) =>
        await sut.InvokeAsync(
            A.Fake<IDispatchMessage>(), new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
            {
                Type = "Error", Title = "Error", ErrorCode = 500, Status = 500, Detail = "boom",
            })),
            CancellationToken.None);

    private static async ValueTask<IMessageResult> DriveSuccessAsync(CircuitBreakerMiddleware sut) =>
        await sut.InvokeAsync(
            A.Fake<IDispatchMessage>(), new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

    // ---- AC-D1: a Closed→Open transition records the transitions counter with from/to/key tags. ----

    [Fact]
    public async Task AC_D1_ClosedToOpen_RecordsTransitionCounter()
    {
        const string key = "acd1-closed-open";
        using var metrics = new MetricCollector(MeterName);
        var sut = CreateSut(Options(key, failureThreshold: 1));

        await DriveFailureAsync(sut); // 1 failure with threshold 1 → Closed→Open

        metrics.Count(TransitionsCounter, ("from_state", "closed"), ("to_state", "open"), ("circuit.key", key))
            .ShouldBe(1);
    }

    // ---- AC-D2: a rejection on an open circuit records the rejections counter with the key tag. ----

    [Fact]
    public async Task AC_D2_OpenCircuitRejection_RecordsRejectionCounter()
    {
        const string key = "acd2-rejection";
        using var metrics = new MetricCollector(MeterName);
        var sut = CreateSut(Options(key, failureThreshold: 1, openDuration: TimeSpan.FromSeconds(60)));

        await DriveFailureAsync(sut);   // → Open
        await DriveSuccessAsync(sut);   // Open + within OpenDuration → rejected at the guard

        metrics.Count(RejectionsCounter, ("circuit.key", key)).ShouldBe(1);
    }

    // ---- AC-D3: the metric is ADDITIVE — the existing Warning log still fires on transition. ----

    [Fact]
    public async Task AC_D3_Transition_StillFiresExistingWarningLog()
    {
        const string key = "acd3-additive";
        using var metrics = new MetricCollector(MeterName);
        var logger = new ListLogger<CircuitBreakerMiddleware>();
        var sut = CreateSut(Options(key, failureThreshold: 1), logger);

        await DriveFailureAsync(sut); // Closed→Open

        // Additive (not replaced): both the metric AND the pre-existing Warning log fire.
        metrics.Count(TransitionsCounter, ("from_state", "closed"), ("to_state", "open"), ("circuit.key", key))
            .ShouldBe(1);
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning);
    }

    // ---- AC-D4: HalfOpen→Closed (recovery) and HalfOpen→Open (re-trip) each record with correct from/to. ----

    [Fact]
    public async Task AC_D4_HalfOpenToClosed_Recovery_RecordsTransition()
    {
        const string key = "acd4-recovery";
        using var metrics = new MetricCollector(MeterName);
        // OpenDuration zero → the next invoke after Open transitions to HalfOpen; SuccessThreshold 1 → one success closes.
        var sut = CreateSut(Options(key, failureThreshold: 1, successThreshold: 1, openDuration: TimeSpan.Zero));

        await DriveFailureAsync(sut);   // Closed→Open
        await DriveSuccessAsync(sut);   // Open→HalfOpen, then RecordSuccess → HalfOpen→Closed

        metrics.Count(TransitionsCounter, ("from_state", "half_open"), ("to_state", "closed"), ("circuit.key", key))
            .ShouldBe(1);
    }

    [Fact]
    public async Task AC_D4_HalfOpenToOpen_Retrip_RecordsTransition()
    {
        const string key = "acd4-retrip";
        using var metrics = new MetricCollector(MeterName);
        var sut = CreateSut(Options(key, failureThreshold: 1, openDuration: TimeSpan.Zero));

        await DriveFailureAsync(sut);   // Closed→Open
        await DriveFailureAsync(sut);   // Open→HalfOpen, then RecordFailure (threshold 1) → HalfOpen→Open

        metrics.Count(TransitionsCounter, ("from_state", "half_open"), ("to_state", "open"), ("circuit.key", key))
            .ShouldBe(1);
    }

    // ---- EC-D1: no MeterListener registered → the emission path is no-op-safe (no throw, result unchanged). ----

    [Fact]
    public async Task EC_D1_NoMeterListener_EmissionIsNoOpSafe()
    {
        const string key = "ecd1-nolistener";
        var sut = CreateSut(Options(key, failureThreshold: 1)); // no MetricCollector created

        var result = await DriveFailureAsync(sut); // transition still occurs; counter records to no listener

        _ = result; // the transition path must not throw with no listener attached
        result.Succeeded.ShouldBeFalse();
    }

    // ---- EC-D2: concurrent transitions on different keys produce independent, correctly-tagged counters. ----

    [Fact]
    public async Task EC_D2_DifferentKeys_RecordIndependentCounters()
    {
        const string keyA = "ecd2-key-a";
        const string keyB = "ecd2-key-b";
        using var metrics = new MetricCollector(MeterName);
        var sutA = CreateSut(Options(keyA, failureThreshold: 1));
        var sutB = CreateSut(Options(keyB, failureThreshold: 1));

        await DriveFailureAsync(sutA); // keyA Closed→Open
        await DriveFailureAsync(sutB); // keyB Closed→Open

        metrics.Count(TransitionsCounter, ("from_state", "closed"), ("to_state", "open"), ("circuit.key", keyA)).ShouldBe(1);
        metrics.Count(TransitionsCounter, ("from_state", "closed"), ("to_state", "open"), ("circuit.key", keyB)).ShouldBe(1);
    }

    #region Test Helpers

    /// <summary>
    /// Collects <see cref="long"/> measurements published to a specific meter via a <see cref="MeterListener"/>.
    /// </summary>
    private sealed class MetricCollector : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly object _lock = new();
        private readonly List<(string Name, long Value, Dictionary<string, object?> Tags)> _measurements = new();

        public MetricCollector(string meterName)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var tag in tags)
                {
                    dict[tag.Key] = tag.Value;
                }

                lock (_lock)
                {
                    _measurements.Add((instrument.Name, measurement, dict));
                }
            });

            _listener.Start();
        }

        /// <summary>Sums the recorded values for measurements on <paramref name="counterName"/> matching all tag pairs.</summary>
        public long Count(string counterName, params (string Key, string Value)[] tags)
        {
            lock (_lock)
            {
                return _measurements
                    .Where(m => string.Equals(m.Name, counterName, StringComparison.Ordinal)
                        && tags.All(t => m.Tags.TryGetValue(t.Key, out var v) && string.Equals(v as string, t.Value, StringComparison.Ordinal)))
                    .Sum(m => m.Value);
            }
        }

        public void Dispose() => _listener.Dispose();
    }

    /// <summary>
    /// Minimal capturing logger. <see cref="IsEnabled"/> returns <see langword="true"/> so source-generated
    /// <c>[LoggerMessage]</c> methods actually emit (a FakeItEasy logger returns false and the source-gen skips Log).
    /// </summary>
    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }

    #endregion
}
