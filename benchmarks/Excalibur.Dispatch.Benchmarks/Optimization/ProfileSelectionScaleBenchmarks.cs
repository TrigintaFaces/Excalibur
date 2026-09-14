// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Benchmarks.Comparative;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Measures the warm profile-selection cache hit cost as the number of registered message types grows.
/// </summary>
/// <remarks>
/// <para>
/// Excalibur_Dispatch-zvcdsf: this class used to compare a plain warm <c>ConcurrentDictionary</c>
/// cache against a <c>FrozenDictionary</c> produced by freezing the cache. That comparison is gone
/// because the freeze mechanism is gone -- it measured slower than the plain dictionary at every N
/// tested (1: +27%, 10: +86%, 100: +81%) and, on top of that, disabled the cache's fall-through for
/// any message type first seen after the freeze, so that type re-ran the full profile scan on every
/// subsequent dispatch forever. <see cref="PipelineProfileRegistry"/> now has only one selection
/// cache, always mutable, so a miss is always cached and never pays the scan twice.
/// </para>
/// <para>
/// What remains worth measuring is whether the warm hit stays cheap as the number of distinct
/// registered message types grows, since that count varies per consumer application.
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

	private PipelineProfileRegistry _registry = null!;
	private IDispatchMessage[] _messages = null!;
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

		_registry = new PipelineProfileRegistry();
		foreach (var message in _messages)
		{
			_ = _registry.SelectProfile(message);
		}

		VerifyArmsMeasureWhatTheyClaim();
	}

	/// <summary>
	/// Fails the run if the arm would measure something other than its name.
	/// </summary>
	private void VerifyArmsMeasureWhatTheyClaim()
	{
		if (_messages.Select(m => m.GetType()).ToHashSet().Count != CachedTypeCount)
		{
			throw new InvalidOperationException(
				$"The cache holds fewer than {CachedTypeCount} distinct types; the sweep would not vary N.");
		}

		foreach (var message in _messages)
		{
			_ = _registry.SelectProfile(message) ?? throw new InvalidOperationException(
				"Profile selection returns null for a seeded type; the arm would measure a failed scan.");
		}
	}

	/// <summary>
	/// The rotation alone. The cached arm pays this too, so it is the floor to read it against.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Control: rotation only (no lookup)")]
	public IDispatchMessage Control_RotationOnly() => Next();

	/// <summary>Warm path: a <c>ConcurrentDictionary</c> hit, rotating across all cached types.</summary>
	[Benchmark(Description = "ProfileSelect: warm (ConcurrentDictionary)")]
	public IPipelineProfile? ProfileSelect_Warm() => _registry.SelectProfile(Next());

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
