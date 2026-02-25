// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

using AwsLambdaSample.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AwsLambdaSample.Functions;

/// <summary>
/// AWS Lambda function handling SQS queue messages.
/// Demonstrates Dispatch messaging integration with SQS triggers.
/// </summary>
public class SqsHandler
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<SqsHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsHandler"/> class.
	/// </summary>
	public SqsHandler()
	{
		_serviceProvider = Startup.ServiceProvider;
		_logger = _serviceProvider.GetRequiredService<ILogger<SqsHandler>>();
	}

	/// <summary>
	/// Processes messages from SQS queue.
	/// </summary>
	/// <param name="sqsEvent">The SQS event containing messages.</param>
	/// <param name="context">The Lambda execution context.</param>
	/// <returns>Batch item failures for partial batch response.</returns>
	/// <remarks>
	/// <para>
	/// This function uses partial batch response to report individual message failures.
	/// Failed messages will be retried or sent to the dead-letter queue.
	/// </para>
	/// <para>
	/// Configure the SQS queue with:
	/// - ReportBatchItemFailures enabled
	/// - A dead-letter queue for failed messages
	/// </para>
	/// </remarks>
	[LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
	public async Task<SQSBatchResponse> ProcessOrderEventsAsync(
		SQSEvent sqsEvent,
		ILambdaContext context)
	{
		_logger.LogInformation(
			"SQS: Processing {MessageCount} messages, RequestId: {RequestId}",
			sqsEvent.Records.Count,
			context.AwsRequestId);

		var batchItemFailures = new List<SQSBatchResponse.BatchItemFailure>();

		foreach (var record in sqsEvent.Records)
		{
			try
			{
				await ProcessMessageAsync(record).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to process message {MessageId}", record.MessageId);

				// Report this message as failed for retry
				batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId, });
			}
		}

		_logger.LogInformation(
			"SQS: Processed {SuccessCount} succeeded, {FailCount} failed",
			sqsEvent.Records.Count - batchItemFailures.Count,
			batchItemFailures.Count);

		return new SQSBatchResponse { BatchItemFailures = batchItemFailures, };
	}

	private async Task ProcessMessageAsync(SQSEvent.SQSMessage message)
	{
		_logger.LogInformation("Processing SQS message: {MessageId}", message.MessageId);

		// Deserialize the event from SQS message body
		var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(
			message.Body,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		if (orderEvent is null)
		{
			throw new InvalidOperationException($"Could not deserialize message {message.MessageId}");
		}

		// Get dispatcher from DI
		using var scope = _serviceProvider.CreateScope();
		var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

		// Create dispatch context with SQS metadata
		var dispatchContext = DispatchContextInitializer.CreateDefaultContext();

		// Dispatch the event
		_ = await dispatcher.DispatchAsync(orderEvent, dispatchContext, cancellationToken: default).ConfigureAwait(false);

		_logger.LogInformation(
			"Successfully processed OrderCreatedEvent for order {OrderId}",
			orderEvent.OrderId);
	}
}
