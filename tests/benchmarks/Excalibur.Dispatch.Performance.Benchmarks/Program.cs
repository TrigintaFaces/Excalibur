// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

using Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Entry point for running Dispatch core benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// All benchmarks use [MemoryDiagnoser] for allocation tracking.
/// Results are exported in JSON for CI comparison.
/// </para>
/// <para>
/// Usage:
///   dotnet run -c Release -- --filter *PipelineBenchmarks*
///   dotnet run -c Release -- --filter *SerializationBenchmarks*
///   dotnet run -c Release -- --export json --artifacts artifacts/benchmarks
/// </para>
/// </remarks>
var config = DefaultConfig.Instance
	.AddExporter(JsonExporter.Full)
	.AddExporter(MarkdownExporter.GitHub)
	.WithArtifactsPath("artifacts/benchmarks");

BenchmarkSwitcher
	.FromAssembly(typeof(PipelineBenchmarks).Assembly)
	.Run(args, config);
