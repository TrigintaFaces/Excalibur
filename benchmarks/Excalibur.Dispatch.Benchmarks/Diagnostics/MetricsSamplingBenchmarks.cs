// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;
using System.Security.Cryptography;

using BenchmarkDotNet.Attributes;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Microbenchmarks for high-frequency metrics sampling and bypass checks.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class MetricsSamplingBenchmarks
{
	private readonly string[] _bypassTypes =
	[
		"HealthCheckCommand",
		"HeartbeatEvent",
		"WarmupCommand",
		"BackgroundProbeEvent",
		"SyntheticValidationMessage",
		"DiagnosticsPing",
		"CacheWarmCommand",
		"TelemetryProbe",
	];

	private readonly FrozenSet<string> _bypassTypeSet;
	private readonly string _messageType = "OrderSubmitted";
	private readonly double _sampleRate = 0.25d;

	public MetricsSamplingBenchmarks()
	{
		_bypassTypeSet = _bypassTypes.ToFrozenSet(StringComparer.Ordinal);
	}

	[Benchmark(Baseline = true, Description = "Bypass: Array.Exists")]
	public bool BypassCheck_ArrayExists()
	{
		return Array.Exists(_bypassTypes, t => string.Equals(t, _messageType, StringComparison.Ordinal));
	}

	[Benchmark(Description = "Bypass: FrozenSet.Contains")]
	public bool BypassCheck_FrozenSet()
	{
		return _bypassTypeSet.Contains(_messageType);
	}

	[Benchmark(Description = "Sampling: RandomNumberGenerator.GetInt32")]
	public bool Sampling_CryptoRng()
	{
		var threshold = (int)(_sampleRate * int.MaxValue);
		return RandomNumberGenerator.GetInt32(int.MaxValue) <= threshold;
	}

	[Benchmark(Description = "Sampling: Random.Shared.NextDouble")]
	public bool Sampling_RandomShared()
	{
		return Random.Shared.NextDouble() <= _sampleRate;
	}
}
