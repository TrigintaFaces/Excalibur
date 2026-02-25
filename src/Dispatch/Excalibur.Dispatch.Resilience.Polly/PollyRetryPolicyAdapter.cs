// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;
using Polly.Retry;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Polly-based retry policy implementation that wraps Polly's resilience pipeline
/// and implements the core <see cref="IRetryPolicy"/> interface.
/// </summary>
/// <remarks>
/// <para>
/// This adapter bridges Polly's powerful retry capabilities with Dispatch's zero-dependency
/// retry abstraction. Use this when you want Polly's advanced features like jitter strategies,
/// circuit breaker integration, or custom should-handle predicates.
/// </para>
/// <para>
/// For consumers who don't need Polly's advanced features, the core package provides
/// <see cref="DefaultRetryPolicy"/> which uses <see cref="IBackoffCalculator"/> for delays.
/// </para>
/// </remarks>
public sealed partial class PollyRetryPolicyAdapter : IRetryPolicy
{
	private readonly ResiliencePipeline _pipeline;
	private readonly ILogger _logger;
	private readonly RetryOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyRetryPolicyAdapter" /> class.
	/// </summary>
	/// <param name="options"> Retry configuration options. </param>
	/// <param name="logger"> Optional logger instance. </param>
	public PollyRetryPolicyAdapter(RetryOptions options, ILogger? logger = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger.Instance;

		// Create Polly resilience pipeline with retry strategy
		_pipeline = new ResiliencePipelineBuilder()
			.AddRetry(new RetryStrategyOptions
			{
				MaxRetryAttempts = _options.MaxRetries,
				Delay = _options.BaseDelay,
				BackoffType = _options.BackoffStrategy switch
				{
					BackoffStrategy.Linear => DelayBackoffType.Linear,
					BackoffStrategy.Exponential => DelayBackoffType.Exponential,
					BackoffStrategy.Fixed => DelayBackoffType.Constant,
					_ => DelayBackoffType.Exponential,
				},
				UseJitter = _options.UseJitter,
				ShouldHandle = new PredicateBuilder()
					.Handle<Exception>(ex => _options.ShouldRetry?.Invoke(ex) ?? true),
				OnRetry = args =>
				{
					LogRetryAttempt(args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message,
						args.Outcome.Exception);
					return ValueTask.CompletedTask;
				},
			})
			.Build();
	}

	/// <inheritdoc />
	/// <remarks>
	/// Executes the action through Polly's resilience pipeline with full retry,
	/// circuit breaker, and jitter support based on <see cref="RetryOptions"/> configuration.
	/// </remarks>
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		return await _pipeline.ExecuteAsync(
			async ct => await action(ct).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Executes the action through Polly's resilience pipeline with full retry,
	/// circuit breaker, and jitter support based on <see cref="RetryOptions"/> configuration.
	/// </remarks>
	public async Task ExecuteAsync(
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		await _pipeline.ExecuteAsync(
			async ct => await action(ct).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.RetryAttemptStarted, LogLevel.Warning,
		"Retry attempt {AttemptNumber} for operation after {Delay}ms. Exception: {Exception}")]
	private partial void LogRetryAttempt(int attemptNumber, double delay, string? exception, Exception? ex);
}
