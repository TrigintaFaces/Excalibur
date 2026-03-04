// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;
using System.Text.Json;

using BenchmarkDotNet.Attributes;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Measures allocation-sensitive serialization and header-encoding patterns used on transport/middleware hot paths.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class SerializationAllocationBenchmarks
{
	private readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private Dictionary<string, object?> _payload = null!;
	private string _traceParent = null!;

	[Params(55, 128)]
	public int TraceParentLength { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_payload = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["messageId"] = Guid.NewGuid().ToString("N"),
			["messageType"] = "BenchmarkMessage",
			["correlationId"] = Guid.NewGuid().ToString("N"),
			["subject"] = "orders.created",
			["version"] = 1,
		};

		const string canonicalTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
		if (TraceParentLength <= canonicalTraceParent.Length)
		{
			_traceParent = canonicalTraceParent[..TraceParentLength];
		}
		else
		{
			_traceParent = canonicalTraceParent + new string('a', TraceParentLength - canonicalTraceParent.Length);
		}
	}

	[Benchmark(Baseline = true, Description = "JSON string + UTF8 bytes")]
	public byte[] SerializeViaStringThenUtf8()
	{
		var json = JsonSerializer.Serialize(_payload, _jsonOptions);
		return Encoding.UTF8.GetBytes(json);
	}

	[Benchmark(Description = "SerializeToUtf8Bytes")]
	public byte[] SerializeToUtf8BytesDirect()
	{
		return JsonSerializer.SerializeToUtf8Bytes(_payload, _jsonOptions);
	}

	[Benchmark(Description = "Trace header pooled+copy")]
	public byte[] TraceHeaderPooledCopy()
	{
		var count = Encoding.UTF8.GetByteCount(_traceParent);
		var rented = ArrayPool<byte>.Shared.Rent(count);

		try
		{
			var written = Encoding.UTF8.GetBytes(_traceParent, rented);
			return rented.AsSpan(0, written).ToArray();
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(rented);
		}
	}

	[Benchmark(Description = "Trace header direct bytes")]
	public byte[] TraceHeaderDirectBytes()
	{
		return Encoding.UTF8.GetBytes(_traceParent);
	}
}
