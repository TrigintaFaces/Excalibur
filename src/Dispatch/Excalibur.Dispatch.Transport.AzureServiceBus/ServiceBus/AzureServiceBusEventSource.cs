// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Event source for Azure Service Bus operations providing structured logging capabilities.
/// </summary>
public static partial class AzureServiceBusEventSource
{
	// Source-generated logging methods (Sprint 362 - EventId Migration)

	/// <summary>
	/// Logs successful message processing completion.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="messageId"> The ID of the processed message. </param>
	[LoggerMessage(AzureServiceBusEventId.MessageProcessed, LogLevel.Information,
		"Processed and completed message {MessageId}")]
	public static partial void LogMessageProcessed(ILogger logger, string messageId);

	/// <summary>
	/// Logs message abandonment due to processing errors.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="messageId"> The ID of the abandoned message. </param>
	/// <param name="exception"> The exception that caused the abandonment. </param>
	[LoggerMessage(AzureServiceBusEventId.MessageAbandonedWithError, LogLevel.Error,
		"Message {MessageId} abandoned due to error")]
	public static partial void LogMessageAbandoned(ILogger logger, string messageId, Exception exception);

	/// <summary>
	/// Logs processing errors that occur during message handling.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="entityPath"> The Service Bus entity path where the error occurred. </param>
	/// <param name="exception"> The processing exception. </param>
	[LoggerMessage(AzureServiceBusEventId.ProcessingError, LogLevel.Error,
		"Error processing message: {EntityPath}")]
	public static partial void LogProcessingError(ILogger logger, string entityPath, Exception exception);
}
