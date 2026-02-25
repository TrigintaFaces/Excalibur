// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
///     Tests for the <see cref="Utf8JsonWriterPool" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Utf8JsonWriterPoolShould : IDisposable
{
	private readonly Utf8JsonWriterPool _sut = new();

	[Fact]
	public void CreateWithDefaultParameters()
	{
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithCustomParameters()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 128,
			threadLocalCacheSize: 2,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		pool.ShouldNotBeNull();
	}

	[Fact]
	public void RentWriterSuccessfully()
	{
		var bufferWriter = new ArrayBufferWriter<byte>();
		var writer = _sut.Rent(bufferWriter);
		writer.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnMultipleWriters()
	{
		for (var i = 0; i < 5; i++)
		{
			var bufferWriter = new ArrayBufferWriter<byte>();
			var writer = _sut.Rent(bufferWriter);
			writer.ShouldNotBeNull();
		}
	}

	[Fact]
	public void ImplementIUtf8JsonWriterPool()
	{
		_sut.ShouldBeAssignableTo<IUtf8JsonWriterPool>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_sut.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void DisposeWithoutErrors()
	{
		Should.NotThrow(() => _sut.Dispose());
	}

	[Fact]
	public void DisposeMultipleTimesWithoutErrors()
	{
		_sut.Dispose();
		Should.NotThrow(() => _sut.Dispose());
	}

	[Fact]
	public void GetPoolStatistics()
	{
		var stats = _sut.GetStatistics();
		stats.ShouldNotBeNull();
	}

	[Fact]
	public void TrackTotalRented()
	{
		var bufferWriter = new ArrayBufferWriter<byte>();
		_ = _sut.Rent(bufferWriter);

		_sut.TotalRented.ShouldBe(1);
	}

	[Fact]
	public void ThrowForInvalidMaxPoolSize()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new Utf8JsonWriterPool(maxPoolSize: 0));
		Should.Throw<ArgumentOutOfRangeException>(() => new Utf8JsonWriterPool(maxPoolSize: 8193));
	}

	[Fact]
	public void ThrowForInvalidThreadLocalCacheSize()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new Utf8JsonWriterPool(threadLocalCacheSize: -1));
		Should.Throw<ArgumentOutOfRangeException>(() => new Utf8JsonWriterPool(threadLocalCacheSize: 33));
	}

	[Fact]
	public void ThrowForNullBufferWriterOnRent()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Rent(null!));
	}

	[Fact]
	public void ThrowForNullWriterOnReturn()
	{
		Should.Throw<ArgumentNullException>(() => _sut.ReturnToPool(null!));
	}

	[Fact]
	public void TrackTotalReturnedAfterReturn()
	{
		var writer = _sut.Rent(new ArrayBufferWriter<byte>());

		_sut.ReturnToPool(writer);

		_sut.TotalReturned.ShouldBe(1);
	}

	[Fact]
	public void ReuseWriterFromThreadLocalCacheWhenOptionsMatch()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 1,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		var writer = pool.Rent(new ArrayBufferWriter<byte>());
		pool.ReturnToPool(writer);
		var reused = pool.Rent(new ArrayBufferWriter<byte>());

		var stats = pool.GetStatistics();
		stats.ThreadLocalHits.ShouldBeGreaterThan(0);

		pool.ReturnToPool(reused);
	}

	[Fact]
	public void RecordThreadLocalMissWhenOptionsDiffer()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 1,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		var initial = pool.Rent(new ArrayBufferWriter<byte>());
		pool.ReturnToPool(initial);

		var nonDefaultOptions = new JsonWriterOptions
		{
			Indented = true,
			SkipValidation = false,
			Encoder = JavaScriptEncoder.Default,
			MaxDepth = 16,
		};

		var second = pool.Rent(new ArrayBufferWriter<byte>(), nonDefaultOptions);
		var stats = pool.GetStatistics();
		stats.ThreadLocalMisses.ShouldBeGreaterThan(0);
		pool.ReturnToPool(second);
	}

	[Fact]
	public void StoreReturnedWritersInGlobalPoolWhenThreadLocalCacheIsDisabled()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 0,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		var writer1 = pool.Rent(new ArrayBufferWriter<byte>());
		var writer2 = pool.Rent(new ArrayBufferWriter<byte>());
		pool.ReturnToPool(writer1);
		pool.ReturnToPool(writer2);

		pool.Count.ShouldBe(2);
	}

	[Fact]
	public void RespectMaxPoolSizeWhenReturningWriters()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 1,
			threadLocalCacheSize: 0,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		var writer1 = pool.Rent(new ArrayBufferWriter<byte>());
		var writer2 = pool.Rent(new ArrayBufferWriter<byte>());
		pool.ReturnToPool(writer1);
		pool.ReturnToPool(writer2);

		pool.Count.ShouldBe(1);
		pool.TotalReturned.ShouldBe(2);
	}

	[Fact]
	public void ClearResetPoolCountAndPeak()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 0,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		pool.PreWarmWithStrategy(3, PreWarmStrategy.Global);
		pool.Count.ShouldBe(3);
		pool.GetStatistics().PeakPoolSize.ShouldBeGreaterThanOrEqualTo(3);

		pool.Clear();

		pool.Count.ShouldBe(0);
		pool.GetStatistics().PeakPoolSize.ShouldBe(0);
	}

	[Fact]
	public void ThrowForInvalidPreWarmCount()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => _sut.PreWarm(0));
	}

	[Fact]
	public void ThrowForInvalidPreWarmStrategyValue()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => _sut.PreWarmWithStrategy(1, (PreWarmStrategy)1234));
	}

	[Fact]
	public void PreWarmThreadLocalSeedThreadLocalCache()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 1,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		pool.PreWarmWithStrategy(1, PreWarmStrategy.ThreadLocal);
		var writer = pool.Rent(new ArrayBufferWriter<byte>());

		pool.GetStatistics().ThreadLocalHits.ShouldBeGreaterThan(0);
		pool.ReturnToPool(writer);
	}

	[Fact]
	public void PreWarmGlobalIncreaseGlobalCount()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 0,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		pool.PreWarmWithStrategy(3, PreWarmStrategy.Global);

		pool.Count.ShouldBe(3);
	}

	[Fact]
	public void PreWarmBalancedDistributeAcrossThreadLocalAndGlobal()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 2,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		pool.PreWarmWithStrategy(4, PreWarmStrategy.Balanced);

		pool.Count.ShouldBe(3);
	}

	[Fact]
	public void ReportCriticalHealthWhenUtilizationIsVeryHigh()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 10,
			threadLocalCacheSize: 0,
			enableAdaptiveSizing: false,
			enableTelemetry: false);
		var rented = new List<Utf8JsonWriter>();

		for (var i = 0; i < 10; i++)
		{
			rented.Add(pool.Rent(new ArrayBufferWriter<byte>()));
		}

		pool.GetHealth().ShouldBe(PoolHealth.Critical);

		foreach (var writer in rented)
		{
			pool.ReturnToPool(writer);
		}
	}

	[Fact]
	public void ReportWarningHealthWhenReturnRateIsModeratelyLow()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 64,
			threadLocalCacheSize: 0,
			enableAdaptiveSizing: false,
			enableTelemetry: false);
		var notReturned = new List<Utf8JsonWriter>();

		for (var i = 0; i < 100; i++)
		{
			var writer = pool.Rent(new ArrayBufferWriter<byte>());
			if (i < 97)
			{
				pool.ReturnToPool(writer);
			}
			else
			{
				notReturned.Add(writer);
			}
		}

		pool.GetHealth().ShouldBe(PoolHealth.Warning);

		foreach (var writer in notReturned)
		{
			pool.ReturnToPool(writer);
		}
	}

	[Fact]
	public void ReportHealthyWhenUtilizationAndReturnRateAreGood()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 0,
			enableAdaptiveSizing: false,
			enableTelemetry: false);

		for (var i = 0; i < 8; i++)
		{
			var writer = pool.Rent(new ArrayBufferWriter<byte>());
			pool.ReturnToPool(writer);
		}

		pool.GetHealth().ShouldBe(PoolHealth.Healthy);
	}

	[Fact]
	public void SwallowErrorsWhenReturningDisposedWriter()
	{
		using var pool = new Utf8JsonWriterPool(
			maxPoolSize: 8,
			threadLocalCacheSize: 0,
			enableAdaptiveSizing: false,
			enableTelemetry: false);
		var writer = pool.Rent(new ArrayBufferWriter<byte>());
		writer.Dispose();

		Should.NotThrow(() => pool.ReturnToPool(writer));
	}

	public void Dispose() => _sut.Dispose();
}
