// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Decomposes cold fan-out cost into fixture build, first lookup, first activation, and first invocation.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsColdBenchmarkConfig))]
public class FanOutColdDecompositionBenchmarks
{
	private readonly DiagnosticEvent _event = new(42, "cold-fanout");

	[Params(1, 10, 50)]
	public int EventHandlerCount { get; set; }

	[Benchmark(Baseline = true, Description = "Cold: fixture build only")]
	public bool FixtureBuildOnly()
	{
		using var fixture = new DiagnosticBenchmarkFixture(eventHandlerCount: EventHandlerCount);
		return fixture.Dispatcher is not null;
	}

	[Benchmark(Description = "Cold: first handler lookup")]
	public int FirstHandlerLookup()
	{
		using var fixture = new DiagnosticBenchmarkFixture(eventHandlerCount: EventHandlerCount);
		return fixture.HandlerRegistry.GetAll().Count(static entry => entry.MessageType == typeof(DiagnosticEvent));
	}

	[Benchmark(Description = "Cold: first handler activation")]
	public bool FirstHandlerActivation()
	{
		using var fixture = new DiagnosticBenchmarkFixture(eventHandlerCount: EventHandlerCount);
		var entry = fixture.HandlerRegistry.GetAll()
			.FirstOrDefault(static handler => handler.MessageType == typeof(DiagnosticEvent));
		if (entry is null)
		{
			return false;
		}

		var context = fixture.CreateContext();
		var handler = fixture.HandlerActivator.ActivateHandler(entry.HandlerType, context, fixture.Services);
		return handler is not null;
	}

	[Benchmark(Description = "Cold: first handler invoke")]
	public async Task<bool> FirstHandlerInvoke()
	{
		using var fixture = new DiagnosticBenchmarkFixture(eventHandlerCount: EventHandlerCount);
		var entry = fixture.HandlerRegistry.GetAll()
			.FirstOrDefault(static handler => handler.MessageType == typeof(DiagnosticEvent));
		if (entry is null)
		{
			return false;
		}

		var context = fixture.CreateContext();
		var handler = fixture.HandlerActivator.ActivateHandler(entry.HandlerType, context, fixture.Services);
		_ = await fixture.HandlerInvoker.InvokeAsync(handler, _event, CancellationToken.None).ConfigureAwait(false);

		return true;
	}
}
