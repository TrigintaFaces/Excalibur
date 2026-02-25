// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for pipeline execution with various middleware configurations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class PipelineBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private TestMessage _message = null!;
	private MessageContext _context = null!;

	// Middleware chains of different sizes
	private Func<Task<IMessageResult>> _noMiddleware = null!;
	private Func<Task<IMessageResult>> _singleMiddleware = null!;
	private Func<Task<IMessageResult>> _threeMiddleware = null!;
	private Func<Task<IMessageResult>> _fiveMiddleware = null!;
	private Func<Task<IMessageResult>> _tenMiddleware = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_serviceProvider = services.BuildServiceProvider();

		_message = new TestMessage { Id = Guid.NewGuid(), Data = "BenchmarkData" };
		_context = new MessageContext(_message, _serviceProvider);

		// Build various pipeline configurations
		_noMiddleware = BuildPipeline(0);
		_singleMiddleware = BuildPipeline(1);
		_threeMiddleware = BuildPipeline(3);
		_fiveMiddleware = BuildPipeline(5);
		_tenMiddleware = BuildPipeline(10);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	private Func<Task<IMessageResult>> BuildPipeline(int middlewareCount)
	{
		Func<Task<IMessageResult>> handler = () => Task.FromResult(MessageResult.Success());

		for (var i = 0; i < middlewareCount; i++)
		{
			var next = handler;
			handler = () =>
			{
				// Simulate minimal middleware work
				return next();
			};
		}

		return handler;
	}

	[Benchmark(Baseline = true)]
	public Task<IMessageResult> Pipeline_NoMiddleware()
	{
		return _noMiddleware();
	}

	[Benchmark]
	public Task<IMessageResult> Pipeline_SingleMiddleware()
	{
		return _singleMiddleware();
	}

	[Benchmark]
	public Task<IMessageResult> Pipeline_ThreeMiddleware()
	{
		return _threeMiddleware();
	}

	[Benchmark]
	public Task<IMessageResult> Pipeline_FiveMiddleware()
	{
		return _fiveMiddleware();
	}

	[Benchmark]
	public Task<IMessageResult> Pipeline_TenMiddleware()
	{
		return _tenMiddleware();
	}

	[Benchmark]
	public async Task<IMessageResult> Pipeline_AsyncMiddleware()
	{
		// Simulate async middleware chain
		await Task.Yield();
		return MessageResult.Success();
	}

	[Benchmark]
	public IMessageResult CreateSuccessResult()
	{
		return MessageResult.Success();
	}

	[Benchmark]
	public IMessageResult CreateFailedResult()
	{
		return MessageResult.Failed("Test error");
	}

	[Benchmark]
	public IMessageResult CreateResultWithData()
	{
		return MessageResult.Success(new { Id = 1, Name = "Test" });
	}

	private sealed record TestMessage : IDispatchMessage
	{
		public Guid Id { get; init; }
		public string Data { get; init; } = string.Empty;
	}
}
