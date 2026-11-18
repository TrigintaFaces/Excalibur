// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Regression")]
public sealed class SagaDeduplicationDiagnosticsShould
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void KeepObserversIndependentWithoutLeakingIdentityLabels(bool throwListener, bool throwLogger)
    {
        var flow = new AsyncLocal<bool> { Value = true };
        var measurements = 0;
        var logger = new DiagnosticLogger(throwLogger);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Name == "excalibur.saga.dedup_evictions" && instrument.Meter.Name == "Excalibur.Saga")
            { owner.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            if (!flow.Value) { return; }
            value.ShouldBe(1);
            tags.Length.ShouldBe(1);
            tags[0].Key.ShouldBe("mode");
            tags[0].Value.ShouldBe("atomic");
            measurements++;
            if (throwListener) { throw new IOException("controlled observer failure"); }
        });
        listener.Start();
        SagaDeduplicationDiagnostics.Report(logger, "atomic");
        flow.Value = false;

        measurements.ShouldBe(1);
        logger.Attempts.ShouldBe(1);
    }

    [Fact]
    public void DetectOneActualEvictionWithoutChangingFifoOrDuplicateSemantics()
    {
        var state = new ProbeState();
        for (var index = 0; index < 1000; index++)
        { SagaDeduplicationDiagnostics.MarkProcessed(state, "old-" + index).ShouldBeFalse(); }
        SagaDeduplicationDiagnostics.MarkProcessed(state, "old-0").ShouldBeFalse();
        SagaDeduplicationDiagnostics.MarkProcessed(state, "new").ShouldBeTrue();
        state.ProcessedEventIds.Count.ShouldBe(1000);
        state.HasProcessedEvent("old-0").ShouldBeFalse();
        state.HasProcessedEvent("old-1").ShouldBeTrue();
        state.HasProcessedEvent("new").ShouldBeTrue();
    }

    private sealed class ProbeState : SagaState;
    private sealed class DiagnosticLogger(bool fail) : ILogger
    {
        internal int Attempts { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name != "SagaDeduplicationEntryEvicted") { return; }
            Attempts++;
            if (fail) { throw new IOException("controlled logger failure"); }
        }
    }
}
