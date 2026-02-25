// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.CloudStubs.Aws;

/// <summary>
/// Stub for Amazon.SQS.IAmazonSQS - compilation only for integration tests.
/// </summary>
public interface IAmazonSQS : IDisposable
{
	/// <summary>Sends a message to the specified queue.</summary>
	Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default);

	/// <summary>Receives messages from the specified queue.</summary>
	Task<ReceiveMessageResponse> ReceiveMessageAsync(ReceiveMessageRequest request, CancellationToken cancellationToken = default);

	/// <summary>Deletes a message from the queue.</summary>
	Task<DeleteMessageResponse> DeleteMessageAsync(DeleteMessageRequest request, CancellationToken cancellationToken = default);

	/// <summary>Gets queue URL by name.</summary>
	Task<GetQueueUrlResponse> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = default);

	/// <summary>Creates a new queue.</summary>
	Task<CreateQueueResponse> CreateQueueAsync(CreateQueueRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Send message request stub.</summary>
public class SendMessageRequest
{
	/// <summary>Gets or sets the queue URL.</summary>
	public string? QueueUrl { get; set; }

	/// <summary>Gets or sets the message body.</summary>
	public string? MessageBody { get; set; }

	/// <summary>Gets or sets the delay in seconds.</summary>
	public int DelaySeconds { get; set; }

	/// <summary>Gets or sets message attributes.</summary>
	public Dictionary<string, MessageAttributeValue>? MessageAttributes { get; set; }
}

/// <summary>Send message response stub.</summary>
public class SendMessageResponse
{
	/// <summary>Gets or sets the message ID.</summary>
	public string? MessageId { get; set; }

	/// <summary>Gets or sets the MD5 of the message body.</summary>
	public string? MD5OfMessageBody { get; set; }
}

/// <summary>Receive message request stub.</summary>
public class ReceiveMessageRequest
{
	/// <summary>Gets or sets the queue URL.</summary>
	public string? QueueUrl { get; set; }

	/// <summary>Gets or sets the max number of messages.</summary>
	public int MaxNumberOfMessages { get; set; } = 1;

	/// <summary>Gets or sets the wait time in seconds.</summary>
	public int WaitTimeSeconds { get; set; }

	/// <summary>Gets or sets the visibility timeout.</summary>
	public int VisibilityTimeout { get; set; }
}

/// <summary>Receive message response stub.</summary>
public class ReceiveMessageResponse
{
	/// <summary>Gets or sets the messages.</summary>
	public List<Message>? Messages { get; set; }
}

/// <summary>SQS message stub.</summary>
public class Message
{
	/// <summary>Gets or sets the message ID.</summary>
	public string? MessageId { get; set; }

	/// <summary>Gets or sets the receipt handle.</summary>
	public string? ReceiptHandle { get; set; }

	/// <summary>Gets or sets the message body.</summary>
	public string? Body { get; set; }

	/// <summary>Gets or sets message attributes.</summary>
	public Dictionary<string, MessageAttributeValue>? MessageAttributes { get; set; }
}

/// <summary>Delete message request stub.</summary>
public class DeleteMessageRequest
{
	/// <summary>Gets or sets the queue URL.</summary>
	public string? QueueUrl { get; set; }

	/// <summary>Gets or sets the receipt handle.</summary>
	public string? ReceiptHandle { get; set; }
}

/// <summary>Delete message response stub.</summary>
public class DeleteMessageResponse
{
}

/// <summary>Get queue URL response stub.</summary>
public class GetQueueUrlResponse
{
	/// <summary>Gets or sets the queue URL.</summary>
	public string? QueueUrl { get; set; }
}

/// <summary>Create queue request stub.</summary>
public class CreateQueueRequest
{
	/// <summary>Gets or sets the queue name.</summary>
	public string? QueueName { get; set; }

	/// <summary>Gets or sets attributes.</summary>
	public Dictionary<string, string>? Attributes { get; set; }
}

/// <summary>Create queue response stub.</summary>
public class CreateQueueResponse
{
	/// <summary>Gets or sets the queue URL.</summary>
	public string? QueueUrl { get; set; }
}

/// <summary>Message attribute value stub.</summary>
public class MessageAttributeValue
{
	/// <summary>Gets or sets the data type.</summary>
	public string? DataType { get; set; }

	/// <summary>Gets or sets the string value.</summary>
	public string? StringValue { get; set; }

	/// <summary>Gets or sets the binary value.</summary>
	public byte[]? BinaryValue { get; set; }
}
