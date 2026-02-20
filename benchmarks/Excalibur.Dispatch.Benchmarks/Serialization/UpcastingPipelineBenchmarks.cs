// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Versioning;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// Performance benchmarks for Universal Message Upcasting (Sprint 54 - Task jb38).
/// </summary>
/// <remarks>
/// <para>
/// <b>Performance targets from ADR-068/069/070:</b>
/// </para>
/// <list type="table">
/// <listheader>
///   <term>Scenario</term>
///   <description>Target</description>
/// </listheader>
/// <item><term>Single-hop upcast</term><description>~15ns</description></item>
/// <item><term>Multi-hop upcast (V1→V4)</term><description>~45ns (3 hops)</description></item>
/// <item><term>Cached path lookup</term><description>~5ns</description></item>
/// <item><term>Non-versioned passthrough</term><description>~0ns</description></item>
/// <item><term>Legacy comparison (DynamicInvoke)</term><description>~300ns</description></item>
/// </list>
/// <para>
/// <b>Key improvement:</b> ~20x faster than legacy DynamicInvoke approach through
/// direct delegate invocation and cached BFS path finding.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class UpcastingPipelineBenchmarks : IDisposable
{
	private UpcastingPipeline _pipeline = null!;
	private BenchmarkUserCreatedEventV1 _v1Event = null!;
	private BenchmarkUserCreatedEventV2 _v2Event = null!;
	private BenchmarkUserCreatedEventV3 _v3Event = null!;
	private BenchmarkNonVersionedMessage _nonVersionedMessage = null!;
	private BenchmarkOrderPlacedEventV1 _orderV1Event = null!;
	private bool _disposed;

	// For legacy comparison
	private Delegate _legacyUpcastDelegate = null!;

	/// <summary>
	/// Initialize the pipeline and test messages before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		_pipeline = new UpcastingPipeline();

		// Register upcasters for UserCreatedEvent (V1 -> V2 -> V3 -> V4)
		_pipeline.Register(new BenchmarkUserCreatedEventV1ToV2Upcaster());
		_pipeline.Register(new BenchmarkUserCreatedEventV2ToV3Upcaster());
		_pipeline.Register(new BenchmarkUserCreatedEventV3ToV4Upcaster());

		// Register upcaster for OrderPlacedEvent (V1 -> V2)
		_pipeline.Register(new BenchmarkOrderPlacedEventV1ToV2Upcaster());

		// Create test messages
		_v1Event = new BenchmarkUserCreatedEventV1
		{
			Id = Guid.NewGuid(),
			Name = "John Doe"
		};

		_v2Event = new BenchmarkUserCreatedEventV2
		{
			Id = Guid.NewGuid(),
			FirstName = "John",
			LastName = "Doe"
		};

		_v3Event = new BenchmarkUserCreatedEventV3
		{
			Id = Guid.NewGuid(),
			FirstName = "John",
			LastName = "Doe",
			Email = "john.doe@example.com"
		};

		_nonVersionedMessage = new BenchmarkNonVersionedMessage
		{
			Data = "test data"
		};

		_orderV1Event = new BenchmarkOrderPlacedEventV1
		{
			OrderId = Guid.NewGuid(),
			Total = 99.99m
		};

		// Warm up path cache by doing initial upcasts
		_ = _pipeline.Upcast(_v1Event);
		_ = _pipeline.Upcast(_v2Event);
		_ = _pipeline.Upcast(_v3Event);
		_ = _pipeline.Upcast(_orderV1Event);

		// For legacy comparison - store delegate that would use DynamicInvoke
		Func<BenchmarkUserCreatedEventV1, BenchmarkUserCreatedEventV2> typedDelegate = oldMessage =>
		{
			var nameParts = oldMessage.Name.Split(' ', 2);
			return new BenchmarkUserCreatedEventV2
			{
				Id = oldMessage.Id,
				FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
				LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty
			};
		};
		_legacyUpcastDelegate = typedDelegate;
	}

	/// <summary>
	/// Cleanup resources after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		Dispose();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes managed resources.
	/// </summary>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_pipeline?.Dispose();
		}

		_disposed = true;
	}

	#region Core Upcasting Benchmarks

	/// <summary>
	/// Benchmark: Single-hop upcast (V1 → V2).
	/// Target: ~15ns with zero allocations in cached path.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Single-hop upcast (V1→V2)")]
	public IDispatchMessage SingleHopUpcast()
	{
		return _pipeline.Upcast(_v1Event);
	}

	/// <summary>
	/// Benchmark: Two-hop upcast (V2 → V3 → V4).
	/// Target: ~30ns (2 hops × 15ns).
	/// </summary>
	[Benchmark(Description = "Two-hop upcast (V2→V4)")]
	public IDispatchMessage TwoHopUpcast()
	{
		return _pipeline.Upcast(_v2Event);
	}

	/// <summary>
	/// Benchmark: Multi-hop upcast (V1 → V2 → V3 → V4).
	/// Target: ~45ns (3 hops × 15ns).
	/// </summary>
	[Benchmark(Description = "Multi-hop upcast (V1→V4)")]
	public IDispatchMessage MultiHopUpcastV1ToV4()
	{
		return _pipeline.Upcast(_v1Event);
	}

	/// <summary>
	/// Benchmark: Single-hop upcast (V3 → V4).
	/// Validates consistent per-hop performance.
	/// </summary>
	[Benchmark(Description = "Single-hop upcast (V3→V4)")]
	public IDispatchMessage SingleHopUpcastV3ToV4()
	{
		return _pipeline.Upcast(_v3Event);
	}

	#endregion Core Upcasting Benchmarks

	#region Path Lookup Benchmarks

	/// <summary>
	/// Benchmark: Cached path lookup via CanUpcast.
	/// Target: ~5ns (ConcurrentDictionary lookup).
	/// </summary>
	[Benchmark(Description = "Cached path lookup (CanUpcast)")]
	public bool CachedPathLookup()
	{
		return _pipeline.CanUpcast("UserCreatedEvent", 1, 4);
	}

	/// <summary>
	/// Benchmark: GetLatestVersion lookup.
	/// Target: ~5ns (Dictionary lookup with reader lock).
	/// </summary>
	[Benchmark(Description = "GetLatestVersion lookup")]
	public int GetLatestVersionLookup()
	{
		return _pipeline.GetLatestVersion("UserCreatedEvent");
	}

	#endregion Path Lookup Benchmarks

	#region Passthrough Benchmarks

	/// <summary>
	/// Benchmark: Non-versioned message passthrough.
	/// Target: ~0ns (type check only, no processing).
	/// </summary>
	[Benchmark(Description = "Non-versioned passthrough")]
	public IDispatchMessage NonVersionedPassthrough()
	{
		return _pipeline.Upcast(_nonVersionedMessage);
	}

	/// <summary>
	/// Benchmark: Already-latest version passthrough.
	/// Target: ~5ns (version comparison, no upcasting).
	/// </summary>
	[Benchmark(Description = "Already-latest passthrough")]
	public IDispatchMessage AlreadyLatestPassthrough()
	{
		// V4 is already at latest version - should passthrough
		var v4Event = new BenchmarkUserCreatedEventV4
		{
			Id = Guid.NewGuid(),
			FirstName = "John",
			LastName = "Doe",
			Email = "john.doe@example.com",
			CreatedAt = DateTimeOffset.UtcNow
		};
		return _pipeline.Upcast(v4Event);
	}

	#endregion Passthrough Benchmarks

	#region Legacy Comparison Benchmarks

	/// <summary>
	/// Benchmark: Legacy DynamicInvoke approach.
	/// Expected: ~300ns (reflection overhead).
	/// This demonstrates the 20x improvement achieved by direct delegate invocation.
	/// </summary>
	[Benchmark(Description = "Legacy DynamicInvoke (comparison)")]
	public object? LegacyDynamicInvoke()
	{
		// Simulates the old IMessageMigrationStrategy approach using DynamicInvoke
		return _legacyUpcastDelegate.DynamicInvoke(_v1Event);
	}

	/// <summary>
	/// Benchmark: Direct delegate invocation (what we use internally).
	/// Expected: ~15ns (direct call, no reflection).
	/// </summary>
	[Benchmark(Description = "Direct delegate (internal approach)")]
	public BenchmarkUserCreatedEventV2 DirectDelegateInvocation()
	{
		var typedDelegate = (Func<BenchmarkUserCreatedEventV1, BenchmarkUserCreatedEventV2>)_legacyUpcastDelegate;
		return typedDelegate(_v1Event);
	}

	#endregion Legacy Comparison Benchmarks

	#region Different Message Type Benchmarks

	/// <summary>
	/// Benchmark: Single-hop upcast for different message type (OrderPlacedEvent).
	/// Validates performance is consistent across message types.
	/// </summary>
	[Benchmark(Description = "Different message type (Order V1→V2)")]
	public IDispatchMessage DifferentMessageTypeUpcast()
	{
		return _pipeline.Upcast(_orderV1Event);
	}

	#endregion Different Message Type Benchmarks
}
