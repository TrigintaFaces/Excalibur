// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using NBomber.Contracts;
using NBomber.CSharp;

namespace Excalibur.Dispatch.LoadTests.Scenarios;

/// <summary>
/// Sustained throughput load test (E5-T6).
/// Tests sustained message throughput over extended duration (default 1 hour).
/// </summary>
public class ThroughputLoadTest : LoadTestBase
{
	protected override string ScenarioName => "sustained-throughput";
	protected override string ScenarioDescription => "Tests sustained message dispatch throughput over 1 hour";

	protected override ScenarioProps CreateScenario()
	{
		var client = new DispatchLoadTestClient(Configuration, Logger);

		return Scenario.Create(ScenarioName, async context =>
		{
			var result = await client.DispatchAsync();

			return result.Success
				? Response.Ok(sizeBytes: 256)
				: Response.Fail(statusCode: "DISPATCH_FAILED");
		})
		.WithInit(context =>
		{
			Logger.Information("Initializing sustained throughput test - Duration: {Duration}s, Rate: {Rate}/s",
				TestConfig.DurationSeconds,
				TestConfig.TargetRate);
			return Task.CompletedTask;
		})
		.WithClean(async context =>
		{
			await client.DisposeAsync();
			Logger.Information("Sustained throughput test completed");
		})
		.WithLoadSimulations(new[]
		{
			Simulation.Inject(
				rate: TestConfig.TargetRate,
				interval: TimeSpan.FromSeconds(1),
				during: TimeSpan.FromSeconds(TestConfig.DurationSeconds))
		});
	}
}

/// <summary>
/// Burst traffic load test (E5-T7).
/// Tests 10x traffic spike handling.
/// </summary>
public class BurstLoadTest : LoadTestBase
{
	protected override string ScenarioName => "burst-traffic";
	protected override string ScenarioDescription => "Tests system response to 10x traffic spike";

	protected override ScenarioProps CreateScenario()
	{
		var client = new DispatchLoadTestClient(Configuration, Logger);

		return Scenario.Create(ScenarioName, async context =>
		{
			var result = await client.DispatchAsync();

			return result.Success
				? Response.Ok(sizeBytes: 256)
				: Response.Fail(statusCode: "DISPATCH_FAILED");
		})
		.WithInit(context =>
		{
			Logger.Information("Initializing burst traffic test - Spike multiplier: {Multiplier}x",
				TestConfig.SpikeMultiplier);
			return Task.CompletedTask;
		})
		.WithClean(async context =>
		{
			await client.DisposeAsync();
		})
		.WithLoadSimulations(CreateLoadSimulation());
	}
}

/// <summary>
/// Concurrent consumers load test (E5-T8).
/// Tests multiple concurrent message consumers.
/// </summary>
public class ConcurrentConsumersLoadTest : LoadTestBase
{
	protected override string ScenarioName => "concurrent-consumers";
	protected override string ScenarioDescription => "Tests multiple concurrent message consumers";

	protected override ScenarioProps CreateScenario()
	{
		var client = new DispatchLoadTestClient(Configuration, Logger);

		return Scenario.Create(ScenarioName, async context =>
		{
			var result = await client.ConsumeAsync();

			return result.Success
				? Response.Ok(sizeBytes: 512)
				: Response.Fail(statusCode: "CONSUME_FAILED");
		})
		.WithInit(context =>
		{
			Logger.Information("Initializing concurrent consumers test - Concurrent users: {Users}",
				TestConfig.ConcurrentUsers);
			return Task.CompletedTask;
		})
		.WithClean(async context =>
		{
			await client.DisposeAsync();
		})
		.WithLoadSimulations(new[]
		{
			Simulation.KeepConstant(
				copies: TestConfig.ConcurrentUsers,
				during: TimeSpan.FromSeconds(TestConfig.DurationSeconds))
		});
	}
}

/// <summary>
/// Failure scenario load test (E5-T9).
/// Tests system resilience under failure conditions.
/// </summary>
public class FailureScenarioLoadTest : LoadTestBase
{
	protected override string ScenarioName => "failure-scenarios";
	protected override string ScenarioDescription => "Tests system resilience under failure conditions";

	protected override ScenarioProps CreateScenario()
	{
		var client = new DispatchLoadTestClient(Configuration, Logger);

		return Scenario.Create(ScenarioName, async context =>
		{
			// Simulate random failures (10%)
			if (Random.Shared.Next(100) < 10)
			{
				return Response.Fail(statusCode: "SIMULATED_FAILURE");
			}

			var result = await client.DispatchAsync();

			return result.Success
				? Response.Ok(sizeBytes: 256)
				: Response.Fail(statusCode: "DISPATCH_FAILED");
		})
		.WithInit(context =>
		{
			Logger.Information("Initializing failure scenario test - 10% simulated failure rate");
			return Task.CompletedTask;
		})
		.WithClean(async context =>
		{
			await client.DisposeAsync();
		})
		.WithLoadSimulations(CreateLoadSimulation());
	}
}
