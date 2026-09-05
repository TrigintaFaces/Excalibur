// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Benchmarks.Comparative;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Asks whether freezing the profile-selection cache actually pays, and at what number of registered
/// message types.
/// </summary>
/// <remarks>
/// <para>
/// The sibling <see cref="CacheOptimizationBenchmarks"/> compares warm against frozen at exactly one
/// cached message type, where a frozen dictionary has nothing to win. This class varies that count so
/// the answer can be "it depends on N, and here is the crossover" rather than a single reading taken
/// at the one point where the question is uninteresting.
/// </para>
/// <para>
/// It also measures the cold path, which no other arm reaches. That is worth measuring for two
/// reasons. It is the candidate explanation for the retired "handler lookup ~50 ns to ~5 ns" claim --
/// which may have described cold-to-cached rather than warm-to-frozen. And it is reachable in
/// production after freezing: a message type absent from the frozen cache finds no mutable dictionary
/// to fall back to, so it re-runs the full profile scan on every dispatch, forever. The cold arm
/// exercises exactly that by asking the frozen registry for a type it was never seeded with -- which
/// is also why it needs no per-iteration setup and runs under the same job as every other arm.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(WarmPathBenchmarkConfig))]
public class ProfileSelectionScaleBenchmarks
{
	private static readonly Type[] TypeArguments =
	[
		typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int),
		typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double),
	];

	private PipelineProfileRegistry _warmRegistry = null!;
	private PipelineProfileRegistry _frozenRegistry = null!;
	private IDispatchMessage[] _messages = null!;
	private IDispatchMessage _neverSeeded = null!;
	private int _index;

	/// <summary>Gets or sets how many distinct message types the selection cache holds.</summary>
	[Params(1, 10, 100)]
	public int CachedTypeCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_messages = new IDispatchMessage[CachedTypeCount];
		for (var i = 0; i < CachedTypeCount; i++)
		{
			var closed = typeof(ScaleMessage<,>).MakeGenericType(
				TypeArguments[i / TypeArguments.Length],
				TypeArguments[i % TypeArguments.Length]);
			_messages[i] = (IDispatchMessage)Activator.CreateInstance(closed)!;
		}

		// decimal appears in no grid position, so this type is absent from both caches by construction.
		_neverSeeded = new ScaleMessage<decimal, decimal>();

		_warmRegistry = new PipelineProfileRegistry();
		_frozenRegistry = new PipelineProfileRegistry();
		foreach (var message in _messages)
		{
			_ = _warmRegistry.SelectProfile(message);
			_ = _frozenRegistry.SelectProfile(message);
		}

		_frozenRegistry.FreezeProfileSelectionCache();

		VerifyArmsMeasureWhatTheyClaim();
	}

	/// <summary>
	/// Fails the run if any arm would measure something other than its name.
	/// </summary>
	private void VerifyArmsMeasureWhatTheyClaim()
	{
		if (_frozenRegistry.IsProfileSelectionCacheFrozen is false || _warmRegistry.IsProfileSelectionCacheFrozen)
		{
			throw new InvalidOperationException(
				"The two registries are not in opposite freeze states; the arms would measure the same path.");
		}

		if (_messages.Select(m => m.GetType()).ToHashSet().Count != CachedTypeCount)
		{
			throw new InvalidOperationException(
				$"The cache holds fewer than {CachedTypeCount} distinct types; the sweep would not vary N.");
		}

		if (_messages.Any(m => m.GetType() == _neverSeeded.GetType()))
		{
			throw new InvalidOperationException(
				"The cold arm's message type is in the seeded set, so that arm would measure a cache hit.");
		}

		foreach (var message in _messages)
		{
			var warm = _warmRegistry.SelectProfile(message);
			if (warm is null)
			{
				throw new InvalidOperationException(
					"Profile selection returns null for a seeded type; every arm would measure a failed scan.");
			}

			// Compared by name, not by reference: each registry builds its own profile instances, so
			// reference equality across the two would never hold and would fail every run.
			var frozen = _frozenRegistry.SelectProfile(message);
			if (frozen is null || !string.Equals(warm.Name, frozen.Name, StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					"Freezing changed the selected profile; the two arms are not measuring the same lookup.");
			}
		}
	}

	/// <summary>
	/// The rotation alone. Both cached arms pay this, so it is the floor to read them against.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Control: rotation only (no lookup)")]
	public IDispatchMessage Control_RotationOnly() => Next();

	/// <summary>Warm path: a <c>ConcurrentDictionary</c> hit, rotating across all cached types.</summary>
	[Benchmark(Description = "ProfileSelect: warm (ConcurrentDictionary)")]
	public IPipelineProfile? ProfileSelect_Warm() => _warmRegistry.SelectProfile(Next());

	/// <summary>Frozen path: a <c>FrozenDictionary</c> hit, rotating across the same types.</summary>
	[Benchmark(Description = "ProfileSelect: frozen (FrozenDictionary)")]
	public IPipelineProfile? ProfileSelect_Frozen() => _frozenRegistry.SelectProfile(Next());

	/// <summary>
	/// Cold path: the full profile scan, reached by asking the frozen registry for a type it never
	/// cached. No rotation, so read it against the control's absence rather than its value.
	/// </summary>
	[Benchmark(Description = "ProfileSelect: cold (full profile scan)")]
	public IPipelineProfile? ProfileSelect_Cold() => _frozenRegistry.SelectProfile(_neverSeeded);

	private IDispatchMessage Next()
	{
		var next = _index + 1;
		_index = next == _messages.Length ? 0 : next;
		return _messages[_index];
	}

	private sealed class ScaleMessage<T1, T2> : IDispatchEvent
	{
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
	}
}
