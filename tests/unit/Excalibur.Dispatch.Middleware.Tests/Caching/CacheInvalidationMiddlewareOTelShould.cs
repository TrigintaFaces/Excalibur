// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- TestMeterFactory is test-scoped

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Caching.Diagnostics;

using FakeItEasy;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

using Tests.Shared;
using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests verifying OTel metric emission from <see cref="CacheInvalidationMiddleware"/>.
/// Uses meter-instance tracking to isolate from concurrent test execution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheInvalidationMiddlewareOTelShould : UnitTestBase
{
	private readonly ConcurrentDictionary<Meter, byte> _ownedMeters = new();
	private readonly IMeterFactory _meterFactory;
	private readonly MeterListener _listener = new();
	private long _invalidationCount;
	private long _tagsInvalidatedCount;
	private long _keysInvalidatedCount;

	public CacheInvalidationMiddlewareOTelShould()
	{
		_meterFactory = new TrackingMeterFactory(new TestMeterFactory(), _ownedMeters);

		_listener.InstrumentPublished = (instrument, listener) =>
		{
			// Only listen to meters created by THIS test instance, not concurrent tests
			if (_ownedMeters.ContainsKey(instrument.Meter))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			switch (instrument.Name)
			{
				case "dispatch.cache.invalidations":
					Interlocked.Add(ref _invalidationCount, measurement);
					break;
				case "dispatch.cache.tags_invalidated":
					Interlocked.Add(ref _tagsInvalidatedCount, measurement);
					break;
				case "dispatch.cache.keys_invalidated":
					Interlocked.Add(ref _keysInvalidatedCount, measurement);
					break;
			}
		});

		_listener.Start();
	}

	[Fact]
	public async Task RecordInvalidationCounter_WhenCacheInvalidatorTriggered()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
		});

		var hybridCache = A.Fake<HybridCache>();
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);

		var message = A.Fake<IDispatchMessage>(o => o.Implements<ICacheInvalidator>());
		var invalidator = (ICacheInvalidator)message;
		A.CallTo(() => invalidator.GetCacheTagsToInvalidate()).Returns((string[])["tag1", "tag2"]);
		A.CallTo(() => invalidator.GetCacheKeysToInvalidate()).Returns((string[])["key1"]);

		var context = A.Fake<IMessageContext>();
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(true);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(result);

		// Act
		_ = await middleware.InvokeAsync(message, context, Next, CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert
		_invalidationCount.ShouldBeGreaterThanOrEqualTo(1);
		_tagsInvalidatedCount.ShouldBeGreaterThanOrEqualTo(2);
		_keysInvalidatedCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task NotRecordMetrics_WhenCachingDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions { Enabled = false });
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var result = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(result);

		// Capture baseline
		_listener.RecordObservableInstruments();
		var baselineInvalidation = Interlocked.Read(ref _invalidationCount);
		var baselineTags = Interlocked.Read(ref _tagsInvalidatedCount);
		var baselineKeys = Interlocked.Read(ref _keysInvalidatedCount);

		// Act
		_ = await middleware.InvokeAsync(message, context, Next, CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert — no NEW metrics should be recorded since caching is disabled
		Interlocked.Read(ref _invalidationCount).ShouldBe(baselineInvalidation);
		Interlocked.Read(ref _tagsInvalidatedCount).ShouldBe(baselineTags);
		Interlocked.Read(ref _keysInvalidatedCount).ShouldBe(baselineKeys);
	}

	[Fact]
	public async Task NotRecordMetrics_WhenNoInvalidationNeeded()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var result = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(result);

		// Capture baseline
		_listener.RecordObservableInstruments();
		var baselineInvalidation = Interlocked.Read(ref _invalidationCount);

		// Act
		_ = await middleware.InvokeAsync(message, context, Next, CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert — no NEW invalidation happened
		Interlocked.Read(ref _invalidationCount).ShouldBe(baselineInvalidation);
	}

	[Fact]
	public async Task RecordTagCount_IncludesDefaultTags()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			DefaultTags = ["default-tag"],
		});
		var hybridCache = A.Fake<HybridCache>();
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);

		var message = A.Fake<IDispatchMessage>(o => o.Implements<ICacheInvalidator>());
		var invalidator = (ICacheInvalidator)message;
		A.CallTo(() => invalidator.GetCacheTagsToInvalidate()).Returns((string[])["tag1"]);
		A.CallTo(() => invalidator.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var result = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(result);

		// Act
		_ = await middleware.InvokeAsync(message, context, Next, CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert — 2 tags: "tag1" + "default-tag"
		_tagsInvalidatedCount.ShouldBeGreaterThanOrEqualTo(2);
	}

	/// <summary>
	/// Factory wrapper that tracks which <see cref="Meter"/> instances belong to this test,
	/// preventing cross-test metric interference when <see cref="MeterListener"/> is process-global.
	/// </summary>
	private sealed class TrackingMeterFactory(TestMeterFactory inner, ConcurrentDictionary<Meter, byte> ownedMeters) : IMeterFactory
	{
		public Meter Create(MeterOptions options)
		{
			var meter = inner.Create(options);
			ownedMeters.TryAdd(meter, 0);
			return meter;
		}

		public void Dispose() => inner.Dispose();
	}
}
