// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Default implementation of transport adapter router that routes messages through the dispatcher pipeline.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TransportAdapterRouter" /> class. </remarks>
/// <param name="dispatcher"> The dispatcher to route messages through. </param>
/// <param name="logger"> The logger. </param>
public sealed partial class TransportAdapterRouter(IDispatcher dispatcher, ILogger<TransportAdapterRouter> logger) : ITransportAdapterRouter
{
	private readonly IDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
	private readonly ILogger<TransportAdapterRouter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Thread-safe registry of transport adapters.
	/// </summary>
	private readonly ConcurrentDictionary<string, IMessageBusAdapter> _registeredAdapters = new(StringComparer.Ordinal);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public async Task<IMessageResult> RouteAsync(
		IDispatchMessage message,
		IMessageContext context,
		string adapterId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrEmpty(adapterId);

		try
		{
			// Enrich context with transport adapter metadata
			context.Items["TransportAdapterId"] = adapterId;
			context.Items["RoutedTimestamp"] = DateTimeOffset.UtcNow;

			LogRoutingMessage(context.MessageId ?? string.Empty, message.GetType().Name, adapterId);

			// Route through dispatcher pipeline
			var result = await _dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);

			if (result.Succeeded)
			{
				LogRoutingSuccess(context.MessageId ?? string.Empty, adapterId);
			}
			else
			{
				LogRoutingFailure(context.MessageId ?? string.Empty, adapterId, result.ErrorMessage);
			}

			return result;
		}
		catch (Exception ex)
		{
			LogRoutingError(context.MessageId ?? string.Empty, adapterId, ex);

			var problemDetails = new MessageProblemDetails
			{
				Type = Resources.TransportAdapterRouter_RoutingFailureType,
				Title = Resources.TransportAdapterRouter_RoutingFailureTitle,
				ErrorCode = 500,
				Status = 500,
				Detail = string.Format(
					CultureInfo.InvariantCulture,
					Resources.TransportAdapterRouter_RoutingFailureDetail,
					adapterId,
					ex.Message),
				Instance = Guid.NewGuid().ToString(),
				Extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
				{
					["AdapterId"] = adapterId,
					["Exception"] = ex.GetType().Name,
				},
			};

			return Messaging.MessageResult.Failure(problemDetails);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<IMessageResult>> RouteBatchAsync(
		IReadOnlyList<IDispatchMessage> messages,
		IReadOnlyList<IMessageContext> contexts,
		string adapterId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);
		ArgumentNullException.ThrowIfNull(contexts);
		ArgumentException.ThrowIfNullOrEmpty(adapterId);

		if (messages.Count != contexts.Count)
		{
			throw new ArgumentException(ErrorMessages.MessagesAndContextsMustHaveSameCount, nameof(contexts));
		}

		if (!messages.Any())
		{
			return Array.Empty<IMessageResult>();
		}

		LogRoutingBatch(messages.Count, adapterId);

		var results = new List<IMessageResult>(messages.Count);

		// Process messages individually to ensure each has proper error handling
		for (var i = 0; i < messages.Count; i++)
		{
			var result = await RouteAsync(messages[i], contexts[i], adapterId, cancellationToken).ConfigureAwait(false);
			results.Add(result);
		}

		return results;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public async Task RegisterAdapterAsync(
		IMessageBusAdapter adapter,
		string adapterId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
		ArgumentNullException.ThrowIfNull(adapter);

		try
		{
			var added = _registeredAdapters.TryAdd(adapterId, adapter);

			if (!added)
			{
				LogAdapterAlreadyRegistered(adapterId);
				throw new InvalidOperationException($"Transport adapter '{adapterId}' is already registered");
			}

			LogAdapterRegistered(adapterId, adapter.GetType().Name);

			// Initialize adapter if needed
			if (adapter is IAsyncInitializable initializableAdapter)
			{
				await initializableAdapter.InitializeAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogAdapterRegistrationFailed(adapterId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public async Task UnregisterAdapterAsync(
		string adapterId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);

		try
		{
			// R0.8: Dispose objects before losing scope - adapter is disposed in subsequent code (lines 233-240)
#pragma warning disable CA2000
			var removed = _registeredAdapters.TryRemove(adapterId, out var adapter);
#pragma warning restore CA2000

			if (!removed)
			{
				LogAdapterUnregisterAttempt(adapterId);
				return;
			}

			LogAdapterUnregistered(adapterId, adapter.GetType().Name);

			// Dispose adapter if needed
			if (adapter is IAsyncDisposable asyncDisposableAdapter)
			{
				await asyncDisposableAdapter.DisposeAsync().ConfigureAwait(false);
			}
			else if (adapter is IDisposable disposableAdapter)
			{
				disposableAdapter.Dispose();
			}
		}
		catch (Exception ex)
		{
			LogAdapterUnregistrationFailed(adapterId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public async Task<IDictionary<string, Abstractions.Transport.HealthCheckResult>> CheckAdapterHealthAsync(
		CancellationToken cancellationToken)
	{
		var healthResults = new Dictionary<string, Abstractions.Transport.HealthCheckResult>(StringComparer.Ordinal);

		foreach (var kvp in _registeredAdapters)
		{
			var adapterId = kvp.Key;
			var adapter = kvp.Value;

			try
			{
				bool isHealthy;
				string description;

				// Check if adapter supports health checking
				if (adapter is IHealthCheckable healthCheckableAdapter)
				{
					isHealthy = await healthCheckableAdapter.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
					description = isHealthy ? "Adapter reports healthy" : "Adapter reports unhealthy";
				}
				else
				{
					// If adapter doesn't support health checking, assume it's healthy if registered
					isHealthy = true;
					description = "Health checking not supported, assuming healthy";
				}

				var data = new Dictionary<string, object>(StringComparer.Ordinal) { ["CheckTimestamp"] = DateTimeOffset.UtcNow };
				healthResults[adapterId] = isHealthy
					? Abstractions.Transport.HealthCheckResult.Healthy(description)
					: Abstractions.Transport.HealthCheckResult.Unhealthy(description);

				LogHealthCheck(adapterId, isHealthy, description);
			}
			catch (Exception ex)
			{
				LogHealthCheckFailed(adapterId, ex);

				_ = new Dictionary<string, object>
					(StringComparer.Ordinal)
				{
					["CheckTimestamp"] = DateTimeOffset.UtcNow,
					["Exception"] = ex.GetType().Name,
					["ExceptionMessage"] = ex.Message,
				};
				healthResults[adapterId] = Abstractions.Transport.HealthCheckResult.Unhealthy(
					string.Format(
						CultureInfo.InvariantCulture,
						Resources.TransportAdapterRouter_HealthCheckFailed,
						ex.Message));
			}
		}

		return healthResults;
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.TransportRoutingMessage, LogLevel.Trace,
		"Routing message {MessageId} of type {MessageType} from adapter {AdapterId}")]
	private partial void LogRoutingMessage(string messageId, string messageType, string adapterId);

	[LoggerMessage(DeliveryEventId.TransportRoutingSuccess, LogLevel.Trace,
		"Successfully routed message {MessageId} from adapter {AdapterId}")]
	private partial void LogRoutingSuccess(string messageId, string adapterId);

	[LoggerMessage(DeliveryEventId.TransportRoutingFailure, LogLevel.Warning,
		"Failed to route message {MessageId} from adapter {AdapterId}: {Error}")]
	private partial void LogRoutingFailure(string messageId, string adapterId, string? error);

	[LoggerMessage(DeliveryEventId.TransportRoutingError, LogLevel.Error,
		"Error routing message {MessageId} from adapter {AdapterId}")]
	private partial void LogRoutingError(string messageId, string adapterId, Exception ex);

	[LoggerMessage(DeliveryEventId.TransportRoutingBatch, LogLevel.Trace,
		"Routing batch of {MessageCount} messages from adapter {AdapterId}")]
	private partial void LogRoutingBatch(int messageCount, string adapterId);

	[LoggerMessage(DeliveryEventId.TransportAdapterAlreadyRegistered, LogLevel.Warning,
		"Adapter {AdapterId} is already registered")]
	private partial void LogAdapterAlreadyRegistered(string adapterId);

	[LoggerMessage(DeliveryEventId.TransportAdapterRegistered, LogLevel.Information,
		"Registered transport adapter {AdapterId} of type {AdapterType}")]
	private partial void LogAdapterRegistered(string adapterId, string adapterType);

	[LoggerMessage(DeliveryEventId.TransportAdapterRegistrationFailed, LogLevel.Error,
		"Failed to register transport adapter {AdapterId}")]
	private partial void LogAdapterRegistrationFailed(string adapterId, Exception ex);

	[LoggerMessage(DeliveryEventId.TransportAdapterUnregisterAttempt, LogLevel.Warning,
		"Attempted to unregister non-existent adapter {AdapterId}")]
	private partial void LogAdapterUnregisterAttempt(string adapterId);

	[LoggerMessage(DeliveryEventId.TransportAdapterUnregistered, LogLevel.Information,
		"Unregistered transport adapter {AdapterId} of type {AdapterType}")]
	private partial void LogAdapterUnregistered(string adapterId, string adapterType);

	[LoggerMessage(DeliveryEventId.TransportAdapterUnregistrationFailed, LogLevel.Error,
		"Failed to unregister transport adapter {AdapterId}")]
	private partial void LogAdapterUnregistrationFailed(string adapterId, Exception ex);

	[LoggerMessage(DeliveryEventId.TransportAdapterHealthCheck, LogLevel.Trace,
		"Health check for adapter {AdapterId}: {IsHealthy} - {Description}")]
	private partial void LogHealthCheck(string adapterId, bool isHealthy, string description);

	[LoggerMessage(DeliveryEventId.TransportAdapterHealthCheckFailed, LogLevel.Error,
		"Health check failed for transport adapter {AdapterId}")]
	private partial void LogHealthCheckFailed(string adapterId, Exception ex);
}
