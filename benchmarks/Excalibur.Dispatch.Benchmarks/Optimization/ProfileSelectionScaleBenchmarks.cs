// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Benchmarks.Comparative;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Measures repeated profile selection across a rotating set of concrete message types.
/// </summary>
/// <remarks>
/// The current registry scans its captured profile-membership array on each selection.
/// Message-kind classification caching is distinct from selection-result caching.
/// Earlier dictionary/freeze comparisons belong to their historical source epoch.
/// This fixture varies message types, not registered profile count.
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

	/// <summary>Gets or sets the number of distinct rotating message types; the identifier is retained for historical case identity.</summary>
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
	/// The rotation alone. The selection arm includes this work; separate timings do not isolate an exclusive selection cost.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Control: rotation only (no lookup)")]
	public IDispatchMessage Control_RotationOnly() => Next();

	/// <summary>Repeated profile selection, rotating across all message types; membership is scanned on each call.</summary>
	[Benchmark(Description = "ProfileSelect: repeated selection (rotating types)")]
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
