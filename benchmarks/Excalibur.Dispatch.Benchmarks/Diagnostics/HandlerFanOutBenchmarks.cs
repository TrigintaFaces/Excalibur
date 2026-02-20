// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Measures event fan-out delivery costs and warm/cold behavior for handler lookup.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class HandlerFanOutBenchmarks
{
	private DiagnosticBenchmarkFixture? _warmFixture;
	private DiagnosticBenchmarkFixture? _coldFixture;
	private DiagnosticEvent _event = null!;

	[Params(1, 3, 10, 50)]
	public int EventHandlerCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_warmFixture = new DiagnosticBenchmarkFixture(eventHandlerCount: EventHandlerCount);
		_event = new DiagnosticEvent(42, "fan-out");
	}

	[IterationSetup(Target = nameof(EventDispatchCold))]
	public void IterationSetup()
	{
		_coldFixture?.Dispose();
		_coldFixture = new DiagnosticBenchmarkFixture(eventHandlerCount: EventHandlerCount);
	}

	[IterationCleanup(Target = nameof(EventDispatchCold))]
	public void IterationCleanup()
	{
		_coldFixture?.Dispose();
		_coldFixture = null;
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_warmFixture?.Dispose();
		_coldFixture?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Event dispatch (warm)")]
	public Task<IMessageResult> EventDispatchWarm()
	{
		return _warmFixture!.Dispatcher.DispatchAsync(_event, _warmFixture.CreateContext(), CancellationToken.None);
	}

	[Benchmark(Description = "Event dispatch (cold)")]
	public Task<IMessageResult> EventDispatchCold()
	{
		return _coldFixture!.Dispatcher.DispatchAsync(_event, _coldFixture.CreateContext(), CancellationToken.None);
	}
}
