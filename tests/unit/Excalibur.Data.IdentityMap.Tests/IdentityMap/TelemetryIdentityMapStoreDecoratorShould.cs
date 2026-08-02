// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using System.Diagnostics.Metrics;

using Excalibur.Data.IdentityMap.Diagnostics;

namespace Excalibur.Data.IdentityMap.Tests.IdentityMap;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.IdentityMap")]
public sealed class TelemetryIdentityMapStoreDecoratorShould : IDisposable
{
	private readonly IIdentityMapStore _inner;
	private readonly TelemetryIdentityMapStoreDecorator _sut;
	private readonly MeterListener _listener;
	// ConcurrentBag, not List. A MeterListener callback runs on WHATEVER THREAD RECORDS THE MEASUREMENT,
	// so these are appended concurrently and List<T> corrupts under it — its resize does a
	// Count-then-Array.Copy that another thread invalidates mid-flight, surfacing as
	// "Destination array was not long enough" from deep inside List<T>.AddWithResize.
	//
	// The corruption did not appear in THIS class's tests. InstrumentPublished filters by meter NAME, which
	// every instance of the decorator shares, so this listener also receives measurements from other test
	// classes in the assembly. A sibling class driving 100 concurrent operations
	// (InMemoryIdentityMapStoreShould.HandleConcurrentBindAndResolve) was the thread that corrupted this
	// list, and that sibling is where the failure was reported — which is why it reproduced only in a full
	// shard run and passed in isolation.
	//
	// Every assertion below is ShouldContain(predicate), so unordered storage and foreign measurements are
	// both harmless here; thread-safety is the property that was actually missing.
	private readonly ConcurrentBag<(string Name, long Value)> _counterRecords = [];
	private readonly ConcurrentBag<(string Name, double Value)> _histogramRecords = [];

	public TelemetryIdentityMapStoreDecoratorShould()
	{
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();
		_inner = services.BuildServiceProvider().GetRequiredService<IIdentityMapStore>();

		_sut = new TelemetryIdentityMapStoreDecorator(_inner);

		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == TelemetryIdentityMapStoreDecorator.MeterName)
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
	public async Task RecordResolveOperation()
	{
		await _sut.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		_listener.RecordObservableInstruments();

		_counterRecords.ShouldContain(r => r.Name == "excalibur.identitymap.operations");
		_histogramRecords.ShouldContain(r => r.Name == "excalibur.identitymap.operation_duration");
	}

	[Fact]
	public async Task RecordBindOperation()
	{
		await _sut.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);
		_listener.RecordObservableInstruments();

		_counterRecords.ShouldContain(r => r.Name == "excalibur.identitymap.operations");
	}

	[Fact]
	public async Task RecordTryBindOperation()
	{
		await _sut.TryBindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);
		_listener.RecordObservableInstruments();

		_counterRecords.ShouldContain(r => r.Name == "excalibur.identitymap.operations");
	}

	[Fact]
	public async Task RecordUnbindOperation()
	{
		await _sut.UnbindAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		_listener.RecordObservableInstruments();

		_counterRecords.ShouldContain(r => r.Name == "excalibur.identitymap.operations");
	}

	[Fact]
	public async Task RecordResolveBatchOperation()
	{
		await _sut.ResolveBatchAsync("SAP", ["EXT-001"], "Order", CancellationToken.None);
		_listener.RecordObservableInstruments();

		_counterRecords.ShouldContain(r => r.Name == "excalibur.identitymap.operations");
	}

	[Fact]
	public void ThrowOnNullInner()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryIdentityMapStoreDecorator(null!));
	}

	[Fact]
	public void ImplementIDisposable()
	{
		using var decorator = new TelemetryIdentityMapStoreDecorator(_inner);
		// Should not throw
	}

	[Fact]
	public void DoubleDispose_NotThrow()
	{
		var decorator = new TelemetryIdentityMapStoreDecorator(_inner);
		decorator.Dispose();
		decorator.Dispose(); // Should not throw
	}
}
