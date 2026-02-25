// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Separates cancellation costs for pre-canceled tokens, in-flight cancellation, and raw CTS callback fan-out.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class CancellationCostBenchmarks
{
	private const string ReturnCancelledResultContextKey = "Dispatch:ReturnCancelledResult";
	private DiagnosticBenchmarkFixture? _fixture;
	private readonly CancelableCommand _cancelableCommand = new(42, DelayMs: 10);

	[Params(1, 8, 32)]
	public int CallbackCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_fixture = new DiagnosticBenchmarkFixture(includeCancelableHandler: true);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_fixture?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Pre-canceled dispatch (throw path)")]
	public async Task<bool> PreCanceledDispatchThrowPath()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		try
		{
			_ = await _fixture!.Dispatcher
				.DispatchAsync(_cancelableCommand, _fixture.CreateContext(), cts.Token)
				.ConfigureAwait(false);
			return false;
		}
		catch (OperationCanceledException)
		{
			return true;
		}
	}

	[Benchmark(Description = "Pre-canceled dispatch (result path)")]
	public async Task<bool> PreCanceledDispatchResultPath()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var context = _fixture!.CreateContext();
		context.SetItem(ReturnCancelledResultContextKey, true);

		var result = await _fixture.Dispatcher.DispatchAsync(_cancelableCommand, context, cts.Token).ConfigureAwait(false);
		return !result.Succeeded;
	}

	[Benchmark(Description = "In-flight dispatch cancellation")]
	public async Task<bool> InFlightDispatchCancellation()
	{
		using var cts = new CancellationTokenSource();
		var dispatchTask = _fixture!.Dispatcher.DispatchAsync(_cancelableCommand, _fixture.CreateContext(), cts.Token);

		cts.Cancel();

		try
		{
			_ = await dispatchTask.ConfigureAwait(false);
			return false;
		}
		catch (OperationCanceledException)
		{
			return true;
		}
	}

	[Benchmark(Description = "CTS cancel callback fan-out")]
	public int CancellationCallbackFanOut()
	{
		using var cts = new CancellationTokenSource();
		var callbackHits = 0;
		var registrations = new CancellationTokenRegistration[CallbackCount];
		for (var i = 0; i < CallbackCount; i++)
		{
			registrations[i] = cts.Token.Register(() => Interlocked.Increment(ref callbackHits));
		}

		cts.Cancel();

		for (var i = 0; i < registrations.Length; i++)
		{
			registrations[i].Dispose();
		}

		return callbackHits;
	}
}
