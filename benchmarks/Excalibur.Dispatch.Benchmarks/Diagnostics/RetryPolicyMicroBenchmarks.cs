// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using MsOptions = Microsoft.Extensions.Options.Options;
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
		var options = new RetryOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			MaxDelay = TimeSpan.FromMilliseconds(1),
			UseJitter = false,
		};

		// RetryOptions ships a non-empty default deny-list (including InvalidOperationException, the
		// exception this benchmark throws) so the FilterMode cases below can isolate exactly the code
		// path each one names -- starting from RetryPolicyOptions' empty-by-default lists.
		options.NonRetryableExceptions.Clear();

		switch (FilterMode)
		{
			case RetryFilterMode.RetriableOnly:
				_ = options.RetryableExceptions.Add(typeof(InvalidOperationException));
				break;
			case RetryFilterMode.NonRetriableSet:
				_ = options.NonRetryableExceptions.Add(typeof(NotSupportedException));
				break;
		}

		var delay = DelayMode switch
		{
			RetryDelayMode.ZeroDelay => TimeSpan.Zero,
			RetryDelayMode.FixedDelay1Ms => TimeSpan.FromMilliseconds(1),
			_ => throw new ArgumentOutOfRangeException(nameof(DelayMode), DelayMode, "Unsupported retry delay mode."),
		};

		// The in-box duplicate retry policy was deleted; Polly is the framework's retry implementation,
		// so this measures what a consumer actually runs. Polly owns the delay, so the parameterised delay
		// mode is expressed through the options rather than through a backoff calculator.
		var logger = LoggingMode == RetryLoggingMode.Enabled
			? (ILogger<PollyRetryPolicyAdapter>)new EnabledNoOpLogger<PollyRetryPolicyAdapter>()
			: NullLogger<PollyRetryPolicyAdapter>.Instance;

		_policy = new PollyRetryPolicyAdapter(
			MsOptions.Create(new PollyRetryOptions
			{
				MaxRetryAttempts = options.MaxRetryAttempts,
				BaseDelay = delay,
				MaxDelay = delay,
				UseJitter = false,
			}),
			logger);
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
