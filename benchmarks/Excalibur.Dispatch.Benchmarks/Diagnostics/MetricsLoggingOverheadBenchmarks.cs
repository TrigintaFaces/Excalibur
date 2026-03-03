// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using BenchmarkDotNet.Attributes;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Focused microbenchmarks for metrics middleware hot-path payload sizing behavior.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class MetricsLoggingOverheadBenchmarks
{
	private static readonly JsonSerializerOptions SerializerOptions = new();
	private TestMessage _message = null!;

	[GlobalSetup]
	public void Setup()
	{
		_message = new TestMessage(
			Id: Guid.NewGuid().ToString("N"),
			Type: "OrderSubmitted",
			CorrelationId: Guid.NewGuid().ToString("N"),
			Payload: new string('x', 512));
	}

	[Benchmark(Baseline = true, Description = "Size via JSON string + UTF8 count")]
	public long EstimateSize_ViaStringAndUtf8Count()
	{
		var json = JsonSerializer.Serialize(_message, _message.GetType(), SerializerOptions);
		return Encoding.UTF8.GetByteCount(json);
	}

	[Benchmark(Description = "Size via SerializeToUtf8Bytes")]
	public long EstimateSize_ViaUtf8Bytes()
	{
		var bytes = JsonSerializer.SerializeToUtf8Bytes(_message, _message.GetType(), SerializerOptions);
		return bytes.Length;
	}

	[Benchmark(Description = "Size estimation skipped")]
	public long EstimateSize_Skipped()
	{
		return 0;
	}

	private sealed record TestMessage(string Id, string Type, string CorrelationId, string Payload);
}
