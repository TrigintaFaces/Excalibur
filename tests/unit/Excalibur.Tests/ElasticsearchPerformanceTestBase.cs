// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

using Elastic.Clients.Elasticsearch;

namespace Excalibur.Tests;

/// <summary>
///     Base class for Elasticsearch performance tests using BenchmarkDotNet.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[Config(typeof(ElasticsearchBenchmarkConfig))]
public abstract class ElasticsearchPerformanceTestBase
{
	private IServiceProvider? _serviceProvider;
	private ElasticsearchClient? _client;

	/// <summary>
	///     Gets or sets the number of operations for benchmarking.
	/// </summary>
	[Params(10, 100, 1000)]
	public int OperationCount { get; set; }

	/// <summary>
	///     Gets or sets the batch size for bulk operations.
	/// </summary>
	[Params(50, 100, 500)]
	public int BatchSize { get; set; }

	/// <summary>
	///     Gets the Elasticsearch client for benchmarking.
	/// </summary>
	protected ElasticsearchClient Client => _client ?? throw new InvalidOperationException("Client not initialized");

	/// <summary>
	///     Gets the service provider for benchmarking.
	/// </summary>
	protected IServiceProvider ServiceProvider =>
		_serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

	/// <summary>
	///     Global setup for the benchmark.
	/// </summary>
	[GlobalSetup]
	public virtual void GlobalSetup()
	{
		var services = new ServiceCollection();
		ConfigureBenchmarkServices(services);

		_serviceProvider = services.BuildServiceProvider();
		_client = _serviceProvider.GetRequiredService<ElasticsearchClient>();

		// Perform any additional setup
		OnGlobalSetup();
	}

	/// <summary>
	///     Global cleanup for the benchmark.
	/// </summary>
	[GlobalCleanup]
	public virtual void GlobalCleanup()
	{
		OnGlobalCleanup();

		(_serviceProvider as IDisposable)?.Dispose();
	}

	/// <summary>
	///     Iteration setup for the benchmark.
	/// </summary>
	[IterationSetup]
	public virtual void IterationSetup() => OnIterationSetup();

	/// <summary>
	///     Iteration cleanup for the benchmark.
	/// </summary>
	[IterationCleanup]
	public virtual void IterationCleanup() => OnIterationCleanup();

	/// <summary>
	///     Configures services for the benchmark.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	protected virtual void ConfigureBenchmarkServices(IServiceCollection services)
	{
		// Add logging
		_ = services.AddLogging(static builder => builder.SetMinimumLevel(LogLevel.Warning));

		// Add Elasticsearch client
		// Note: This should be configured with actual connection settings
		_ = services.AddSingleton(static sp =>
		{
			var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"))
				.DefaultIndex("benchmark-index");

			return new ElasticsearchClient(settings);
		});
	}

	/// <summary>
	///     Called during global setup.
	/// </summary>
	protected virtual void OnGlobalSetup()
	{
	}

	/// <summary>
	///     Called during global cleanup.
	/// </summary>
	protected virtual void OnGlobalCleanup()
	{
	}

	/// <summary>
	///     Called during iteration setup.
	/// </summary>
	protected virtual void OnIterationSetup()
	{
	}

	/// <summary>
	///     Called during iteration cleanup.
	/// </summary>
	protected virtual void OnIterationCleanup()
	{
	}

	/// <summary>
	///     Measures the execution time of an action.
	/// </summary>
	/// <param name="action"> The action to measure. </param>
	/// <returns> The elapsed time. </returns>
	protected TimeSpan MeasureTime(Action action)
	{
		ArgumentNullException.ThrowIfNull(action);
		var sw = Stopwatch.StartNew();
		action();
		sw.Stop();
		return sw.Elapsed;
	}

	/// <summary>
	///     Measures the execution time of an async action.
	/// </summary>
	/// <param name="action"> The async action to measure. </param>
	/// <returns> The elapsed time. </returns>
	protected async Task<TimeSpan> MeasureTimeAsync(Func<Task> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		var sw = Stopwatch.StartNew();
		await action().ConfigureAwait(false);
		sw.Stop();
		return sw.Elapsed;
	}
}

/// <summary>
///     Configuration for Elasticsearch benchmarks.
/// </summary>
public class ElasticsearchBenchmarkConfig : ManualConfig
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ElasticsearchBenchmarkConfig" /> class.
	/// </summary>
	public ElasticsearchBenchmarkConfig()
	{
		_ = WithOptions(ConfigOptions.DisableOptimizationsValidator);

		_ = AddDiagnoser(MemoryDiagnoser.Default);
		_ = AddDiagnoser(ThreadingDiagnoser.Default);

		_ = AddJob(BenchmarkDotNet.Jobs.Job.Default
			.WithWarmupCount(3)
			.WithIterationCount(10)
			.WithInvocationCount(1)
			.WithUnrollFactor(1));
	}
}

