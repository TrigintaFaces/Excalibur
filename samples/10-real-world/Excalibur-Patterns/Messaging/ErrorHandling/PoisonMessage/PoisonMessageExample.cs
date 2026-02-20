// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Excalibur.Dispatch.Abstractions.Pipeline;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Core.Messaging.ErrorHandling.PoisonMessage;

/// <summary>
///     Example showing how to configure and use poison message handling.
/// </summary>
public static class PoisonMessageExample
{
	/// <summary>
	///     Configures poison message handling for a host application.
	/// </summary>
	public static IHostBuilder ConfigurePoisonMessageHandling(this IHostBuilder hostBuilder) =>
		hostBuilder.ConfigureServices((context, services) =>
		{
			var connectionString = context.Configuration["ConnectionStrings:SqlServer"]
									 ?? "Server=localhost;Database=ExcaliburDispatch;Integrated Security=true;";

			// TODO: Uncomment when AddDispatch is available
			/*
 services.AddDispatch(builder =>
 {
 // Add error handling first
 builder.AddErrorHandling();

 // Add poison message handling with production settings
 builder.AddProductionPoisonMessageHandling(
 connectionFactory: () => new SqlConnection(connectionString),
 additionalConfig: options =>
 {
 // Customize for specific requirements
 options.MaxRetryAttempts = 3;
 options.AlertThreshold = 25;

 // Add custom poison exception types
 options.PoisonExceptionTypes.Add(typeof(BusinessRuleViolationException));
 options.PoisonExceptionTypes.Add(typeof(DataIntegrityException));

 // Add more transient exception types
 options.TransientExceptionTypes.Add(typeof(SqlException));
 options.TransientExceptionTypes.Add(typeof(HttpRequestException));
 });

 // Add custom poison detector
 builder.AddPoisonDetector<BusinessLogicPoisonDetector>();
 });
 */
		});

	/// <summary>
	///     Example of a custom poison message detector.
	/// </summary>
	public class BusinessLogicPoisonDetector : IPoisonMessageDetector
	{
		public Task<PoisonDetectionResult> IsPoisonMessageAsync(
			IDispatchMessage message,
			IMessageContext context,
			MessageProcessingInfo processingInfo,
			Exception? exception = null)
		{
			// Check for specific business logic violations
			if (message is OrderProcessingMessage order)
			{
				// Invalid order amount
				if (order.TotalAmount < 0)
				{
					return Task.FromResult(PoisonDetectionResult.Poison(
						"Order has negative amount",
						nameof(BusinessLogicPoisonDetector),
						new Dictionary<string, object> { ["OrderId"] = order.OrderId, ["Amount"] = order.TotalAmount }));
				}

				// Order too old to process
				if (order.OrderDate < DateTimeOffset.UtcNow.AddDays(-30))
				{
					return Task.FromResult(PoisonDetectionResult.Poison(
						"Order is too old to process",
						nameof(BusinessLogicPoisonDetector),
						new Dictionary<string, object> { ["OrderId"] = order.OrderId, ["OrderDate"] = order.OrderDate }));
				}
			}

			return Task.FromResult(PoisonDetectionResult.NotPoison());
		}
	}

	/// <summary>
	///     Example service that interacts with poison message handling.
	/// </summary>
	public class MessageProcessingService
	{
		private readonly IPoisonMessageHandler _poisonHandler;
		private readonly IDeadLetterStore _deadLetterStore;
		private readonly ILogger<MessageProcessingService> _logger;

		public MessageProcessingService(
			IPoisonMessageHandler poisonHandler,
			IDeadLetterStore deadLetterStore,
			ILogger<MessageProcessingService> logger)
		{
			_poisonHandler = poisonHandler;
			_deadLetterStore = deadLetterStore;
			_logger = logger;
		}

		/// <summary>
		///     Example of querying and analyzing poison messages.
		/// </summary>
		public async Task AnalyzePoisonMessagesAsync()
		{
			// Get statistics
			var stats = await _poisonHandler.GetStatisticsAsync();
			_logger.LogInformation(
				"Poison message statistics: Total={Total}, Recent={Recent} in last {Window}",
				stats.TotalCount,
				stats.RecentCount,
				stats.TimeWindow);

			// Query specific message types
			var orderMessages = await _deadLetterStore.GetMessagesAsync(new DeadLetterFilter
			{
				MessageType = typeof(OrderProcessingMessage).FullName,
				FromDate = DateTimeOffset.UtcNow.AddDays(-7),
				IsReplayed = false
			});

			foreach (var message in orderMessages)
			{
				_logger.LogInformation(
					"Found poison order: {MessageId}, Reason: {Reason}, Attempts: {Attempts}",
					message.MessageId,
					message.Reason,
					message.ProcessingAttempts);
			}
		}

		/// <summary>
		///     Example of selective message replay based on business rules.
		/// </summary>
		public async Task ReplayRecoverableMessagesAsync()
		{
			var recentMessages = await _deadLetterStore.GetMessagesAsync(new DeadLetterFilter
			{
				FromDate = DateTimeOffset.UtcNow.AddHours(-24),
				IsReplayed = false,
				MaxResults = 50
			});

			foreach (var message in recentMessages)
			{
				// Check if message is recoverable based on reason
				if (IsRecoverable(message))
				{
					_logger.LogInformation(
						"Attempting to replay message {MessageId} with reason: {Reason}",
						message.MessageId,
						message.Reason);

					var success = await _poisonHandler.ReplayMessageAsync(message.MessageId);

					if (success)
					{
						_logger.LogInformation("Successfully replayed message {MessageId}", message.MessageId);
					}
					else
					{
						_logger.LogWarning("Failed to replay message {MessageId}", message.MessageId);
					}
				}
			}
		}

		private bool IsRecoverable(DeadLetterMessage message)
		{
			// Business logic to determine if a message is recoverable
			var recoverableReasons = new[] { "exceeded maximum retry attempts", "timeout", "Service temporarily unavailable" };

			return recoverableReasons.Any(reason =>
				message.Reason.Contains(reason, StringComparison.OrdinalIgnoreCase));
		}
	}

	// Example message types
	public class OrderProcessingMessage : IDispatchMessage
	{
		public string OrderId { get; set; } = string.Empty;
		public decimal TotalAmount { get; set; }
		public DateTimeOffset OrderDate { get; set; }
	}

	public class BusinessRuleViolationException : Exception
	{
		public BusinessRuleViolationException(string message) : base(message)
		{
		}
	}

	public class DataIntegrityException : Exception
	{
		public DataIntegrityException(string message) : base(message)
		{
		}
	}
}
