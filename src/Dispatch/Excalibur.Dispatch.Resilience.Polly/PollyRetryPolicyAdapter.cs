// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
/// the in-box policies, which use <see cref="IBackoffCalculator"/> for delays.
/// </para>
/// </remarks>
public sealed partial class PollyRetryPolicyAdapter : IRetryPolicy
{
	private readonly ResiliencePipeline _pipeline;
	private readonly ILogger _logger;
	private readonly PollyRetryOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyRetryPolicyAdapter" /> class.
	/// </summary>
	/// <param name="options"> Retry configuration options. </param>
	/// <param name="logger"> Optional logger instance. </param>
	public PollyRetryPolicyAdapter(IOptions<PollyRetryOptions> options, ILogger? logger = null)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger.Instance;

		// Zero is a supported framework setting: execute once without a retry strategy.
		// Polly's retry strategy itself requires at least one retry.
		_pipeline = _options.MaxRetryAttempts == 0 ? ResiliencePipeline.Empty : new ResiliencePipelineBuilder()
			.AddRetry(new RetryStrategyOptions
			{
				MaxRetryAttempts = _options.MaxRetryAttempts,
				Delay = _options.BaseDelay,
				// Exhaustive, no default arm -- see the matching mapping in
				// DispatchBuilderResilienceExtensions for why a `_ =>` here is a silent-substitution bug
				// rather than a safe fallback.
				BackoffType = _options.BackoffStrategy switch
				{
					BackoffStrategy.Constant => DelayBackoffType.Constant,
					BackoffStrategy.Linear => DelayBackoffType.Linear,
					BackoffStrategy.Exponential => DelayBackoffType.Exponential,
					_ => throw new InvalidOperationException(
						Resources.PollyRetryPolicyAdapter_UnknownBackoffStrategy),
				},
				UseJitter = _options.UseJitter,

				// The cap a consumer configures. Without this, an exponential strategy grows unbounded:
				// PollyRetryOptions.MaxDelay defaults to one minute, and every consumer of this adapter
				// was silently getting no cap at all. The option was only ever read by the duplicate
				// concrete retry implementation, which nothing resolved, so the knob looked honoured.
				MaxDelay = _options.MaxDelay,
				// Retain the tenant-isolation floor. Otherwise use Polly's cancellation exclusion by default;
				// an explicit predicate can select attempt-local cancellation when the caller is still active.
				ShouldHandle = new PredicateBuilder()
					.Handle<Exception>(ex => !IsNeverRetryable(ex) &&
						(_options.ShouldRetry?.Invoke(ex) ?? (ex is not OperationCanceledException))),
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
	/// circuit breaker, and jitter support based on <see cref="PollyRetryOptions"/> configuration.
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
	/// circuit breaker, and jitter support based on <see cref="PollyRetryOptions"/> configuration.
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
