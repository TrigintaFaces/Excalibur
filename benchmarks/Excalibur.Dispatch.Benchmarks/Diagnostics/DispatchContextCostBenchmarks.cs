// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Measures IMessageContext.Items and key-path overhead for common Dispatch hot keys.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class DispatchContextCostBenchmarks
{
	private const string HotKey = "Dispatch:Result";
	private const string SecondaryKey = "Dispatch:CacheHit";

	private DiagnosticBenchmarkFixture? _fixture;
	private IMessageContext _context = null!;

	[GlobalSetup]
	public void GlobalSetup()
	{
		_fixture = new DiagnosticBenchmarkFixture();
		_context = _fixture.CreateContext();
		_context.Items[HotKey] = 42;
		_context.Items[SecondaryKey] = true;
	}

	[IterationSetup]
	public void IterationSetup()
	{
		_context = _fixture!.CreateContext();
		_context.Items[HotKey] = 42;
		_context.Items[SecondaryKey] = true;
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_fixture?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Items write hot key")]
	public bool ItemsWriteHotKey()
	{
		_context.Items[HotKey] = 1337;
		return _context.Items.ContainsKey(HotKey);
	}

	[Benchmark(Description = "Items read hot key")]
	public int ItemsReadHotKey()
	{
		return _context.Items.TryGetValue(HotKey, out var value) && value is int intValue
			? intValue
			: 0;
	}

	[Benchmark(Description = "Contains + read hot key")]
	public bool ContainsThenReadHotKey()
	{
		return _context.ContainsItem(SecondaryKey) && _context.GetItem<bool>(SecondaryKey);
	}

	[Benchmark(Description = "SetItem + GetItem path")]
	public int SetAndGetViaContextHelpers()
	{
		_context.SetItem(HotKey, 7);
		return _context.GetItem(HotKey, 0);
	}
}
