// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when messaging-related errors occur.
/// </summary>
[Serializable]
public sealed class MessagingException : DispatchException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MessagingException" /> class.
	/// </summary>
	public MessagingException()
		: base(ErrorCodes.MessageSendFailed, ErrorMessages.MessagingErrorOccurred)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagingException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	public MessagingException(string message)
		: base(ErrorCodes.MessageSendFailed, message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagingException" /> class with a specified error message and a reference to the
	/// inner exception.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public MessagingException(string message, Exception innerException)
		: base(ErrorCodes.MessageSendFailed, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagingException" /> class with an explicit error code.
	/// </summary>
	/// <param name="errorCode">The error code to associate with the exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public MessagingException(string errorCode, string message) : base(errorCode, message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagingException" /> class with an explicit error code and inner exception.
	/// </summary>
	/// <param name="errorCode">The error code to associate with the exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public MessagingException(string errorCode, string message, Exception? innerException) : base(errorCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the message ID that caused the exception.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the message type that caused the exception.
	/// </summary>
	/// <value>The current <see cref="MessageType"/> value.</value>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the queue or topic name involved in the exception.
	/// </summary>
	/// <value>The current <see cref="QueueName"/> value.</value>
	public string? QueueName { get; set; }

	/// <summary>
	/// Creates an exception for when a message handler is not found.
	/// </summary>
	/// <param name="messageType"> The type of message for which no handler was found. </param>
	/// <returns> A new MessagingException instance. </returns>
	public static MessagingException HandlerNotFound(Type messageType)
	{
		var ex = new MessagingException($"No handler found for message type '{messageType.Name}'.")
		{
			Data = { ["ErrorCode"] = ErrorCodes.MessageHandlerNotFound },
			MessageType = messageType.FullName,
		};
		return ex.WithContext("messageType", messageType.FullName)
			.WithSuggestedAction($"Register a handler for {messageType.Name} in your service configuration.")
			.WithStatusCode(500) as MessagingException ?? new MessagingException();
	}

	/// <summary>
	/// Creates an exception for when message routing fails.
	/// </summary>
	/// <param name="messageId"> The ID of the message that failed to route. </param>
	/// <param name="reason"> The reason for the routing failure. </param>
	/// <returns> A new MessagingException instance. </returns>
	public static MessagingException RoutingFailed(string messageId, string reason)
	{
		var ex = new MessagingException($"Failed to route message '{messageId}': {reason}")
		{
			Data = { ["ErrorCode"] = ErrorCodes.MessageRoutingFailed },
			MessageId = messageId,
		};
		return ex.WithContext("messageId", messageId)
			.WithContext("reason", reason)
			.WithSuggestedAction("Check the routing configuration and ensure the destination is available.")
			.WithStatusCode(500) as MessagingException ?? new MessagingException();
	}

	/// <summary>
	/// Creates an exception for when a duplicate message is detected.
	/// </summary>
	/// <param name="messageId"> The ID of the duplicate message. </param>
	/// <returns> A new MessagingException instance. </returns>
	public static MessagingException DuplicateMessage(string messageId)
	{
		var ex = new MessagingException($"Duplicate message detected: '{messageId}'")
		{
			Data = { ["ErrorCode"] = ErrorCodes.MessageDuplicate },
			MessageId = messageId,
		};
		return ex.WithContext("messageId", messageId)
			.WithSuggestedAction("This message has already been processed. No action required.")
			.WithStatusCode(409) as MessagingException ?? new MessagingException(); // Conflict
	}

	/// <summary>
	/// Creates an exception for when the retry limit is exceeded.
	/// </summary>
	/// <param name="messageId"> The ID of the message that exceeded retry limit. </param>
	/// <param name="retryCount"> The number of retries attempted. </param>
	/// <returns> A new MessagingException instance. </returns>
	public static MessagingException RetryLimitExceeded(string messageId, int retryCount)
	{
		var ex = new MessagingException($"Message '{messageId}' exceeded retry limit after {retryCount} attempts.")
		{
			Data = { ["ErrorCode"] = ErrorCodes.MessageRetryLimitExceeded },
			MessageId = messageId,
		};
		return ex.WithContext("messageId", messageId)
			.WithContext("retryCount", retryCount)
			.WithSuggestedAction("Message has been moved to dead letter queue for manual inspection.")
			.WithStatusCode(500) as MessagingException ?? new MessagingException();
	}

	/// <summary>
	/// Creates an exception for when the message broker connection fails.
	/// </summary>
	/// <param name="brokerAddress"> The address of the message broker. </param>
	/// <param name="innerException"> The inner exception with connection details. </param>
	/// <returns> A new MessagingException instance. </returns>
	public static MessagingException BrokerConnectionFailed(string brokerAddress, Exception? innerException = null)
	{
		var ex = new MessagingException(
			$"Failed to connect to message broker at '{brokerAddress}'.",
						innerException ?? new InvalidOperationException(ErrorMessages.ConnectionFailed))
		{
			Data = { ["ErrorCode"] = ErrorCodes.MessageBrokerConnectionFailed },
		};
		return ex.WithContext("brokerAddress", brokerAddress)
			.WithSuggestedAction("Check network connectivity and broker configuration.")
			.WithStatusCode(503) as MessagingException ?? new MessagingException(); // Service Unavailable
	}
}
