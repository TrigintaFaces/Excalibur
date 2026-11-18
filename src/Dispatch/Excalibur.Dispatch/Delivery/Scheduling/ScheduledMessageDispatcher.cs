// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>Decodes and dispatches one admitted payload inside the caller-owned occurrence scope.</summary>
internal static partial class ScheduledMessageDispatcher
{
	internal static async Task<ScheduleFailureReason?> DispatchAsync(IScheduledMessage item, ScheduleExecutionLease lease,
		IServiceProvider scopedProvider, IDispatcher dispatcher, DispatchJsonSerializer serializer,
		TimeProvider timeProvider, ILogger logger, CancellationToken cancellationToken)
	{
		// Establish the schedule's tenant as ambient for the whole dispatch, at the point of use. Stamping
		// the identity feature alone is not enough: it is read from the message context, whereas every
		// ITenantContext-reading store a handler touches reads the ambient holder, and nothing on this path
		// wrote it. A scheduled message therefore ran with the poller's ambient tenant -- normally none --
		// so a handler's writes landed untenanted while the context claimed the right tenant. Establishing
		// it here rather than at the caller makes the caller's scope irrelevant, so no future caller can
		// reintroduce the gap.
		//
		// The term is read back off a stored schedule, so it goes through the total store-read conversion.
		// A raw null CLEARS the ambient, and a cleared ambient means "no tenant was established" -- which a
		// multi-tenant store fails closed on. An untenanted schedule is a different state and binds the
		// reserved untenanted term.
		using var tenantScope = TenantContextHolder.BeginScope(
			KeyedTenantPartition.FromStoredValue(item.TenantId).TenantId);

		if (!MessageTypeRegistry.TryGetType(item.MessageName, out var type))
		{
			LogUnknownMessageType(logger, item.MessageName);
			return ScheduleFailureReason.UnknownType;
		}

		object? message;
		try { message = await serializer.DeserializeAsync(item.MessageBody, type).ConfigureAwait(false); }
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception ex)
		{
			LogErrorProcessingMessages(logger, ex);
			return ScheduleFailureReason.MalformedPayload;
		}
		if (message is null)
		{
			LogDeserializationFailed(logger, item.Id);
			return ScheduleFailureReason.MalformedPayload;
		}

		var context = DispatchContextInitializer.CreateDefaultContext(scopedProvider);
		context.CorrelationId = item.CorrelationId;
		context.Items["ScheduleStoreId"] = lease.StoreId;
		context.Items["ScheduleId"] = lease.ScheduleId;
		context.Items["ScheduleGeneration"] = lease.Generation;
		context.Items["ScheduleOccurrenceUtc"] = lease.OccurrenceUtc;

		var identityFeature = context.GetOrCreateIdentityFeature();
		identityFeature.TraceParent = item.TraceParent;
		identityFeature.TenantId = item.TenantId;
		identityFeature.UserId = item.UserId;

		// Add timezone information to context if available
		if (!string.IsNullOrEmpty(item.TimeZoneId))
		{
			context.Items["ScheduleTimeZone"] = item.TimeZoneId;
		}

		IMessageResult? result;
		switch (message)
		{
			case IDispatchAction action:
				result = await dispatcher.DispatchAsync(action, context, cancellationToken).ConfigureAwait(false);
				break;

			case IDispatchEvent evt:
				result = await dispatcher.DispatchAsync(evt, context, cancellationToken).ConfigureAwait(false);
				break;

			case IDispatchMessage dispatchMessage:
				result = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken).ConfigureAwait(false);
				break;

			default:
				LogUnsupportedMessageType(logger, message.GetType().Name);
				return ScheduleFailureReason.UnsupportedType;
		}

		// A handler may swallow cancellation and return success. The processing deadline
		// still owns this attempt, so never record success after its token has expired.
		cancellationToken.ThrowIfCancellationRequested();
		if (result is null || !result.Succeeded)
		{
			// Returned failures follow the same per-row policy as thrown failures. A cancelled
			// dispatch token retains host-shutdown and handler-deadline ownership at the caller.
			LogErrorProcessingMessages(logger, new InvalidOperationException(result?.ErrorMessage ?? result?.ProblemDetails?.Detail
				?? "Scheduled message dispatch did not succeed."));
			return ScheduleFailureReason.InvalidResult;
		}

		// Update last execution time
		item.LastExecutionUtc = timeProvider.GetUtcNow();
		return null;
	}

	[LoggerMessage(DeliveryEventId.ScheduledUnknownMessageType, LogLevel.Warning,
		"Unknown scheduled message type {Type}")]
	private static partial void LogUnknownMessageType(ILogger logger, string type);

	[LoggerMessage(DeliveryEventId.ScheduledDeserializationFailed, LogLevel.Warning,
		"Failed to deserialize scheduled message {MessageId}")]
	private static partial void LogDeserializationFailed(ILogger logger, Guid messageId);

	[LoggerMessage(DeliveryEventId.ScheduledProcessingError, LogLevel.Error,
		"Error processing scheduled messages")]
	private static partial void LogErrorProcessingMessages(ILogger logger, Exception ex);

	[LoggerMessage(DeliveryEventId.ScheduledUnsupportedMessageType, LogLevel.Warning,
		"Message type {Type} is not supported for scheduling")]
	private static partial void LogUnsupportedMessageType(ILogger logger, string type);

}
