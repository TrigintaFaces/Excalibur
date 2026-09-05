// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Benchmarks.Comparative;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Microbenchmarks asking whether three per-call caches actually pay for themselves.
/// </summary>
/// <remarks>
/// <para>
/// Every arm runs under one job (<see cref="WarmPathBenchmarkConfig"/>: in-process emit toolchain,
/// auto-calibrated invocation count). That is deliberate and is the whole point of the class -- a
/// cached arm and its uncached arm are only comparable if they were measured the same way.
/// </para>
/// <para>
/// Two earlier defects are worth stating, because both produced numbers that read like findings:
/// </para>
/// <list type="bullet">
/// <item>
/// The frozen profile-selection arm used <c>[IterationSetup]</c>, which forces BenchmarkDotNet onto
/// <c>InvocationCount=1, UnrollFactor=1</c> for that method alone. It reported 1,341 ns +/- 396
/// against a warm arm measured in single-digit nanoseconds under the default job -- two numbers from
/// two different instruments, so their difference meant nothing. Both registries are now built once
/// in <see cref="Setup"/> and both arms run under the same job.
/// </item>
/// <item>
/// The activity-name cache shared one dictionary with the type-name cache, keyed on the same
/// <see cref="Type"/>. It therefore returned the type name where the activity name was expected and
/// its factory never ran, so the two "cached" arms were literally the same lookup measured twice
/// (2.6605 ns and 2.6575 ns). They are separate dictionaries now, and <see cref="Setup"/> asserts
/// each seeded value is the one its arm claims to return.
/// </item>
/// </list>
/// <para>
/// The <c>Control</c> arm exists so the reader can tell a real measurement from a folded-away one.
/// It returns a compile-time constant, which is the floor an arm measures when the JIT has optimised
/// its body to nothing. An arm materially above that floor is doing real work; an arm at it is not.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(WarmPathBenchmarkConfig))]
public class CacheOptimizationBenchmarks
{
	// --- Profile Selection ---
	// Two registries, both fully built in Setup. Freezing is one-way per instance, so a single
	// registry cannot serve both arms without per-iteration setup -- which is what broke the
	// comparison before.
	private PipelineProfileRegistry _warmRegistry = null!;
	private PipelineProfileRegistry _frozenRegistry = null!;
	private TestActionMessage _actionMessage = null!;

	// --- Type / Activity Name ---
	// SEPARATE dictionaries. One dictionary keyed on Type cannot hold two different values per type.
	private static readonly ConcurrentDictionary<Type, string> TypeNameCache = new();
	private static readonly ConcurrentDictionary<Type, string> ActivityNameCache = new();
	private Type _messageType = null!;

	/// <summary>The folding floor: what an arm costs when its body has been optimised to nothing.</summary>
	private const string FoldingFloor = "TestActionMessage";

	private const string ActivityNamePrefix = "middleware.";

	// --- Message Kind ---
	private static readonly ConcurrentDictionary<string, string> MessageKindCache = new();
	private string _commandTypeName = null!;
	private string _eventTypeName = null!;

	[GlobalSetup]
	public void Setup()
	{
		_actionMessage = new TestActionMessage();

		_warmRegistry = new PipelineProfileRegistry();
		_ = _warmRegistry.SelectProfile(_actionMessage);

		_frozenRegistry = new PipelineProfileRegistry();
		_ = _frozenRegistry.SelectProfile(_actionMessage);
		_frozenRegistry.FreezeProfileSelectionCache();

		_messageType = typeof(TestActionMessage);
		_ = TypeNameCache.TryAdd(_messageType, _messageType.Name);
		_ = ActivityNameCache.TryAdd(_messageType, string.Concat(ActivityNamePrefix, _messageType.Name));

		_commandTypeName = "SubmitOrderCommand";
		_eventTypeName = "OrderSubmittedEvent";
		_ = MessageKindCache.TryAdd(_commandTypeName, "Action");
		_ = MessageKindCache.TryAdd(_eventTypeName, "Event");

		VerifyArmsMeasureWhatTheyClaim();
	}

	/// <summary>
	/// Fails the run if any arm would silently measure something other than its name.
	/// </summary>
	/// <remarks>
	/// Without this, a cache that was never seeded measures its factory, a registry that failed to
	/// freeze measures the warm path, and a mis-seeded cache returns the wrong string -- all of which
	/// still produce a clean-looking report. Every one of those has already happened in this class.
	/// </remarks>
	private void VerifyArmsMeasureWhatTheyClaim()
	{
		if (_frozenRegistry.IsProfileSelectionCacheFrozen is false)
		{
			throw new InvalidOperationException(
				"The frozen arm's registry is not frozen; it would measure the warm path under the frozen name.");
		}

		if (_warmRegistry.IsProfileSelectionCacheFrozen)
		{
			throw new InvalidOperationException(
				"The warm arm's registry is frozen; both profile-selection arms would measure the same path.");
		}

		if (!TypeNameCache.TryGetValue(_messageType, out var cachedTypeName) ||
			!string.Equals(cachedTypeName, _messageType.Name, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"The type-name cache is not seeded with the type name; the cached arm would run its factory.");
		}

		if (!ActivityNameCache.TryGetValue(_messageType, out var cachedActivityName) ||
			!string.Equals(cachedActivityName, ActivityNamePrefix + _messageType.Name, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"The activity-name cache does not hold the activity name; the cached arm would return the wrong string.");
		}

		if (!MessageKindCache.TryGetValue(_commandTypeName, out var cachedKind) ||
			!string.Equals(cachedKind, DetermineMessageKindUncached(_commandTypeName), StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"The message-kind cache disagrees with the uncached computation; the two arms are not comparable.");
		}

		if (!string.Equals(FoldingFloor, _messageType.Name, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"The control constant no longer matches the type name it is the floor for.");
		}
	}

