// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Transport;

using Microsoft.Extensions.Options;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Builds the retry and circuit-breaker pipeline every Elasticsearch call runs through.
/// </summary>
/// <remarks>
/// <para>
/// Retry ladders and breaker state machines are solved problems, and the versions written here were
/// re-solving them: an attempt loop, a delay calculation, a half-open probe, and a failure counter,
/// all maintained by hand beside a library the solution already ships in five other packages.
/// </para>
/// <para>
/// Order matters, and it is the order the strategies are added: the first added is outermost, so
/// retry wraps the breaker. Every attempt therefore passes through the breaker and counts as its own
/// outcome, which is what lets a run of failures open the circuit; and once open, the breaker
/// rejects with an exception the retry does not treat as transient, so a broken circuit stops the
/// calls rather than being retried against.
/// </para>
/// </remarks>
internal sealed class ElasticsearchResiliencePipeline
{
	private readonly ResiliencePipeline _pipeline;

	public ElasticsearchResiliencePipeline(IOptions<ElasticsearchResilienceOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		var settings = options.Value;

		var builder = new ResiliencePipelineBuilder();

		// MaxAttempts of 1 means the call is made once and not retried. Polly rejects a retry
		// strategy configured for zero retries, so that case adds no strategy at all rather than
		// constructing one it will refuse.
		if (settings.Retry.Enabled && settings.Retry.MaxAttempts > 1)
		{
			_ = builder.AddRetry(new RetryStrategyOptions
			{
				// Polly counts RETRIES; the configured value counts ATTEMPTS, so the initial try is
				// subtracted. Setting them equal would quietly give every consumer one extra call.
				MaxRetryAttempts = settings.Retry.MaxAttempts - 1,
				Delay = settings.Retry.BaseDelay,
				MaxDelay = settings.Retry.MaxDelay,
				BackoffType = settings.Retry.UseExponentialBackoff
					? DelayBackoffType.Exponential
					: DelayBackoffType.Constant,
				UseJitter = settings.Retry.JitterFactor > 0,
				ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
			});
		}

		if (settings.CircuitBreaker.Enabled)
		{
			StateProvider = new CircuitBreakerStateProvider();

			_ = builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
			{
				FailureRatio = settings.CircuitBreaker.FailureRateThreshold,
				MinimumThroughput = settings.CircuitBreaker.MinimumThroughput,
				SamplingDuration = settings.CircuitBreaker.SamplingDuration,
				BreakDuration = settings.CircuitBreaker.BreakDuration,
				// The breaker counts every failure, which is deliberately WIDER than the retry
				// predicate. They answer different questions: retry asks "is this worth trying
				// again", the breaker asks "is this dependency unhealthy". Sharing one predicate
				// meant a connection refusal -- a TransportException carrying no HTTP status, so
				// not transient by the retry test -- never reached the circuit, and the breaker
				// could not open on the most basic failure there is. Polly's own default for a
				// circuit breaker is every exception.
				ShouldHandle = new PredicateBuilder().Handle<Exception>(),
				StateProvider = StateProvider,
			});
		}

		_pipeline = builder.Build();
	}

	/// <summary>
	/// Gets the breaker's state, or <see langword="null"/> when the breaker is disabled.
	/// </summary>
	public CircuitBreakerStateProvider? StateProvider { get; }

	public ValueTask<T> ExecuteAsync<T>(
		Func<CancellationToken, ValueTask<T>> operation,
		CancellationToken cancellationToken) =>
		_pipeline.ExecuteAsync(operation, cancellationToken);

	/// <summary>
	/// Decides which failures are worth trying again. Unchanged from the hand-written policy: a
	/// widened predicate here would retry genuine rejections, and a narrowed one would give up on
	/// failures that do pass.
	/// </summary>
	/// <remarks>
	/// An <see cref="ElasticsearchInvalidResponseException" /> is judged by the SAME status-code rule as
	/// a <see cref="TransportException" /> -- <see cref="IsRetriableStatusCode" /> is the one place that
	/// rule lives, so the two branches below cannot drift apart. Deliberately NOT
	/// <c>InvalidOperationException =&gt; true</c>: that would also make the unrelated "circuit breaker
	/// is open" signal retriable, which is the opposite of what it means.
	/// </remarks>
	internal static bool IsTransient(Exception exception) =>
		exception switch
		{
			HttpRequestException => true,
			TaskCanceledException => true,
			TimeoutException => true,
			TransportException te => IsRetriableStatusCode(te.ApiCallDetails?.HttpStatusCode),
			ElasticsearchInvalidResponseException ir => IsRetriableStatusCode(ir.HttpStatusCode),
			_ => false,
		};

	/// <summary>
	/// The single place the transient HTTP status codes are enumerated, so <see cref="IsTransient" />'s
	/// two exception-carrying-a-status-code branches cannot list them differently.
	/// </summary>
	private static bool IsRetriableStatusCode(int? statusCode) =>
		statusCode is 429 or 502 or 503 or 504;
}
