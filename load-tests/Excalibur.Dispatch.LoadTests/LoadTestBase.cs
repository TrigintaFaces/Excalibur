// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.LoadTests.Reports;

using Microsoft.Extensions.Configuration;

using NBomber.Contracts;
using NBomber.CSharp;

using Serilog;

namespace Excalibur.Dispatch.LoadTests;

/// <summary>
/// Base class for all load tests providing common configuration and execution patterns.
/// </summary>
public abstract class LoadTestBase
{
	protected IConfiguration Configuration { get; }
	protected ILogger Logger { get; }
	protected LoadTestConfig TestConfig { get; }

	protected LoadTestBase()
	{
		Configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", optional: false)
			.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development"}.json", optional: true)
			.AddEnvironmentVariables()
			.Build();

		Logger = new LoggerConfiguration()
			.ReadFrom.Configuration(Configuration)
			.CreateLogger();

		TestConfig = Configuration.GetSection("LoadTest").Get<LoadTestConfig>() ?? new LoadTestConfig();
	}

	/// <summary>
	/// Gets the name of this load test scenario.
	/// </summary>
	protected abstract string ScenarioName { get; }

	/// <summary>
	/// Gets the description of what this test validates.
	/// </summary>
	protected abstract string ScenarioDescription { get; }

	/// <summary>
	/// Creates the NBomber scenario for this test.
	/// </summary>
	protected abstract ScenarioProps CreateScenario();

	/// <summary>
	/// Run the load test with the configured scenario.
	/// </summary>
	public virtual void Run()
	{
		var scenario = CreateScenario();
		var reportFolder = $"reports/{ScenarioName}";

		_ = NBomberRunner
			.RegisterScenarios(scenario)
			.WithReportFolder(reportFolder)
			.Run();

		// Generate enhanced HTML report
		GenerateEnhancedReport(reportFolder);
	}

	/// <summary>
	/// Generates an enhanced HTML report from NBomber output.
	/// </summary>
	protected virtual void GenerateEnhancedReport(string reportFolder)
	{
		try
		{
			var statsFile = Directory.GetFiles(reportFolder, "*stats.json", SearchOption.AllDirectories)
				.OrderByDescending(File.GetLastWriteTime)
				.FirstOrDefault();

			if (statsFile == null)
			{
				Logger.Warning("No stats.json found in {Folder}, skipping enhanced report", reportFolder);
				return;
			}

			var slaThresholds = Configuration.GetSection("LoadTest:Sla").Get<SlaThresholds>() ?? new SlaThresholds();
			var generator = new LoadTestReportGenerator(Logger, slaThresholds);

			var html = generator.GenerateReportAsync(statsFile, ScenarioName, ScenarioDescription).GetAwaiter().GetResult();
			var outputPath = Path.Combine(reportFolder, $"{ScenarioName}-enhanced-report.html");

			generator.SaveReportAsync(html, outputPath).GetAwaiter().GetResult();

			Logger.Information("Enhanced report generated at {Path}", outputPath);
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "Failed to generate enhanced report");
		}
	}

	/// <summary>
	/// Creates load simulation based on configuration.
	/// </summary>
	protected LoadSimulation[] CreateLoadSimulation()
	{
		return TestConfig.SimulationType switch
		{
			"RampUp" => new[]
			{
				Simulation.RampingInject(
					rate: TestConfig.TargetRate,
					interval: TimeSpan.FromSeconds(1),
					during: TimeSpan.FromSeconds(TestConfig.RampUpSeconds))
			},
			"Constant" => new[]
			{
				Simulation.Inject(
					rate: TestConfig.TargetRate,
					interval: TimeSpan.FromSeconds(1),
					during: TimeSpan.FromSeconds(TestConfig.DurationSeconds))
			},
			"Spike" => new[]
			{
				Simulation.Inject(
					rate: TestConfig.TargetRate,
					interval: TimeSpan.FromSeconds(1),
					during: TimeSpan.FromSeconds(TestConfig.DurationSeconds / 3)),
				Simulation.Inject(
					rate: TestConfig.TargetRate * TestConfig.SpikeMultiplier,
					interval: TimeSpan.FromSeconds(1),
					during: TimeSpan.FromSeconds(TestConfig.DurationSeconds / 3)),
				Simulation.Inject(
					rate: TestConfig.TargetRate,
					interval: TimeSpan.FromSeconds(1),
					during: TimeSpan.FromSeconds(TestConfig.DurationSeconds / 3))
			},
			_ => new[]
			{
				Simulation.KeepConstant(
					copies: TestConfig.ConcurrentUsers,
					during: TimeSpan.FromSeconds(TestConfig.DurationSeconds))
			}
		};
	}
}

/// <summary>
/// Configuration for load tests.
/// </summary>
public class LoadTestConfig
{
	public string SimulationType { get; set; } = "Constant";
	public int TargetRate { get; set; } = 100;
	public int DurationSeconds { get; set; } = 60;
	public int RampUpSeconds { get; set; } = 10;
	public int ConcurrentUsers { get; set; } = 10;
	public int SpikeMultiplier { get; set; } = 10;
	public int WarmUpSeconds { get; set; } = 5;
}
