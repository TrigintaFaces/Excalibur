// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Timing;

/// <summary>
/// Default implementation of time-based policies for message processing. R7.4: Configurable timeout handling with adaptive capabilities.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DefaultTimePolicy" /> class. </remarks>
/// <param name="options"> The time policy options. </param>
/// <param name="monitor"> Optional timeout monitor for adaptive timeouts. </param>
public sealed class DefaultTimePolicy(IOptions<TimePolicyOptions> options, ITimeoutMonitor? monitor = null) : ITimePolicy
{
	private readonly TimePolicyOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public TimeSpan DefaultTimeout => _options.DefaultTimeout;

	/// <inheritdoc />
	public TimeSpan MaxTimeout => _options.MaxTimeout;

	/// <inheritdoc />
	public TimeSpan HandlerTimeout => _options.HandlerTimeout;

	/// <inheritdoc />
	public TimeSpan SerializationTimeout => _options.SerializationTimeout;

	/// <inheritdoc />
	public TimeSpan TransportTimeout => _options.TransportTimeout;

	/// <inheritdoc />
	public TimeSpan ValidationTimeout => _options.ValidationTimeout;

	/// <inheritdoc />
	public TimeSpan GetTimeoutFor(TimeoutOperationType operationType)
	{
		// Check for custom timeout overrides
		if (_options.CustomTimeouts.TryGetValue(operationType, out var customTimeout))
		{
			return customTimeout;
		}

		// Use adaptive timeouts if enabled and monitor is available
		if (_options.UseAdaptiveTimeouts && monitor?.HasSufficientSamples(operationType, _options.MinimumSampleSize) == true)
		{
			var adaptiveTimeout = monitor.GetRecommendedTimeout(operationType, _options.AdaptiveTimeoutPercentile);

			// Ensure adaptive timeout doesn't exceed max timeout
			return TimeSpan.FromTicks(Math.Min(adaptiveTimeout.Ticks, _options.MaxTimeout.Ticks));
		}

		// Fall back to configured timeouts
		var baseTimeout = operationType switch
		{
			TimeoutOperationType.Handler => _options.HandlerTimeout,
			TimeoutOperationType.Serialization => _options.SerializationTimeout,
			TimeoutOperationType.Transport => _options.TransportTimeout,
			TimeoutOperationType.Validation => _options.ValidationTimeout,
			TimeoutOperationType.Middleware => _options.DefaultTimeout,
			TimeoutOperationType.Pipeline => _options.HandlerTimeout,
			TimeoutOperationType.Outbox => _options.TransportTimeout,
			TimeoutOperationType.Inbox => _options.TransportTimeout,
			TimeoutOperationType.Scheduling => _options.DefaultTimeout,
			TimeoutOperationType.Database => _options.TransportTimeout,
			TimeoutOperationType.Http => _options.TransportTimeout,
			_ => _options.DefaultTimeout,
		};

		return baseTimeout;
	}

	/// <inheritdoc />
	public bool ShouldApplyTimeout(TimeoutOperationType operationType, TimeoutContext? context = null)
	{
		if (!_options.EnforceTimeouts)
		{
			return false;
		}

		// Check if we have a specific timeout configured
		var timeout = GetTimeoutFor(operationType);

		// Apply complexity multipliers if context is provided
		if (context != null)
		{
			timeout = ApplyComplexityMultiplier(timeout, context.Complexity);
		}

		// Don't apply timeout if it would be negative or zero
		return timeout > TimeSpan.Zero;
	}

	/// <inheritdoc />
	public CancellationToken CreateTimeoutToken(TimeoutOperationType operationType, CancellationToken parentToken)
	{
		if (!ShouldApplyTimeout(operationType))
		{
			return parentToken;
		}

		var timeout = GetTimeoutFor(operationType);

		// Create a combined cancellation token
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
		cts.CancelAfter(timeout);

		return cts.Token;
	}

	/// <summary>
	/// Gets the timeout for a specific message type, applying message-specific overrides if configured.
	/// </summary>
	/// <param name="operationType"> The operation type. </param>
	/// <param name="messageType"> The message type. </param>
	/// <returns> The timeout for the message type. </returns>
	public TimeSpan GetTimeoutForMessage(TimeoutOperationType operationType, Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		var messageTypeName = messageType.FullName ?? messageType.Name;

		if (_options.MessageTypeTimeouts.TryGetValue(messageTypeName, out var messageTimeout))
		{
			return messageTimeout;
		}

		return GetTimeoutFor(operationType);
	}

	/// <summary>
	/// Gets the timeout for a specific handler type, applying handler-specific overrides if configured.
	/// </summary>
	/// <param name="operationType"> The operation type. </param>
	/// <param name="handlerType"> The handler type. </param>
	/// <returns> The timeout for the handler type. </returns>
	public TimeSpan GetTimeoutForHandler(TimeoutOperationType operationType, Type handlerType)
	{
		ArgumentNullException.ThrowIfNull(handlerType);

		var handlerTypeName = handlerType.FullName ?? handlerType.Name;

		if (_options.HandlerTypeTimeouts.TryGetValue(handlerTypeName, out var handlerTimeout))
		{
			return handlerTimeout;
		}

		return GetTimeoutFor(operationType);
	}

	/// <summary>
	/// Gets the timeout for a specific context, applying all relevant overrides and multipliers.
	/// </summary>
	/// <param name="operationType"> The operation type. </param>
	/// <param name="context"> The timeout context. </param>
	/// <returns> The timeout for the context. </returns>
	public TimeSpan GetTimeoutForContext(TimeoutOperationType operationType, TimeoutContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var timeout = GetTimeoutFor(operationType);

		// Apply message-specific timeout if available
		if (context.MessageType != null)
		{
			timeout = GetTimeoutForMessage(operationType, context.MessageType);
		}

		// Apply handler-specific timeout if available
		if (context.HandlerType != null)
		{
			timeout = GetTimeoutForHandler(operationType, context.HandlerType);
		}

		// Apply complexity multiplier
		timeout = ApplyComplexityMultiplier(timeout, context.Complexity);

		// Apply retry multiplier if this is a retry
		if (context is { IsRetry: true, RetryCount: > 0 })
		{
			// Increase timeout by 25% for each retry, up to max timeout
			var retryMultiplier = 1.0 + (context.RetryCount * 0.25);
			timeout = TimeSpan.FromTicks((long)(timeout.Ticks * retryMultiplier));
		}

		// Ensure we don't exceed max timeout
		return TimeSpan.FromTicks(Math.Min(timeout.Ticks, _options.MaxTimeout.Ticks));
	}

	private TimeSpan ApplyComplexityMultiplier(TimeSpan baseTimeout, OperationComplexity complexity)
	{
		var multiplier = complexity switch
		{
			OperationComplexity.Simple => 0.5,
			OperationComplexity.Normal => 1.0,
			OperationComplexity.Complex => _options.ComplexityMultiplier,
			OperationComplexity.Heavy => _options.HeavyOperationMultiplier,
			_ => 1.0,
		};

		return TimeSpan.FromTicks((long)(baseTimeout.Ticks * multiplier));
	}
}
