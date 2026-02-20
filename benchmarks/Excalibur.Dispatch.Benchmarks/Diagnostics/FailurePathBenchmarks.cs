// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Quantifies retry, failure, poison/dead-letter, and cancellation overhead.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class FailurePathBenchmarks
{
	private DiagnosticBenchmarkFixture? _fixture;
	private IRetryPolicy? _retryPolicy;
	private IRetryPolicy? _retryPolicyNoDelay;

	[GlobalSetup]
	public void GlobalSetup()
	{
		_fixture = new DiagnosticBenchmarkFixture(includeFaultingHandler: true, includeCancelableHandler: true);
		_retryPolicy = new DefaultRetryPolicy(new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			MaxDelay = TimeSpan.FromMilliseconds(5),
			EnableJitter = false,
		});
		_retryPolicyNoDelay = new DefaultRetryPolicy(new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromTicks(1),
			MaxDelay = TimeSpan.FromTicks(1),
			EnableJitter = false,
		}, new FixedBackoffCalculator(TimeSpan.Zero));
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_fixture?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Retry: succeeds on third attempt")]
	public async Task<int> RetrySucceedsAfterTransientFailure()
	{
		var attempts = 0;
		return await _retryPolicy!.ExecuteAsync(
			ct =>
			{
				attempts++;
				if (attempts < 3)
				{
					throw new InvalidOperationException("synthetic transient failure");
				}

				ct.ThrowIfCancellationRequested();
				return Task.FromResult(attempts);
			},
			CancellationToken.None).ConfigureAwait(false);
	}

	[Benchmark(Description = "Retry overhead: succeeds on third attempt (zero delay)")]
	public async Task<int> RetryOverheadSucceedsAfterTransientFailure()
	{
		var attempts = 0;
		return await _retryPolicyNoDelay!.ExecuteAsync(
			ct =>
			{
				attempts++;
				if (attempts < 3)
				{
					throw new InvalidOperationException("synthetic transient failure");
				}

				ct.ThrowIfCancellationRequested();
				return Task.FromResult(attempts);
			},
			CancellationToken.None).ConfigureAwait(false);
	}

	[Benchmark(Description = "Retry: exhausted failures")]
	public async Task<int> RetryExhaustedFailure()
	{
		var attempts = 0;
		try
		{
			_ = await _retryPolicy!.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					ct.ThrowIfCancellationRequested();
					throw new InvalidOperationException("persistent failure");
				},
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected path for benchmark.
		}

		return attempts;
	}

	[Benchmark(Description = "Retry overhead: exhausted failures (zero delay)")]
	public async Task<int> RetryOverheadExhaustedFailure()
	{
		var attempts = 0;
		try
		{
			_ = await _retryPolicyNoDelay!.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					ct.ThrowIfCancellationRequested();
					throw new InvalidOperationException("persistent failure");
				},
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected path for benchmark.
		}

		return attempts;
	}

	[Benchmark(Description = "Dispatch: faulting handler")]
	public async Task<bool> DispatchFaultingHandler()
	{
		try
		{
			_ = await _fixture!.Dispatcher
				.DispatchAsync(new FaultingCommand(42), _fixture.CreateContext(), CancellationToken.None)
				.ConfigureAwait(false);
			return false;
		}
		catch (InvalidOperationException)
		{
			return true;
		}
	}

	[Benchmark(Description = "Dispatch: cancellation in-flight")]
	public async Task<bool> DispatchCanceledCommand()
	{
		using var cts = new CancellationTokenSource();
		var dispatchTask = _fixture!.Dispatcher
			.DispatchAsync(new CancelableCommand(42, DelayMs: 10), _fixture.CreateContext(), cts.Token);
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

	[Benchmark(Description = "Dispatch: pre-canceled token")]
	public async Task<bool> DispatchPreCanceledCommand()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		try
		{
			_ = await _fixture!.Dispatcher
				.DispatchAsync(new CancelableCommand(42, DelayMs: 10), _fixture.CreateContext(), cts.Token)
				.ConfigureAwait(false);
			return false;
		}
		catch (OperationCanceledException)
		{
			return true;
		}
	}

	[Benchmark(Description = "Dead-letter: store message")]
	public async Task<long> DeadLetterStoreMessage()
	{
		var deadLetterStore = CreateDeadLetterStore();
		var messageId = Guid.NewGuid().ToString("N");
		var deadLetter = new DeadLetterMessage
		{
			MessageId = messageId,
			MessageType = typeof(FaultingCommand).FullName ?? nameof(FaultingCommand),
			MessageBody = "{\"value\":42}",
			MessageMetadata = "{\"attempt\":3}",
			Reason = "poison-message",
			ExceptionDetails = "InvalidOperationException",
			ProcessingAttempts = 3,
			SourceSystem = "benchmark",
			CorrelationId = Guid.NewGuid().ToString("N"),
		};

		await deadLetterStore.StoreAsync(deadLetter, CancellationToken.None).ConfigureAwait(false);
		return await deadLetterStore.GetCountAsync(CancellationToken.None).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dead-letter: query + replay marker")]
	public async Task<int> DeadLetterQueryAndReplay()
	{
		var deadLetterStore = CreateDeadLetterStore();
		var messageId = Guid.NewGuid().ToString("N");
		var deadLetter = new DeadLetterMessage
		{
			MessageId = messageId,
			MessageType = typeof(FaultingCommand).FullName ?? nameof(FaultingCommand),
			MessageBody = "{\"value\":42}",
			MessageMetadata = "{\"attempt\":3}",
			Reason = "retry-exhausted",
			ExceptionDetails = "InvalidOperationException",
			ProcessingAttempts = 3,
			SourceSystem = "benchmark",
			CorrelationId = Guid.NewGuid().ToString("N"),
		};

		await deadLetterStore.StoreAsync(deadLetter, CancellationToken.None).ConfigureAwait(false);
		await deadLetterStore.MarkAsReplayedAsync(messageId, CancellationToken.None).ConfigureAwait(false);

		var results = await deadLetterStore.GetMessagesAsync(new DeadLetterFilter
		{
			MessageType = deadLetter.MessageType,
			MaxResults = 25,
		}, CancellationToken.None).ConfigureAwait(false);

		return results.Count();
	}

	private static InMemoryDeadLetterStore CreateDeadLetterStore()
	{
		return new InMemoryDeadLetterStore(NullLogger<InMemoryDeadLetterStore>.Instance);
	}
}
