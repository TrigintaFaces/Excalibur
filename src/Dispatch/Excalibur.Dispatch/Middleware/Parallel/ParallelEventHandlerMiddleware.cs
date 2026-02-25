// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.ParallelExecution;

/// <summary>
/// Middleware that enables parallel execution of event handlers when multiple handlers
/// are registered for the same event type.
/// </summary>
/// <remarks>
/// <para>
/// By default, Dispatch executes event handlers sequentially. This middleware enables
/// concurrent execution with configurable parallelism and failure strategies.
/// </para>
/// <para>
/// This middleware only applies to <see cref="MessageKinds.Event"/> messages. Actions
/// and documents always execute sequentially through the standard pipeline.
/// </para>
/// </remarks>
/// <param name="options">The parallel execution configuration.</param>
/// <param name="logger">The logger for diagnostic output.</param>
[AppliesTo(MessageKinds.Event)]
public sealed partial class ParallelEventHandlerMiddleware(
	IOptions<ParallelEventHandlerOptions> options,
	ILogger<ParallelEventHandlerMiddleware> logger) : IDispatchMiddleware
{
	private readonly ParallelEventHandlerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<ParallelEventHandlerMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.Event;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Get parallel handler delegates from context (set by the pipeline builder)
		var parallelDelegates = context.GetItem<IReadOnlyList<DispatchRequestDelegate>>("ParallelHandlers");
		if (parallelDelegates == null || parallelDelegates.Count <= 1)
		{
			// No parallel delegates available or single handler - fall through to standard pipeline
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		LogParallelExecution(parallelDelegates.Count, _options.MaxDegreeOfParallelism, context.MessageId ?? string.Empty);

		using var timeoutCts = _options.Timeout.HasValue
			? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
			: null;

		if (timeoutCts != null)
		{
			timeoutCts.CancelAfter(_options.Timeout!.Value);
		}

		var effectiveToken = timeoutCts?.Token ?? cancellationToken;

		return _options.WhenAllStrategy switch
		{
			WhenAllStrategy.WaitAll => await ExecuteWaitAllAsync(
				parallelDelegates, message, context, effectiveToken).ConfigureAwait(false),
			WhenAllStrategy.FirstFailure => await ExecuteFirstFailureAsync(
				parallelDelegates, message, context, effectiveToken).ConfigureAwait(false),
			_ => await nextDelegate(message, context, cancellationToken).ConfigureAwait(false),
		};
	}

	[LoggerMessage(MiddlewareEventId.BatchProcessingStarted, LogLevel.Debug,
		"Executing {HandlerCount} event handlers in parallel (max parallelism: {MaxParallelism}) for message {MessageId}")]
	private partial void LogParallelExecution(int handlerCount, int maxParallelism, string messageId);

	[LoggerMessage(MiddlewareEventId.BatchCompleted, LogLevel.Debug,
		"All {HandlerCount} parallel event handlers completed for message {MessageId}")]
	private partial void LogParallelCompleted(int handlerCount, string messageId);

	/// <summary>
	/// Executes all handlers and waits for all to complete, collecting exceptions.
	/// </summary>
	private async ValueTask<IMessageResult> ExecuteWaitAllAsync(
		IReadOnlyList<DispatchRequestDelegate> delegates,
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		using var semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);
		var tasks = new Task<IMessageResult>[delegates.Count];

		for (var i = 0; i < delegates.Count; i++)
		{
			var handler = delegates[i];
			tasks[i] = ExecuteWithSemaphoreAsync(semaphore, handler, message, context, cancellationToken);
		}

		try
		{
			var results = await Task.WhenAll(tasks).ConfigureAwait(false);
			LogParallelCompleted(delegates.Count, context.MessageId ?? string.Empty);

			// Return first failure or success
			foreach (var result in results)
			{
				if (!result.Succeeded)
				{
					return result;
				}
			}

			return MessageResult.Success();
		}
		catch (Exception)
		{
			// Collect all exceptions from faulted tasks
			var exceptions = tasks
				.Where(static t => t.IsFaulted)
				.Select(static t => t.Exception!.InnerException ?? t.Exception)
				.ToList();

			if (exceptions.Count == 1)
			{
				throw exceptions[0];
			}

			throw new AggregateException(
				"Multiple event handlers failed during parallel execution.", exceptions);
		}
	}

	/// <summary>
	/// Executes handlers and cancels remaining on first failure.
	/// </summary>
	private async ValueTask<IMessageResult> ExecuteFirstFailureAsync(
		IReadOnlyList<DispatchRequestDelegate> delegates,
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		using var failureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		using var semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);
		var tasks = new Task<IMessageResult>[delegates.Count];

		for (var i = 0; i < delegates.Count; i++)
		{
			var handler = delegates[i];
			tasks[i] = ExecuteWithSemaphoreAsync(semaphore, handler, message, context, failureCts.Token);
		}

		try
		{
			var results = await Task.WhenAll(tasks).ConfigureAwait(false);
			LogParallelCompleted(delegates.Count, context.MessageId ?? string.Empty);

			foreach (var result in results)
			{
				if (!result.Succeeded)
				{
					return result;
				}
			}

			return MessageResult.Success();
		}
		catch (Exception)
		{
			// Cancel remaining handlers on first failure
			await failureCts.CancelAsync().ConfigureAwait(false);

			var firstFaulted = tasks.FirstOrDefault(static t => t.IsFaulted);
			if (firstFaulted?.Exception?.InnerException != null)
			{
				throw firstFaulted.Exception.InnerException;
			}

			throw;
		}
	}

	/// <summary>
	/// Executes a handler delegate within a semaphore-controlled concurrency limit.
	/// </summary>
	private static async Task<IMessageResult> ExecuteWithSemaphoreAsync(
		SemaphoreSlim semaphore,
		DispatchRequestDelegate handler,
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return await handler(message, context, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			semaphore.Release();
		}
	}
}
