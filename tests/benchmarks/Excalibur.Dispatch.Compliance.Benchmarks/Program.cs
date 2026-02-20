// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

using Excalibur.Dispatch.Compliance.Benchmarks;

/// <summary>
/// Entry point for running Excalibur.Dispatch.Compliance benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// Per AD-257-2, benchmarks use [MemoryDiagnoser] for allocation tracking.
/// Per AD-257-3, results are exported in JSON for CI comparison.
/// </para>
/// <para>
/// Usage:
///   dotnet run -c Release -- --filter *EncryptionBenchmarks*
///   dotnet run -c Release -- --export json --artifacts artifacts/benchmarks
/// </para>
/// </remarks>
var config = DefaultConfig.Instance
	.AddExporter(JsonExporter.Full)
	.AddExporter(MarkdownExporter.GitHub)
	.WithArtifactsPath("artifacts/benchmarks");

BenchmarkSwitcher
	.FromAssembly(typeof(EncryptionBenchmarks).Assembly)
	.Run(args, config);