/// <summary>
///     Helper class for running performance tests.
/// </summary>
public static class PerformanceTestRunner
{
	/// <summary>
	///     Runs performance benchmarks for the specified type.
	/// </summary>
	/// <typeparam name="TBenchmark"> The benchmark type. </typeparam>
	public static void RunBenchmarks<TBenchmark>() where TBenchmark : class
	{
		var summary = BenchmarkRunner.Run<TBenchmark>();

		// Print results
		Console.WriteLine($"Total benchmarks: {summary.BenchmarksCases.Length}");
		Console.WriteLine($"Total time: {summary.TotalTime}");

		foreach (var report in summary.Reports)
		{
			Console.WriteLine($"Benchmark: {report.BenchmarkCase.DisplayInfo}");
			Console.WriteLine($" Mean: {report.ResultStatistics?.Mean ?? 0:N2} ns");
			Console.WriteLine($" Error: {report.ResultStatistics?.StandardError ?? 0:N2} ns");
			Console.WriteLine($" StdDev: {report.ResultStatistics?.StandardDeviation ?? 0:N2} ns");

			if (report.Metrics != null)
			{
				foreach (var metric in report.Metrics)
				{
					Console.WriteLine($" {metric.Key}: {metric.Value}");
				}
			}
		}
	}

	/// <summary>
	///     Runs a simple performance test without BenchmarkDotNet.
	/// </summary>
	/// <param name="name"> The test name. </param>
	/// <param name="action"> The action to test. </param>
	/// <param name="iterations"> The number of iterations. </param>
	public static void RunSimpleTest(string name, Action action, int iterations = 100)
	{
		ArgumentNullException.ThrowIfNull(action);
		Console.WriteLine($"Running: {name}");

		// Warm up
		for (var i = 0; i < 10; i++)
		{
			action();
		}

		// Measure
		var times = new List<double>(iterations);
		var sw = new Stopwatch();

		for (var i = 0; i < iterations; i++)
		{
			sw.Restart();
			action();
			sw.Stop();
			times.Add(sw.Elapsed.TotalMilliseconds);
		}

		// Calculate statistics
		times.Sort();
		var mean = times.Average();
		var median = times[iterations / 2];
		var min = times[0];
		var max = times[^1];
		var p95 = times[(int)(iterations * 0.95)];
		var p99 = times[(int)(iterations * 0.99)];

		Console.WriteLine($" Iterations: {iterations}");
		Console.WriteLine($" Mean: {mean:F3} ms");
		Console.WriteLine($" Median: {median:F3} ms");
		Console.WriteLine($" Min: {min:F3} ms");
		Console.WriteLine($" Max: {max:F3} ms");
		Console.WriteLine($" P95: {p95:F3} ms");
		Console.WriteLine($" P99: {p99:F3} ms");
		Console.WriteLine();
	}

	/// <summary>
	///     Runs a simple async performance test without BenchmarkDotNet.
	/// </summary>
	/// <param name="name"> The test name. </param>
	/// <param name="action"> The async action to test. </param>
	/// <param name="iterations"> The number of iterations. </param>
	public static async Task RunSimpleTestAsync(string name, Func<Task> action, int iterations = 100)
	{
		ArgumentNullException.ThrowIfNull(action);
		Console.WriteLine($"Running: {name}");

		// Warm up
		for (var i = 0; i < 10; i++)
		{
			await action().ConfigureAwait(false);
		}

		// Measure
		var times = new List<double>(iterations);
		var sw = new Stopwatch();

		for (var i = 0; i < iterations; i++)
		{
			sw.Restart();
			await action().ConfigureAwait(false);
			sw.Stop();
			times.Add(sw.Elapsed.TotalMilliseconds);
		}

		// Calculate statistics
		times.Sort();
		var mean = times.Average();
		var median = times[iterations / 2];
		var min = times[0];
		var max = times[^1];
		var p95 = times[(int)(iterations * 0.95)];
		var p99 = times[(int)(iterations * 0.99)];

		Console.WriteLine($" Iterations: {iterations}");
		Console.WriteLine($" Mean: {mean:F3} ms");
		Console.WriteLine($" Median: {median:F3} ms");
		Console.WriteLine($" Min: {min:F3} ms");
		Console.WriteLine($" Max: {max:F3} ms");
		Console.WriteLine($" P95: {p95:F3} ms");
		Console.WriteLine($" P99: {p99:F3} ms");
		Console.WriteLine();
	}
}
