// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.Diagnostics;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Saga.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="TelemetrySagaStoreDecorator"/> — verifies telemetry
/// instrumentation (counters + histograms) for Load and Save operations,
/// including saga_type tag.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TelemetrySagaStoreDecoratorShould : IDisposable
{
    private readonly ISagaStore _inner;
    private readonly TelemetrySagaStoreDecorator _sut;
    private readonly MeterListener _listener;
    private readonly List<(string Name, long Value)> _counterRecords = [];
    private readonly List<(string Name, double Value)> _histogramRecords = [];

    public TelemetrySagaStoreDecoratorShould()
    {
        _inner = A.Fake<ISagaStore>();
        _sut = new TelemetrySagaStoreDecorator(_inner);

        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TelemetrySagaStoreDecorator.MeterName)
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

    private sealed class TestSagaState : SagaState
    {
    }

    [Fact]
    public async Task RecordLoadOperation()
    {
        A.CallTo(() => _inner.LoadAsync<TestSagaState>(A<Guid>._, A<CancellationToken>._))
            .Returns((TestSagaState?)null);

        await _sut.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None);
        _listener.RecordObservableInstruments();

        _counterRecords.ShouldContain(r => r.Name == "excalibur.saga.operations");
        _histogramRecords.ShouldContain(r => r.Name == "excalibur.saga.operation_duration");
    }

    [Fact]
    public async Task RecordSaveOperation()
    {
        var state = new TestSagaState();

        await _sut.SaveAsync(state, CancellationToken.None);
        _listener.RecordObservableInstruments();

        _counterRecords.ShouldContain(r => r.Name == "excalibur.saga.operations");
        _histogramRecords.ShouldContain(r => r.Name == "excalibur.saga.operation_duration");
    }

    [Fact]
    public async Task DelegateLoadToInnerStore()
    {
        var sagaId = Guid.NewGuid();

        await _sut.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

        A.CallTo(() => _inner.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DelegateSaveToInnerStore()
    {
        var state = new TestSagaState();

        await _sut.SaveAsync(state, CancellationToken.None);

        A.CallTo(() => _inner.SaveAsync(state, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReturnInnerStoreResult_ForLoad()
    {
        var expected = new TestSagaState();
        A.CallTo(() => _inner.LoadAsync<TestSagaState>(A<Guid>._, A<CancellationToken>._))
            .Returns(expected);

        var result = await _sut.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeSameAs(expected);
    }

    [Fact]
    public void Constructor_ThrowOnNullInner()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TelemetrySagaStoreDecorator(null!));
    }

    [Fact]
    public void AcceptNullMeterFactory()
    {
        using var decorator = new TelemetrySagaStoreDecorator(_inner, null);
        decorator.ShouldNotBeNull();
    }

    [Fact]
    public void UseMeterFactory_WhenProvided()
    {
        var meterFactory = A.Fake<IMeterFactory>();
        A.CallTo(() => meterFactory.Create(A<MeterOptions>._))
            .Returns(new Meter(TelemetrySagaStoreDecorator.MeterName));

        using var decorator = new TelemetrySagaStoreDecorator(_inner, meterFactory);
        decorator.ShouldNotBeNull();

        A.CallTo(() => meterFactory.Create(A<MeterOptions>._)).MustHaveHappenedOnceExactly();
    }
}
