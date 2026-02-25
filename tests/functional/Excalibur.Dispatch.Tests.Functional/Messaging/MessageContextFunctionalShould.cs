// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;


using IAuthorizationResult = Excalibur.Dispatch.Abstractions.IAuthorizationResult;
using IValidationResult = Excalibur.Dispatch.Abstractions.Validation.IValidationResult;

namespace Excalibur.Dispatch.Tests.Functional.Messaging;

#pragma warning disable CA1034 // Nested types should not be visible

/// <summary>
///     Functional tests for MessageContext testing complete scenarios end-to-end.
/// </summary>
[Trait("Category", "Functional")]
public sealed class MessageContextFunctionalShould : FunctionalTestBase
{
	// Interfaces for the processing pipeline
	public interface IMessageProcessor
	{
		Task<ProcessingResult> ProcessMessageAsync(TestMessage message, CancellationToken cancellationToken = default);
	}

	public interface IMessageEnricher
	{
		Task EnrichAsync(IMessageContext context, TestMessage message, CancellationToken cancellationToken = default);
	}

	public interface IMessageValidator
	{
		Task<SerializableValidationResult> ValidateAsync(IMessageContext context, TestMessage message,
			CancellationToken cancellationToken = default);
	}

	public interface IMessageAuthorizer
	{
		Task<AuthorizationResult> AuthorizeAsync(IMessageContext context, TestMessage message,
			CancellationToken cancellationToken = default);
	}

	public interface IMessageHandler
	{
		string Name { get; }

		Task HandleAsync(IMessageContext context, TestMessage message, CancellationToken cancellationToken = default);
	}

	[Fact]
	public async Task ProcessCompleteMessageWorkflow()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(builder => builder.AddConsole());
		_ = services.AddSingleton<IMessageProcessor, MessageProcessor>();
		_ = services.AddSingleton<IMessageEnricher, MessageEnricher>();
		_ = services.AddSingleton<IMessageValidator, MessageValidator>();
		_ = services.AddSingleton<IMessageAuthorizer, MessageAuthorizer>();
		_ = services.AddSingleton<IDispatchRouter, MessageRouter>();
		_ = services.AddSingleton<IMessageHandler, TestMessageHandler>();

		var serviceProvider = services.BuildServiceProvider();
		var processor = serviceProvider.GetRequiredService<IMessageProcessor>();

		var testMessage = new TestMessage
		{
			MessageId = Guid.NewGuid(),
			Content = "Test message content",
			UserId = "user-123",
			TenantId = "tenant-456",
		};

		// Act - Process message with timeout
		var result = await RunWithTimeoutAsync(ct => processor.ProcessMessageAsync(testMessage, ct))
			.ConfigureAwait(true);

