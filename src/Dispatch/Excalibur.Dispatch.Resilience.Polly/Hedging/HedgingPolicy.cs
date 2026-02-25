// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;
using Polly.Hedging;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Hedging policy that sends parallel requests after a configured delay,
/// returning the first successful result to reduce tail latency.
/// </summary>
/// <remarks>
/// <para>
/// Uses Polly v8's hedging strategy under the hood. Hedging is particularly useful
/// for read operations or idempotent writes where latency reduction is more important
/// than resource conservation.
/// </para>
/// </remarks>
public sealed partial class HedgingPolicy
{
	private readonly ResiliencePipeline<object> _pipeline;
	private readonly HedgingOptions _options;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="HedgingPolicy"/> class.
	/// </summary>
	/// <param name="options">The hedging options.</param>
	/// <param name="logger">Optional logger instance.</param>
	public HedgingPolicy(
		HedgingOptions options,
		ILogger<HedgingPolicy>? logger = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger<HedgingPolicy>.Instance;

		_pipeline = BuildPipeline();
	}

	/// <summary>
	/// Executes an operation with hedging protection.
	/// </summary>
	/// <typeparam name="TResult">The type of result returned by the operation.</typeparam>
	/// <param name="operation">The operation to execute.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>The result from the first successful attempt.</returns>
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken)
		where TResult : class
	{
		ArgumentNullException.ThrowIfNull(operation);

		var result = await _pipeline.ExecuteAsync(
			async ct =>
			{
				var value = await operation(ct).ConfigureAwait(false);
				return (object)value;
			},
			cancellationToken).ConfigureAwait(false);

		return (TResult)result;
	}

	/// <summary>
	/// Executes a void operation with hedging protection.
	/// </summary>
	/// <param name="operation">The operation to execute.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ExecuteAsync(
		Func<CancellationToken, Task> operation,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		await _pipeline.ExecuteAsync(
			async ct =>
			{
				await operation(ct).ConfigureAwait(false);
				return (object)0;
			},
			cancellationToken).ConfigureAwait(false);
	}

	private ResiliencePipeline<object> BuildPipeline()
	{
		var builder = new ResiliencePipelineBuilder<object>();

		_ = builder.AddHedging(new HedgingStrategyOptions<object>
		{
			MaxHedgedAttempts = _options.MaxHedgedAttempts,
			Delay = _options.Delay,
			ShouldHandle = new PredicateBuilder<object>()
				.Handle<Exception>(ex => _options.ShouldHedge?.Invoke(ex) ?? IsHedgeable(ex)),
			OnHedging = args =>
			{
				LogHedgingAttempt(
					args.AttemptNumber,
					_options.MaxHedgedAttempts,
					_options.Delay.TotalMilliseconds);
				return ValueTask.CompletedTask;
			},
		});

		return builder.Build();
	}

	private static bool IsHedgeable(Exception ex) =>
		ex switch
		{
			TimeoutException => true,
			TaskCanceledException => false,
			OperationCanceledException => false,
			ArgumentException => false,
			InvalidOperationException => false,
			_ => true,
		};

	[LoggerMessage(ResilienceEventId.HedgingAttemptLaunched, LogLevel.Information,
		"Hedging attempt {AttemptNumber} of {MaxAttempts} launched after {DelayMs}ms delay")]
	private partial void LogHedgingAttempt(int attemptNumber, int maxAttempts, double delayMs);
}
