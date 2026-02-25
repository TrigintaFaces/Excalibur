// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Quantifies incremental dispatch cost as middleware depth increases.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class MiddlewareCostCurveBenchmarks
{
	private DiagnosticBenchmarkFixture? _fixture;
	private DiagnosticCommand _command = null!;
	private DiagnosticQuery _query = null!;
	private DiagnosticEvent _event = null!;

	[Params(0, 1, 3, 5, 10)]
	public int MiddlewareCount { get; set; }

	[Params(DispatchScenario.Command, DispatchScenario.Query, DispatchScenario.Event)]
	public DispatchScenario Scenario { get; set; }

	[Params(false, true)]
	public bool CacheHit { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_fixture = new DiagnosticBenchmarkFixture(middlewareCount: MiddlewareCount, eventHandlerCount: 3);
		_command = new DiagnosticCommand(42);
		_query = new DiagnosticQuery(42);
		_event = new DiagnosticEvent(42, "cost-curve");
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_fixture?.Dispose();
	}

	[Benchmark(Baseline = true)]
	public async Task<IMessageResult> Dispatch_WithConfiguredMiddleware()
	{
		return Scenario switch
		{
			DispatchScenario.Command => await _fixture!.Dispatcher
				.DispatchAsync(_command, _fixture.CreateContext(CacheHit, cachedResult: 0), CancellationToken.None)
				.ConfigureAwait(false),
			DispatchScenario.Query => await _fixture!.Dispatcher
				.DispatchAsync<DiagnosticQuery, int>(_query, _fixture.CreateContext(CacheHit, cachedResult: 43), CancellationToken.None)
				.ConfigureAwait(false),
			_ => await _fixture!.Dispatcher
				.DispatchAsync(_event, _fixture.CreateContext(), CancellationToken.None)
				.ConfigureAwait(false),
		};
	}
}
