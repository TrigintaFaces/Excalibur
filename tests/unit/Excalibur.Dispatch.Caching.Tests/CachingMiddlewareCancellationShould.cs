// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Regression tests for CachingMiddleware cancellation filter (Sprint 670, T.3).
/// Verifies that real caller cancellation propagates as OperationCanceledException
/// while cache timeouts are caught and handled gracefully.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareCancellationShould : IDisposable
{
	private readonly IMeterFactory _meterFactory = new TestMeterFactory();
	private readonly HybridCache _fakeCache = A.Fake<HybridCache>();
	private readonly ICacheKeyBuilder _fakeKeyBuilder = A.Fake<ICacheKeyBuilder>();
	private readonly IServiceProvider _fakeServices = A.Fake<IServiceProvider>();

	public void Dispose()
	{
		if (_meterFactory is IDisposable d) d.Dispose();
	}

	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters) meter.Dispose();
		}
	}

	[Fact]
	public async Task PropagateOperationCanceledException_WhenCallerCancels()
	{
		// Arrange
		var logger = A.Fake<ILogger<CachingMiddleware>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		var middleware = new CachingMiddleware(
			_meterFactory, _fakeCache, _fakeKeyBuilder, _fakeServices,
			MsOptions.Create(options), logger);

		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Create an already-cancelled token to simulate caller cancellation
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		DispatchRequestDelegate nextDelegate = (_, _, ct) =>
		{
			ct.ThrowIfCancellationRequested();
			return ValueTask.FromResult(A.Fake<IMessageResult>());
		};

		// Act & Assert -- real cancellation MUST propagate, not be swallowed
#pragma warning disable CA2012 // ValueTask used correctly in async lambda
		await Should.ThrowAsync<OperationCanceledException>(
			async () => await middleware.InvokeAsync(message, context, nextDelegate, cts.Token));
#pragma warning restore CA2012
	}

	[Fact]
	public async Task CatchCacheTimeout_WhenCallerHasNotCancelled()
	{
		// Arrange
		var logger = A.Fake<ILogger<CachingMiddleware>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		var memDistCache = new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(
			MsOptions.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));

		A.CallTo(() => _fakeServices.GetService(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache)))
			.Returns(memDistCache);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		var middleware = new CachingMiddleware(
			_meterFactory, _fakeCache, _fakeKeyBuilder, _fakeServices,
			MsOptions.Create(options), logger);

		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Simulate cache timeout by making HybridCache throw OCE with a non-cancelled caller token
		A.CallTo(() => _fakeCache.GetOrCreateAsync(
				A<string>._, A<object>._, A<Func<object, CancellationToken, ValueTask<IMessageResult>>>._,
				A<HybridCacheEntryOptions>._, A<IEnumerable<string>>._, A<CancellationToken>._))
			.ThrowsAsync(new OperationCanceledException("Cache timeout"));

		// Act -- should NOT throw; cache timeout is caught and next delegate is called
		var result = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}
}
