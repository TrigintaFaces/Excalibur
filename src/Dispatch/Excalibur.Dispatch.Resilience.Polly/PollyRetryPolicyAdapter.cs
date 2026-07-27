// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Messaging;
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
					// FullJitter maps to Polly's exponential backoff with jitter forced on — Polly v8 has no
					// distinct FullJitter member; UseJitter applies its AWS-style jitter on the exponential base.
					BackoffStrategy.FullJitter => DelayBackoffType.Exponential,
					_ => DelayBackoffType.Exponential,
				},
				UseJitter = _options.UseJitter || _options.BackoffStrategy == BackoffStrategy.FullJitter,
				// The floor is composed with AND, so a consumer predicate can only ever NARROW what is retried,
				// never widen it. Written the other way round -- consulting ShouldRetry first, or defaulting to
				// true and letting the predicate opt out -- a caller who supplies no predicate (the default)
				// retries a permanently-failing operation forever, and a caller who supplies a permissive one
				// can re-enable it deliberately. Neither is a choice worth offering.
				ShouldHandle = new PredicateBuilder()
					.Handle<Exception>(ex => !IsNeverRetryable(ex) && (_options.ShouldRetry?.Invoke(ex) ?? true)),
				OnRetry = args =>
				{
					LogRetryAttempt(args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message,
						args.Outcome.Exception);
					return ValueTask.CompletedTask;
				},
			})
			.Build();
	}

	/// <summary>
	/// Returns whether an exception represents a failure that cannot succeed on a later attempt, regardless of
	/// how the caller has configured retries.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A retry is a bet that the same operation may succeed later. That bet is sound for a timeout, a
	/// transient fault, or a concurrency conflict. It is unsound for a tenant-isolation violation: the record
	/// belongs to another tenant, that does not change between attempts, and every retry fails identically
	/// while consuming the caller's retry budget and delaying the failure they need to see.
	/// </para>
	/// <para>
	/// This is deliberately a floor rather than a policy. It names only failures whose permanence is a
	/// property of the exception's own contract, so widening it is a design decision rather than a tuning
	/// one — a caller who wants to retry less can still say so through <c>ShouldRetry</c>.
	/// </para>
	/// </remarks>
	private static bool IsNeverRetryable(Exception exception) =>
		exception is TenantIsolationViolationException;

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
