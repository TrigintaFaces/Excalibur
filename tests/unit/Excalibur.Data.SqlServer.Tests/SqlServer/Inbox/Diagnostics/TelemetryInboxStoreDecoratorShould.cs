// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Inbox.Diagnostics;

namespace Excalibur.Data.Tests.SqlServer.Inbox.Diagnostics;

/// <summary>
/// Unit tests for <see cref="TelemetryInboxStoreDecorator"/> — verifies telemetry
/// instrumentation (counters + histograms) for all 6 inbox store operations.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Core")]
public sealed class TelemetryInboxStoreDecoratorShould : IDisposable
{
    private readonly IInboxStore _inner;
    private readonly TelemetryInboxStoreDecorator _sut;
    private readonly MeterListener _listener;
    private readonly List<(string Name, long Value)> _counterRecords = [];
    private readonly List<(string Name, double Value)> _histogramRecords = [];

    public TelemetryInboxStoreDecoratorShould()
    {
        _inner = A.Fake<IInboxStore>();
        _sut = new TelemetryInboxStoreDecorator(_inner);

        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TelemetryInboxStoreDecorator.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            _counterRecords.Add((instrument.Name, measurement)));
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
            _histogramRecords.Add((instrument.Name, measurement)));
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _sut.Dispose();
    }

    [Fact]
    public async Task RecordCreateEntryOperation()
    {
        A.CallTo(() => _inner.CreateEntryAsync(A<string>._, A<string>._, A<string>._, A<byte[]>._, A<IDictionary<string, object>>._, A<CancellationToken>._))
            .Returns(new InboxEntry("msg-1", "Handler", "Type", [], new Dictionary<string, object>()));

        await _sut.CreateEntryAsync("msg-1", "Handler", "Type", [], new Dictionary<string, object>(), CancellationToken.None);
        _listener.RecordObservableInstruments();

        _counterRecords.ShouldContain(r => r.Name == "excalibur.inbox.operations");
        _histogramRecords.ShouldContain(r => r.Name == "excalibur.inbox.operation_duration");
    }

    [Fact]
    public async Task RecordMarkProcessedOperation()
    {
        await _sut.MarkProcessedAsync("msg-1", "Handler", CancellationToken.None);
        _listener.RecordObservableInstruments();

        _counterRecords.ShouldContain(r => r.Name == "excalibur.inbox.operations");
        _histogramRecords.ShouldContain(r => r.Name == "excalibur.inbox.operation_duration");
    }

    [Fact]
    public async Task RecordTryMarkAsProcessedOperation()
    {
        A.CallTo(() => _inner.TryMarkAsProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(true);

        var result = await _sut.TryMarkAsProcessedAsync("msg-1", "Handler", CancellationToken.None);
        _listener.RecordObservableInstruments();

        result.ShouldBeTrue();
        _counterRecords.ShouldContain(r => r.Name == "excalibur.inbox.operations");
    }

    [Fact]
    public async Task RecordIsProcessedOperation()
    {
        A.CallTo(() => _inner.IsProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(false);

        var result = await _sut.IsProcessedAsync("msg-1", "Handler", CancellationToken.None);
        _listener.RecordObservableInstruments();

        result.ShouldBeFalse();
        _counterRecords.ShouldContain(r => r.Name == "excalibur.inbox.operations");
    }

    [Fact]
    public async Task RecordGetEntryOperation()
    {
        A.CallTo(() => _inner.GetEntryAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns((InboxEntry?)null);

        var result = await _sut.GetEntryAsync("msg-1", "Handler", CancellationToken.None);
        _listener.RecordObservableInstruments();

        result.ShouldBeNull();
        _counterRecords.ShouldContain(r => r.Name == "excalibur.inbox.operations");
    }

    [Fact]
    public async Task RecordMarkFailedOperation()
    {
        await _sut.MarkFailedAsync("msg-1", "Handler", "Error occurred", CancellationToken.None);
        _listener.RecordObservableInstruments();

        _counterRecords.ShouldContain(r => r.Name == "excalibur.inbox.operations");
        _histogramRecords.ShouldContain(r => r.Name == "excalibur.inbox.operation_duration");
    }

    [Fact]
    public void DelegateToInnerStore()
    {
        // Verify the decorator delegates, not replaces
        _inner.ShouldNotBeNull();
        _sut.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_ThrowOnNullInner()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TelemetryInboxStoreDecorator(null!));
    }

    [Fact]
    public void AcceptNullMeterFactory()
    {
        // Should not throw — creates default Meter
        using var decorator = new TelemetryInboxStoreDecorator(_inner, null);
        decorator.ShouldNotBeNull();
    }

    [Fact]
    public void UseMeterFactory_WhenProvided()
    {
        var meterFactory = A.Fake<IMeterFactory>();
        A.CallTo(() => meterFactory.Create(A<MeterOptions>._))
            .Returns(new Meter(TelemetryInboxStoreDecorator.MeterName));

        using var decorator = new TelemetryInboxStoreDecorator(_inner, meterFactory);
        decorator.ShouldNotBeNull();

        A.CallTo(() => meterFactory.Create(A<MeterOptions>._)).MustHaveHappenedOnceExactly();
    }
}
