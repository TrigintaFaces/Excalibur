// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.LoadTests;
using Excalibur.Dispatch.LoadTests.Scenarios;

// Load test harness entry point
// Usage: dotnet run -- <scenario>
// Available scenarios: throughput, burst, concurrent, failure

var scenario = args.Length > 0 ? args[0].ToUpperInvariant() : "THROUGHPUT";

Console.WriteLine("=== Excalibur Load Test Harness ===");
Console.WriteLine($"Running scenario: {scenario}");
Console.WriteLine();

LoadTestBase test = scenario switch
{
	"THROUGHPUT" => new ThroughputLoadTest(),
	"BURST" => new BurstLoadTest(),
	"CONCURRENT" => new ConcurrentConsumersLoadTest(),
	"FAILURE" => new FailureScenarioLoadTest(),
	_ => throw new ArgumentException($"Unknown scenario: {scenario}. Available: throughput, burst, concurrent, failure")
};

test.Run();
