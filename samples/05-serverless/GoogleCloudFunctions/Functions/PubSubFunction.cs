// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Google.Cloud.Functions.Framework;

using GoogleCloudFunctionsSample.Messages;

using Microsoft.Extensions.Logging;

namespace GoogleCloudFunctionsSample.Functions;

/// <summary>
/// Pub/Sub-triggered Google Cloud Function for processing order events.
/// Demonstrates Dispatch messaging integration with Cloud Pub/Sub.
/// </summary>
public class PubSubFunction : ICloudEventFunction
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<PubSubFunction> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubFunction"/> class.
	/// </summary>
	/// <param name="dispatcher">The Dispatch dispatcher.</param>
	/// <param name="logger">The logger instance.</param>
	public PubSubFunction(IDispatcher dispatcher, ILogger<PubSubFunction> logger)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Handles incoming Pub/Sub messages via CloudEvents.
	/// </summary>
	/// <param name="cloudEvent">The CloudEvent containing the Pub/Sub message.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// Pub/Sub messages are delivered as CloudEvents with:
	/// - Type: google.cloud.pubsub.topic.v1.messagePublished
	/// - Data: MessagePublishedData containing the Pub/Sub message
	/// </remarks>
	public async Task HandleAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		_logger.LogInformation(
			"Pub/Sub trigger: {Type}, Source: {Source}, Id: {Id}",
			cloudEvent.Type,
			cloudEvent.Source,
			cloudEvent.Id);

		// Extract message data from CloudEvent
		if (cloudEvent.Data is null)
		{
			_logger.LogWarning("Received CloudEvent with no data");
			return;
		}

		try
		{
			// Parse the Pub/Sub message data
			var messageData = ParsePubSubMessage(cloudEvent.Data);

			if (messageData is null)
			{
				_logger.LogWarning("Could not parse Pub/Sub message data");
				return;
			}

			// Create order event from message
			var orderEvent = new OrderCreatedEvent(
				messageData.OrderId ?? $"ORD-{Guid.NewGuid():N}",
				messageData.CustomerId ?? "UNKNOWN",
				messageData.TotalAmount,
				DateTimeOffset.UtcNow);

			// Create dispatch context
			var dispatchContext = DispatchContextInitializer.CreateDefaultContext();

			// Dispatch the event
			_ = await _dispatcher.DispatchAsync(orderEvent, dispatchContext, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Processed Pub/Sub message for order {OrderId}",
				orderEvent.OrderId);
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex, "Failed to deserialize Pub/Sub message");
			throw; // Let Cloud Functions handle the retry
		}
	}

	private static PubSubOrderMessage? ParsePubSubMessage(object data)
	{
		// CloudEvents from Pub/Sub contain base64-encoded data
		// The actual structure depends on how the message was published
		if (data is JsonElement jsonElement)
		{
			// Try to extract the message data
			if (jsonElement.TryGetProperty("message", out var message) &&
				message.TryGetProperty("data", out var base64Data))
			{
				var decodedBytes = Convert.FromBase64String(base64Data.GetString() ?? string.Empty);
				var decodedJson = System.Text.Encoding.UTF8.GetString(decodedBytes);
				return JsonSerializer.Deserialize<PubSubOrderMessage>(decodedJson,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true, });
			}

			// Direct JSON parsing fallback
			return JsonSerializer.Deserialize<PubSubOrderMessage>(jsonElement.GetRawText(),
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true, });
		}

		return null;
	}

	/// <summary>
	/// Internal message type for Pub/Sub order messages.
	/// </summary>
	private sealed record PubSubOrderMessage(
		string? OrderId,
		string? CustomerId,
		decimal TotalAmount);
}
