// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Microbenchmarks validating P0 cache optimizations from the performance review sprint.
/// </summary>
/// <remarks>
/// Measures the cost of:
/// <list type="bullet">
/// <item>Profile selection: cached (warm) vs uncached (cold) path</item>
/// <item>Activity name: cached type name vs raw reflection</item>
/// <item>Message kind determination: cached vs uncached string.Contains</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class CacheOptimizationBenchmarks
{
	// --- Profile Selection ---
	private PipelineProfileRegistry _registry = null!;
	private TestActionMessage _actionMessage = null!;

	// --- Activity Name ---
	private static readonly ConcurrentDictionary<Type, string> TypeNameCache = new();
	private Type _messageType = null!;

	// --- Message Kind ---
	private static readonly ConcurrentDictionary<string, string> MessageKindCache = new();
	private string _commandTypeName = null!;
	private string _eventTypeName = null!;

	[GlobalSetup]
	public void Setup()
	{
		// Profile selection setup
		_registry = new PipelineProfileRegistry();
		_actionMessage = new TestActionMessage();

		// Warm the profile cache with a first call
		_ = _registry.SelectProfile(_actionMessage);

		// Activity name setup
		_messageType = typeof(TestActionMessage);
		TypeNameCache.TryAdd(_messageType, _messageType.Name);

		// Message kind setup
		_commandTypeName = "SubmitOrderCommand";
		_eventTypeName = "OrderSubmittedEvent";
		MessageKindCache.TryAdd(_commandTypeName, "Action");
		MessageKindCache.TryAdd(_eventTypeName, "Event");
	}

	#region Profile Selection

	/// <summary>
	/// Warm-path profile selection: per-type cache hit.
	/// </summary>
	[Benchmark(Description = "ProfileSelect: cached (warm)")]
	public IPipelineProfile? ProfileSelect_Cached()
	{
		return _registry.SelectProfile(_actionMessage);
	}

	/// <summary>
	/// Frozen-path profile selection after FreezeProfileSelectionCache().
	/// </summary>
	[Benchmark(Description = "ProfileSelect: frozen")]
	public IPipelineProfile? ProfileSelect_Frozen()
	{
		// Note: We freeze in setup for this benchmark via iteration setup
		return _registry.SelectProfile(_actionMessage);
	}

	[IterationSetup(Target = nameof(ProfileSelect_Frozen))]
	public void FreezeSetup()
	{
		// Recreate and freeze for each iteration
		_registry = new PipelineProfileRegistry();
		_ = _registry.SelectProfile(_actionMessage); // Warm cache
		_registry.FreezeProfileSelectionCache(); // Freeze
	}

	[IterationCleanup(Target = nameof(ProfileSelect_Frozen))]
	public void FreezeCleanup()
	{
		// Restore unfrozen registry for other benchmarks
		_registry = new PipelineProfileRegistry();
		_ = _registry.SelectProfile(_actionMessage);
	}

	#endregion

	#region Activity Name Caching

	/// <summary>
	/// Uncached: raw Type.Name reflection per call.
	/// </summary>
	[Benchmark(Baseline = true, Description = "TypeName: raw reflection")]
	public string TypeName_RawReflection()
	{
		return _messageType.Name;
	}

	/// <summary>
	/// Cached: ConcurrentDictionary lookup for type name.
	/// </summary>
	[Benchmark(Description = "TypeName: cached")]
	public string TypeName_Cached()
	{
		return TypeNameCache.GetOrAdd(_messageType, static t => t.Name);
	}

	/// <summary>
	/// Uncached: string interpolation for activity name.
	/// </summary>
	[Benchmark(Description = "ActivityName: interpolated")]
	public string ActivityName_Interpolated()
	{
		return $"middleware.{_messageType.Name}";
	}

	/// <summary>
	/// Cached: ConcurrentDictionary lookup for activity name.
	/// </summary>
	[Benchmark(Description = "ActivityName: cached")]
	public string ActivityName_Cached()
	{
		return TypeNameCache.GetOrAdd(_messageType, static t => string.Concat("middleware.", t.Name));
	}

	#endregion

	#region Message Kind Determination

	/// <summary>
	/// Uncached: string.Contains per call.
	/// </summary>
	[Benchmark(Description = "MessageKind: string.Contains")]
	public string MessageKind_StringContains()
	{
		return DetermineMessageKindUncached(_commandTypeName);
	}

	/// <summary>
	/// Cached: ConcurrentDictionary lookup.
	/// </summary>
	[Benchmark(Description = "MessageKind: cached")]
	public string MessageKind_Cached()
	{
		return MessageKindCache.GetOrAdd(_commandTypeName, static name =>
		{
			if (name.Contains("Command", StringComparison.Ordinal) ||
				name.Contains("Query", StringComparison.Ordinal))
			{
				return "Action";
			}

			if (name.Contains("Event", StringComparison.Ordinal) ||
				name.Contains("Notification", StringComparison.Ordinal))
			{
				return "Event";
			}

			return "Unknown";
		});
	}

	private static string DetermineMessageKindUncached(string typeName)
	{
		if (typeName.Contains("Command", StringComparison.Ordinal) ||
			typeName.Contains("Query", StringComparison.Ordinal))
		{
			return "Action";
		}

		if (typeName.Contains("Event", StringComparison.Ordinal) ||
			typeName.Contains("Notification", StringComparison.Ordinal))
		{
			return "Event";
		}

		return "Unknown";
	}

	#endregion

	#region Test Types

	private sealed class TestActionMessage : IDispatchMessage
	{
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
	}

	#endregion
}
