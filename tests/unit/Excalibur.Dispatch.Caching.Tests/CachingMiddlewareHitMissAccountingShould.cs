// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Caching.Diagnostics;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// The hit counter must report what was actually served. An entry the read-side guards decline is not
/// served — the handler runs — so counting it as a hit overstates the hit rate by exactly the number of
/// key collisions, which is the population those guards exist to catch.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareHitMissAccountingShould : IDisposable
{
	private const string SharedLogicalKey = "user:42";

	private readonly ServiceProvider _services;
	private readonly HybridCache _cache;
	private readonly IMeterFactory _meterFactory;
	private readonly DispatchJsonSerializer _serializer = new();
	private readonly DefaultCacheKeyBuilder _keyBuilder;
	private readonly Meter _meter;
	private readonly MeterListener _listener;
	private long _hits;
	private long _misses;
	private bool _disposed;

	public CachingMiddlewareHitMissAccountingShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddHybridCache();
		_services = services.BuildServiceProvider();
		_cache = _services.GetRequiredService<HybridCache>();
		_meterFactory = _services.GetRequiredService<IMeterFactory>();
		_keyBuilder = new DefaultCacheKeyBuilder(_serializer);

		// The factory caches per name, so this is the same Meter instance the middleware under test will use.
		// Subscribing by name alone would also collect measurements from other test classes running in
		// parallel on the same meter name; matching the instance keeps the counts to this test's dispatches.
		_meter = _meterFactory.Create(DispatchCachingTelemetryConstants.MeterName);

		// Read the emitted counters rather than any internal state, so the assertion binds what an operator
		// dashboard would actually display.
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
		{
			if (instrument.Name == "dispatch.cache.hits")
			{
				_ = Interlocked.Add(ref _hits, measurement);
			}
			else if (instrument.Name == "dispatch.cache.misses")
			{
				_ = Interlocked.Add(ref _misses, measurement);
			}
		});
		_listener.Start();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_listener.Dispose();

		if (_meterFactory is IDisposable disposableMeterFactory)
		{
			disposableMeterFactory.Dispose();
		}

		_serializer.Dispose();
		_services.Dispose();
	}

	/// <summary>
	/// Two actions share a cache key. The second is declined by the action-attribution guard and its handler
	/// runs, so the dispatch is a miss.
	/// </summary>
	[Fact]
	public async Task CountAGuardRejectedEntryAsAMissNotAHit()
	{
		var middleware = CreateMiddleware();

		DispatchRequestDelegate profileHandler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success(new ProfileDto("Ada")));

		_ = await middleware.InvokeAsync(new ProfileQuery(), NewContext(), profileHandler, CancellationToken.None)
			.ConfigureAwait(true);

		Reset();

		var permissionsRan = false;
		DispatchRequestDelegate permissionsHandler = (_, _, _) =>
		{
			permissionsRan = true;
			return new ValueTask<IMessageResult>(MessageResult.Success(new PermissionsDto("admin")));
		};

		_ = await middleware
			.InvokeAsync(new PermissionsQuery(), NewContext(), permissionsHandler, CancellationToken.None)
			.ConfigureAwait(true);

		_listener.RecordObservableInstruments();

		permissionsRan.ShouldBeTrue("the guard declined the stored entry, so the handler must have run");
		Interlocked.Read(ref _hits).ShouldBe(0, "the handler ran, so nothing was served from cache");
		Interlocked.Read(ref _misses).ShouldBe(1);
	}

	/// <summary>
	/// Liveness arm: a genuine hit must still be counted as one. A counter that reported every dispatch as a
	/// miss would satisfy the arm above while making the metric useless.
	/// </summary>
	[Fact]
	public async Task StillCountARealCacheHitAsAHit()
	{
		var middleware = CreateMiddleware();

		var executions = 0;
		DispatchRequestDelegate handler = (_, _, _) =>
		{
			executions++;
			return new ValueTask<IMessageResult>(MessageResult.Success(new ProfileDto("Ada")));
		};

		_ = await middleware.InvokeAsync(new ProfileQuery(), NewContext(), handler, CancellationToken.None)
			.ConfigureAwait(true);

		Reset();

		_ = await middleware.InvokeAsync(new ProfileQuery(), NewContext(), handler, CancellationToken.None)
			.ConfigureAwait(true);

		_listener.RecordObservableInstruments();

		executions.ShouldBe(1, "the second dispatch must be served from cache");
		Interlocked.Read(ref _hits).ShouldBe(1);
		Interlocked.Read(ref _misses).ShouldBe(0);
	}

	/// <summary>
	/// The first dispatch of a key executes the handler, so it is a miss.
	/// </summary>
	[Fact]
	public async Task CountAFirstDispatchAsAMiss()
	{
		var middleware = CreateMiddleware();

		DispatchRequestDelegate handler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success(new ProfileDto("Ada")));

		_ = await middleware.InvokeAsync(new ProfileQuery(), NewContext(), handler, CancellationToken.None)
			.ConfigureAwait(true);

		_listener.RecordObservableInstruments();

		Interlocked.Read(ref _hits).ShouldBe(0);
		Interlocked.Read(ref _misses).ShouldBe(1);
	}

	private void Reset()
	{
		_ = Interlocked.Exchange(ref _hits, 0);
		_ = Interlocked.Exchange(ref _misses, 0);
	}

	private CachingMiddleware CreateMiddleware()
		=> new(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Create(new CacheOptions { Enabled = true }),
			NullLogger<CachingMiddleware>.Instance);

	private static IMessageContext NewContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	private sealed class ProfileQuery : ICacheable<ProfileDto>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class PermissionsQuery : ICacheable<PermissionsDto>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}
}