	#region Folding Floor Control

	/// <summary>
	/// Returns a compile-time constant. Nothing measurable happens here, so this arm's mean is the
	/// cost of BenchmarkDotNet's own call -- the floor an arm reports when the JIT has folded its
	/// body away. Read every other arm against this before calling any of them "too fast to be real".
	/// </summary>
	[Benchmark(Description = "Control: return a constant (folding floor)")]
	public string Control_ReturnConstant() => FoldingFloor;

	#endregion

	#region Profile Selection

	/// <summary>
	/// Warm-path profile selection: a per-type <see cref="ConcurrentDictionary{TKey, TValue}"/> hit.
	/// </summary>
	[Benchmark(Description = "ProfileSelect: warm (ConcurrentDictionary)")]
	public IPipelineProfile? ProfileSelect_Warm() => _warmRegistry.SelectProfile(_actionMessage);

	/// <summary>
	/// Frozen-path profile selection: a <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/>
	/// hit after <c>FreezeProfileSelectionCache()</c>. Same job as the warm arm, so the two are
	/// directly comparable and the before/after-freeze claim is expressible from this class.
	/// </summary>
	[Benchmark(Description = "ProfileSelect: frozen (FrozenDictionary)")]
	public IPipelineProfile? ProfileSelect_Frozen() => _frozenRegistry.SelectProfile(_actionMessage);

	#endregion

	#region Type Name Caching

	/// <summary>
	/// <c>Type.Name</c> read through a field, so the JIT cannot see which type it is. This is what a
	/// caller actually does, and it is the baseline the cache has to beat.
	/// </summary>
	/// <remarks>
	/// Named "raw reflection" previously, which oversold it: the <see cref="Type"/> is already in
	/// hand, and the runtime caches the name string inside it. No lookup is performed here.
	/// </remarks>
	[Benchmark(Baseline = true, Description = "TypeName: Type.Name (type from a field)")]
	public string TypeName_FromField() => _messageType.Name;

	/// <summary>
	/// The same property read where the JIT knows the exact type at compile time. Compared against
	/// the field arm it answers whether the optimiser is folding this call away.
	/// </summary>
	[Benchmark(Description = "TypeName: Type.Name (type known to the JIT)")]
	public string TypeName_FromTypeof() => typeof(TestActionMessage).Name;

	/// <summary>
	/// The cache under test: a <see cref="ConcurrentDictionary{TKey, TValue}"/> lookup keyed on the type.
	/// </summary>
	[Benchmark(Description = "TypeName: ConcurrentDictionary cache")]
	public string TypeName_Cached() => TypeNameCache.GetOrAdd(_messageType, static t => t.Name);

	#endregion

	#region Activity Name Caching

	/// <summary>
	/// Uncached: string interpolation builds a new activity name on every call.
	/// </summary>
	[Benchmark(Description = "ActivityName: interpolated")]
	public string ActivityName_Interpolated() => $"{ActivityNamePrefix}{_messageType.Name}";

	/// <summary>
	/// Cached: a lookup in the activity-name dictionary, which returns the whole prefixed string.
	/// </summary>
	[Benchmark(Description = "ActivityName: ConcurrentDictionary cache")]
	public string ActivityName_Cached() =>
		ActivityNameCache.GetOrAdd(_messageType, static t => string.Concat(ActivityNamePrefix, t.Name));

	#endregion

	#region Message Kind Determination

	/// <summary>
	/// Uncached: up to four <c>string.Contains</c> scans over the type name.
	/// </summary>
	[Benchmark(Description = "MessageKind: string.Contains")]
	public string MessageKind_StringContains() => DetermineMessageKindUncached(_commandTypeName);

	/// <summary>
	/// Cached: a string-keyed <see cref="ConcurrentDictionary{TKey, TValue}"/> lookup, which has to
	/// hash the same string the uncached arm merely scans.
	/// </summary>
	[Benchmark(Description = "MessageKind: ConcurrentDictionary cache")]
	public string MessageKind_Cached() =>
		MessageKindCache.GetOrAdd(_commandTypeName, static name => DetermineMessageKindUncached(name));

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
