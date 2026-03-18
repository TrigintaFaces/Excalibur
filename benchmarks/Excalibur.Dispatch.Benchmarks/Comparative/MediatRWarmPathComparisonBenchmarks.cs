// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

/// <summary>
/// WarmPath variants of comparative benchmarks for published documentation numbers.
/// Uses BDN defaults (auto-calibrated InvocationCount, UnrollFactor) for steady-state throughput.
/// Run with: --filter *WarmPath*
/// </summary>
/// <remarks>
/// These classes inherit all benchmark methods from their ColdPath counterparts.
/// The only difference is the <see cref="WarmPathBenchmarkConfig"/> which lets BDN
/// auto-calibrate iteration counts for statistically meaningful warm-JIT results.
/// </remarks>

#pragma warning disable SA1402 // File may only contain a single type - WarmPath variants are trivial one-line subclasses

[Config(typeof(WarmPathBenchmarkConfig))]
public class MediatRWarmPathComparisonBenchmarks : MediatRComparisonBenchmarks;

[Config(typeof(WarmPathBenchmarkConfig))]
public class WolverineWarmPathComparisonBenchmarks : WolverineComparisonBenchmarks;

[Config(typeof(WarmPathBenchmarkConfig))]
public class WolverineInProcessWarmPathComparisonBenchmarks : WolverineInProcessComparisonBenchmarks;

[Config(typeof(WarmPathBenchmarkConfig))]
public class MassTransitWarmPathComparisonBenchmarks : MassTransitComparisonBenchmarks;

[Config(typeof(WarmPathBenchmarkConfig))]
public class MassTransitMediatorWarmPathComparisonBenchmarks : MassTransitMediatorComparisonBenchmarks;

[Config(typeof(WarmPathBenchmarkConfig))]
public class PipelineWarmPathComparisonBenchmarks : PipelineComparisonBenchmarks;

[Config(typeof(WarmPathBenchmarkConfig))]
public class TransportQueueParityWarmPathComparisonBenchmarks : TransportQueueParityComparisonBenchmarks;

[Config(typeof(WarmPathBenchmarkConfig))]
public class RoutingFirstParityWarmPathBenchmarks : RoutingFirstParityBenchmarks;