		// Assert
		result.Success.ShouldBeTrue();
		result.ProcessingTime.ShouldBeGreaterThan(TimeSpan.Zero);
		result.HandledBy.ShouldBe("TestMessageHandler");
		result.Context.MessageId.ShouldNotBeNullOrEmpty();
		result.Context.Success.ShouldBeTrue();
		result.Context.GetItem<string>("Enriched").ShouldBe("true");
		result.Context.GetItem<string>("Validated").ShouldBe("true");
		result.Context.GetItem<string>("Authorized").ShouldBe("true");
		result.Context.GetItem<string>("Routed").ShouldBe("true");
		result.Context.GetItem<string>("Handled").ShouldBe("true");
	}

	[Fact]
	public async Task HandleFailuresGracefully()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IMessageProcessor, MessageProcessor>();
		_ = services.AddSingleton<IMessageEnricher, MessageEnricher>();
		_ = services.AddSingleton<IMessageValidator, MessageValidator>();
		_ = services.AddSingleton<IMessageAuthorizer, MessageAuthorizer>();
		_ = services.AddSingleton<IDispatchRouter, MessageRouter>();
		_ = services.AddSingleton<IMessageHandler, FailingMessageHandler>();

		var serviceProvider = services.BuildServiceProvider();
		var processor = serviceProvider.GetRequiredService<IMessageProcessor>();

		var testMessage = new TestMessage
		{
			MessageId = Guid.NewGuid(),
			Content = "FAIL", // Trigger failure
			UserId = "user-123",
			TenantId = "tenant-456",
		};

		// Act - Process failing message with timeout
		var result = await RunWithTimeoutAsync(ct => processor.ProcessMessageAsync(testMessage, ct))
			.ConfigureAwait(true);

		// Assert
		result.Success.ShouldBeFalse();
		result.Error.ShouldNotBeNullOrEmpty();
		result.Context.Success.ShouldBeFalse();
		result.Context.ValidationResult.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task MeasurePerformanceUnderLoad()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IMessageProcessor, MessageProcessor>();
		_ = services.AddSingleton<IMessageEnricher, MessageEnricher>();
		_ = services.AddSingleton<IMessageValidator, MessageValidator>();
		_ = services.AddSingleton<IMessageAuthorizer, MessageAuthorizer>();
		_ = services.AddSingleton<IDispatchRouter, MessageRouter>();
		_ = services.AddSingleton<IMessageHandler, TestMessageHandler>();

		var serviceProvider = services.BuildServiceProvider();
		var processor = serviceProvider.GetRequiredService<IMessageProcessor>();

		const int MessageCount = 1000;
		var messages = Enumerable.Range(0, MessageCount)
			.Select(i => new TestMessage
			{
				MessageId = Guid.NewGuid(),
				Content = $"Message {i}",
				UserId = $"user-{i % 10}",
				TenantId = $"tenant-{i % 5}",
			})
			.ToList();

		// Act - Process all messages with timeout
		var stopwatch = Stopwatch.StartNew();
		var results = await RunWithTimeoutAsync(async ct =>
		{
			var tasks = messages.Select(msg => processor.ProcessMessageAsync(msg, ct));
			return await Task.WhenAll(tasks).ConfigureAwait(true);
		}).ConfigureAwait(true);
		stopwatch.Stop();

		// Assert
		results.Length.ShouldBe(MessageCount);
		results.Count(r => r.Success).ShouldBe(MessageCount);

		var throughput = MessageCount / stopwatch.Elapsed.TotalSeconds;
		Console.WriteLine($"Processed {MessageCount} messages in {stopwatch.ElapsedMilliseconds}ms");
		Console.WriteLine($"Throughput: {throughput:F2} messages/second");

		// Should process at least 100 messages per second
		throughput.ShouldBeGreaterThan(100);
	}

	// Test message and result classes
	/// <summary>
	/// Test message - IDispatchMessage is now a marker interface.
	/// </summary>
	public sealed class TestMessage : IDispatchMessage
	{
		public Guid MessageId { get; set; }
		public required string Content { get; set; } = string.Empty;
		public required string UserId { get; set; } = string.Empty;
		public required string TenantId { get; set; } = string.Empty;
	}

	public sealed class ProcessingResult
	{
		public bool Success { get; set; }

		public required string Error { get; set; }

		public TimeSpan ProcessingTime { get; set; }

		public required string HandledBy { get; set; }

		public required MessageContext Context { get; set; } = null!;
	}

	// Implementations
	public sealed class MessageProcessor(IServiceProvider serviceProvider, ILogger<MessageProcessor> logger)
		: IMessageProcessor
	{
		/// <inheritdoc/>
		public async Task<ProcessingResult> ProcessMessageAsync(TestMessage message, CancellationToken cancellationToken = default)
		{
			var stopwatch = Stopwatch.StartNew();
			var context = new MessageContext(message, serviceProvider);

			try
			{
				// Enrichment
				var enricher = serviceProvider.GetRequiredService<IMessageEnricher>();
				await enricher.EnrichAsync(context, message, cancellationToken).ConfigureAwait(true);

				// Validation
				var validator = serviceProvider.GetRequiredService<IMessageValidator>();
				var validationResult = await validator.ValidateAsync(context, message, cancellationToken).ConfigureAwait(true);
				context.ValidationResult = validationResult;

				if (!validationResult.IsValid)
				{
					return new ProcessingResult
					{
						Success = false,
						Error = "Validation failed",
						HandledBy = string.Empty,
						ProcessingTime = stopwatch.Elapsed,
						Context = context,
					};
				}

				// Authorization
				var authorizer = serviceProvider.GetRequiredService<IMessageAuthorizer>();
				var authResult = await authorizer.AuthorizeAsync(context, message, cancellationToken).ConfigureAwait(true);
				context.AuthorizationResult = authResult;

				if (!authResult.IsAuthorized)
				{
					return new ProcessingResult
					{
						Success = false,
						Error = "Authorization failed",
						HandledBy = string.Empty,
						ProcessingTime = stopwatch.Elapsed,
						Context = context,
					};
				}

				// Routing
				var router = serviceProvider.GetRequiredService<IDispatchRouter>();
				var routingDecision = await router
						.RouteAsync(message, context, cancellationToken)
						.ConfigureAwait(true);
				context.RoutingDecision = routingDecision;

				if (!routingDecision.IsSuccess)
				{
					return new ProcessingResult
					{
						Success = false,
						Error = "Routing failed",
						HandledBy = string.Empty,
						ProcessingTime = stopwatch.Elapsed,
						Context = context,
					};
				}

				// Handling
				var handler = serviceProvider.GetRequiredService<IMessageHandler>();
				await handler.HandleAsync(context, message, cancellationToken).ConfigureAwait(true);

				return new ProcessingResult
				{
					Success = true,
					Error = string.Empty,
					ProcessingTime = stopwatch.Elapsed,
					HandledBy = handler.Name,
					Context = context,
				};
			}
			catch (Exception ex)
			{
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
				logger.LogError(ex, "Error processing message {MessageId}", context.MessageId);
#pragma warning restore CA1848 // Use LoggerMessage delegates for performance
				return new ProcessingResult
				{
					Success = false,
					Error = ex.Message,
					HandledBy = string.Empty,
					ProcessingTime = stopwatch.Elapsed,
					Context = context,
				};
			}
		}
	}

	public sealed class MessageEnricher : IMessageEnricher
	{
		/// <inheritdoc/>
		public Task EnrichAsync(IMessageContext context, TestMessage message, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(context);
			ArgumentNullException.ThrowIfNull(message);

			context.MessageId = message.MessageId.ToString();
			context.UserId = message.UserId;
			context.TenantId = message.TenantId;
			context.MessageType = nameof(TestMessage);
			context.Source = "FunctionalTest";
			context.ReceivedTimestampUtc = DateTimeOffset.UtcNow;
			context.SetItem("Enriched", "true");
			context.SetItem("OriginalContent", message.Content);

			return Task.CompletedTask;
		}
	}

	public sealed class MessageValidator : IMessageValidator
	{
		/// <inheritdoc/>
		public Task<SerializableValidationResult> ValidateAsync(IMessageContext context, TestMessage message,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(context);
			ArgumentNullException.ThrowIfNull(message);

			var errors = new List<string>();

			if (message.Content == "FAIL")
			{
				errors.Add("Content contains forbidden value");
			}

			if (string.IsNullOrWhiteSpace(message.UserId))
			{
				errors.Add("UserId is required");
			}

			if (string.IsNullOrWhiteSpace(message.TenantId))
			{
				errors.Add("TenantId is required");
			}

			context.SetItem("Validated", "true");

			return Task.FromResult(errors.Count > 0
				? SerializableValidationResult.Failed([.. errors])
				: SerializableValidationResult.Success());
		}
	}

	public sealed class MessageAuthorizer : IMessageAuthorizer
	{
		/// <inheritdoc/>
		public Task<AuthorizationResult> AuthorizeAsync(IMessageContext context, TestMessage message,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(context);
			ArgumentNullException.ThrowIfNull(message);

			// Simulate authorization logic - cast to MessageContext to access property
			var msgContext = (MessageContext)context;
			var isAuthorized = msgContext.ValidationResult.IsValid &&
								 !string.IsNullOrEmpty(context.UserId);

			context.SetItem("Authorized", isAuthorized ? "true" : "false");

			return Task.FromResult(isAuthorized
				? AuthorizationResult.Success()
				: AuthorizationResult.Failed("User not authorized"));
		}
	}

	public sealed class MessageRouter : IDispatchRouter
	{
		/// <inheritdoc/>
		public ValueTask<RoutingDecision> RouteAsync(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(message);
			ArgumentNullException.ThrowIfNull(context);
			cancellationToken.ThrowIfCancellationRequested();

			// Simulate routing logic - cast to MessageContext to access property
			var msgContext = (MessageContext)context;
			if (msgContext.AuthorizationResult.IsAuthorized)
			{
				context.SetItem("Routed", "true");
				context.SetItem("TargetHandler", nameof(TestMessageHandler));
				return ValueTask.FromResult(RoutingDecision.Success("local", [nameof(TestMessageHandler)]));
			}

			return ValueTask.FromResult(RoutingDecision.Failure("No route found"));
		}

		/// <inheritdoc/>
		public bool CanRouteTo(IDispatchMessage message, string destination)
		{
			ArgumentNullException.ThrowIfNull(message);
			ArgumentException.ThrowIfNullOrWhiteSpace(destination);

			// Simple implementation - only routes to "local" transport or TestMessageHandler endpoint
			return destination == "local" || destination == nameof(TestMessageHandler);
		}

		/// <inheritdoc/>
		public IEnumerable<Abstractions.Routing.RouteInfo> GetAvailableRoutes(IDispatchMessage message, IMessageContext context)
		{
			ArgumentNullException.ThrowIfNull(message);
			ArgumentNullException.ThrowIfNull(context);

			// Return single route for diagnostics
			yield return new Abstractions.Routing.RouteInfo("local", nameof(TestMessageHandler));
		}
	}

	public sealed class TestMessageHandler : IMessageHandler
	{
		/// <inheritdoc/>
		public string Name => nameof(TestMessageHandler);

		/// <inheritdoc/>
		public async Task HandleAsync(IMessageContext context, TestMessage message, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(context);
			ArgumentNullException.ThrowIfNull(message);

			context.SetItem("Handled", "true");
			context.SetItem("HandledAt", DateTimeOffset.UtcNow);
			context.SetItem("ProcessedContent", message.Content.ToUpperInvariant());

			// Simulate some processing
			await Task.Delay(10, cancellationToken).ConfigureAwait(true);
		}
	}

	public sealed class FailingMessageHandler : IMessageHandler
	{
		/// <inheritdoc/>
		public string Name => nameof(FailingMessageHandler);

		/// <inheritdoc/>
		public Task HandleAsync(IMessageContext context, TestMessage message, CancellationToken cancellationToken = default) =>
			throw new InvalidOperationException("Handler failed intentionally");
	}
}
