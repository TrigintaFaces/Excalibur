// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// Performance benchmarks for Universal Message Upcasting (Sprint 54 - Task jb38).
/// </summary>
/// <remarks>
/// <para>
/// <b>There are deliberately no target figures here.</b> This class previously listed per-scenario
/// nanosecond targets carried over from the original design notes. They had no linked source epoch, no
/// matched runtime and no raw evidence, and one of them described a reader lock the pipeline no longer
/// takes. Unverified numbers in a doc comment read as a budget and silently become folklore, so the
/// budget lives where it can actually fail a build -- the recorded means and allocations in
/// <c>benchmarks/baselines/performance-baselines.json</c>, compared by the performance gate.
/// </para>
/// <para>
/// What this class measures is the shape of the work: a passthrough that does nothing, a cached-path
/// lookup, and one, two and three hop traversals. Run it and read what it prints; do not quote a figure
/// from a comment.
/// </para>
/// </remarks>
// Run from this project's directory so BenchmarkDotNet resolves the intended project.
// Copied project files elsewhere in the repository are not a reason to change the job.
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class UpcastingPipelineBenchmarks : IDisposable
{
	private UpcastingPipeline _pipeline = null!;
	private bool _disposed;
	private BenchmarkUserCreatedEventV1 _v1Event = null!;
	private BenchmarkUserCreatedEventV2 _v2Event = null!;
	private BenchmarkUserCreatedEventV3 _v3Event = null!;
	private BenchmarkNonVersionedMessage _nonVersionedMessage = null!;
	private BenchmarkOrderPlacedEventV1 _orderV1Event = null!;

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
	/// Benchmark: Single-hop upcast (V1 â†’ V2).
	/// Despite its historical name, this method traverses V1 through the registered latest version V4 (three hops).
	/// </summary>
	[Benchmark(Baseline = true, Description = "Single-hop upcast (V1->V2)")]
	public IDispatchMessage SingleHopUpcast()
	{
		return _pipeline.Upcast(_v1Event);
	}

	/// <summary>
	/// Benchmark: Two-hop upcast (V2 â†’ V3 â†’ V4).
	/// Measures two resolved hops over a cached path.
	/// </summary>
	[Benchmark(Description = "Two-hop upcast (V2->V4)")]
	public IDispatchMessage TwoHopUpcast()
	{
		return _pipeline.Upcast(_v2Event);
	}

	/// <summary>
	/// Benchmark: Multi-hop upcast (V1 â†’ V2 â†’ V3 â†’ V4).
	/// Measures three resolved hops over a cached path.
	/// </summary>
	[Benchmark(Description = "Multi-hop upcast (V1->V4)")]
	public IDispatchMessage MultiHopUpcastV1ToV4()
	{
		return _pipeline.Upcast(_v1Event);
	}

	/// <summary>
	/// Benchmark: Single-hop upcast (V3 â†’ V4).
	/// Validates consistent per-hop performance.
	/// </summary>
	[Benchmark(Description = "Single-hop upcast (V3->V4)")]
	public IDispatchMessage SingleHopUpcastV3ToV4()
	{
		return _pipeline.Upcast(_v3Event);
	}

	#endregion Core Upcasting Benchmarks

	#region Path Lookup Benchmarks

	/// <summary>
	/// Benchmark: Cached path lookup via CanUpcast.
	/// Measures a cached-path lookup with no traversal.
	/// </summary>
	[Benchmark(Description = "Cached path lookup (CanUpcast)")]
	public bool CachedPathLookup()
	{
		return _pipeline.CanUpcast("UserCreatedEvent", 1, 4);
	}

	/// <summary>
	/// Benchmark: GetLatestVersion lookup.
	/// Measures the latest-version lookup. Takes no lock: one volatile read plus a frozen lookup.
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
	/// Measures the non-versioned passthrough, which is a type check and an immediate return.
	/// </summary>
	[Benchmark(Description = "Non-versioned passthrough")]
	public IDispatchMessage NonVersionedPassthrough()
	{
		return _pipeline.Upcast(_nonVersionedMessage);
	}

	/// <summary>
	/// Benchmark: Already-latest version passthrough.
	/// Measures the already-at-latest passthrough, which resolves no path.
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
	/// Reference point: the same transformation through reflection rather than a typed delegate.
	/// Compare only the work performed by these two fixture methods; no fixed speedup is asserted.
	/// </summary>
	[Benchmark(Description = "Legacy DynamicInvoke (comparison)")]
	public object? LegacyDynamicInvoke()
	{
		// Simulates the old IMessageMigrationStrategy approach using DynamicInvoke
		return _legacyUpcastDelegate.DynamicInvoke(_v1Event);
	}

	/// <summary>
	/// Benchmark: Direct delegate invocation (what we use internally).
	/// Reference point: a direct delegate call, the floor this traversal is built on.
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
	[Benchmark(Description = "Different message type (Order V1->V2)")]
	public IDispatchMessage DifferentMessageTypeUpcast()
	{
		return _pipeline.Upcast(_orderV1Event);
	}

	#endregion Different Message Type Benchmarks
}
