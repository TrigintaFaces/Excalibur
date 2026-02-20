// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Isolates retry policy overhead across logging, exception filters, and delay provider modes.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class RetryPolicyMicroBenchmarks
{
	private IRetryPolicy? _policy;

	[ParamsAllValues]
	public RetryLoggingMode LoggingMode { get; set; }

	[ParamsAllValues]
	public RetryFilterMode FilterMode { get; set; }

	[ParamsAllValues]
	public RetryDelayMode DelayMode { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			MaxDelay = TimeSpan.FromMilliseconds(1),
			EnableJitter = false,
		};

		switch (FilterMode)
		{
			case RetryFilterMode.RetriableOnly:
				_ = options.RetriableExceptions.Add(typeof(InvalidOperationException));
				break;
			case RetryFilterMode.NonRetriableSet:
				_ = options.NonRetriableExceptions.Add(typeof(NotSupportedException));
				break;
		}

		var backoff = DelayMode switch
		{
			RetryDelayMode.ZeroDelay => new FixedBackoffCalculator(TimeSpan.Zero),
			RetryDelayMode.FixedDelay1Ms => new FixedBackoffCalculator(TimeSpan.FromMilliseconds(1)),
			_ => throw new ArgumentOutOfRangeException(nameof(DelayMode), DelayMode, "Unsupported retry delay mode."),
		};

		var logger = LoggingMode == RetryLoggingMode.Enabled
			? (ILogger<DefaultRetryPolicy>)new EnabledNoOpLogger<DefaultRetryPolicy>()
			: NullLogger<DefaultRetryPolicy>.Instance;

		_policy = new DefaultRetryPolicy(options, backoff, logger);
	}

	[Benchmark(Baseline = true, Description = "Retry success after transient failures")]
	public async Task<int> RetrySuccessThirdAttempt()
	{
		var attempts = 0;
		return await _policy!.ExecuteAsync(
			ct =>
			{
				attempts++;
				if (attempts < 3)
				{
					throw new InvalidOperationException("retry micro benchmark transient");
				}

				ct.ThrowIfCancellationRequested();
				return Task.FromResult(attempts);
			},
			CancellationToken.None).ConfigureAwait(false);
	}

	[Benchmark(Description = "Retry exhausted failure path")]
	public async Task<int> RetryExhaustedFailure()
	{
		var attempts = 0;
		try
		{
			_ = await _policy!.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					ct.ThrowIfCancellationRequested();
					throw new InvalidOperationException("retry micro benchmark persistent");
				},
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected benchmark path.
		}

		return attempts;
	}

	private sealed class EnabledNoOpLogger<T> : ILogger<T>
	{
		public IDisposable BeginScope<TState>(TState state)
			where TState : notnull => NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			// Intentionally empty: enable logging code paths without sink overhead.
		}

		private sealed class NullScope : IDisposable
		{
			internal static readonly NullScope Instance = new();

			public void Dispose()
			{
			}
		}
	}
}

public enum RetryLoggingMode
{
	Disabled,
	Enabled,
}

public enum RetryFilterMode
{
	None,
	RetriableOnly,
	NonRetriableSet,
}

public enum RetryDelayMode
{
	ZeroDelay,
	FixedDelay1Ms,
}
