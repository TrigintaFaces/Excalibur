// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.DependencyInjection;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Measures handler resolution overhead across lifetimes and lookup states.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class HandlerResolutionBenchmarks
{
	private DiagnosticBenchmarkFixture? _fixture;
	private readonly DiagnosticCommand _command = new(42);
	private int _missIndex;

	private static readonly Type[] MissingTypes =
	[
		typeof(UnregisteredDiagnosticCommand),
		typeof(UnregisteredDiagnosticCommand2),
		typeof(UnregisteredDiagnosticCommand3),
		typeof(UnregisteredDiagnosticCommand4),
	];

	[Params(HandlerLifetimeMode.Transient, HandlerLifetimeMode.Scoped, HandlerLifetimeMode.Singleton)]
	public HandlerLifetimeMode HandlerLifetime { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_fixture = new DiagnosticBenchmarkFixture(commandHandlerLifetime: HandlerLifetime, eventHandlerCount: 3);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_fixture?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Resolve action handler")]
	public object ResolveActionHandler()
	{
		if (HandlerLifetime == HandlerLifetimeMode.Scoped)
		{
			using var scope = _fixture!.Services.CreateScope();
			return scope.ServiceProvider.GetRequiredService<IActionHandler<DiagnosticCommand>>();
		}

		return _fixture!.Services.GetRequiredService<IActionHandler<DiagnosticCommand>>();
	}

	[Benchmark(Description = "Dispatch command")]
	public Task<IMessageResult> DispatchCommand()
	{
		return _fixture!.Dispatcher.DispatchAsync(_command, _fixture.CreateContext(), CancellationToken.None);
	}

	[Benchmark(Description = "Registry lookup (warm hit)")]
	public bool RegistryLookupWarmHit()
	{
		return _fixture!.HandlerRegistry.TryGetHandler(typeof(DiagnosticCommand), out _);
	}

	[Benchmark(Description = "Registry lookup (cold miss)")]
	public bool RegistryLookupColdMiss()
	{
		var type = MissingTypes[_missIndex++ & 3];
		return _fixture!.HandlerRegistry.TryGetHandler(type, out _);
	}
}
