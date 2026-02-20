// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Example demonstrating pipeline profiles configuration and usage

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Abstractions.Pipeline;
using Excalibur.Dispatch.Messaging.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging.Pipeline;
using Excalibur.Dispatch.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace examples.PipelineProfiles;

/// <summary>
///     Example demonstrating how to configure and use pipeline profiles.
/// </summary>
public class PipelineProfileExample
{
	public static void ConfigureServices(IServiceCollection services)
	{
		// Configure dispatch with multiple pipeline profiles
		// Note: Since Sprint 70, correlation is automatically handled at the Dispatcher level.
		// No CorrelationMiddleware is needed in pipeline configurations.
		services.AddDispatch(dispatch => dispatch
			// Define a strict pipeline for critical operations
			.AddPipeline("strict", pipeline => pipeline
				.WithDescription("Strict validation pipeline for commands and queries")
				.ForMessageKinds(MessageKinds.Action)
				.Use<ContextEnrichmentMiddleware>()
				.Use<TenantIdentityMiddleware>()
				.Use<AuthorizationMiddleware>()
				.Use<ValidationMiddleware>()
				.Use<TransactionMiddleware>()
				.Use<OutboxStagingMiddleware>()
				.Use<MetricsLoggingMiddleware>())

			// Define an optimized pipeline for internal events
			.AddPipeline("internal-event", pipeline => pipeline
				.WithDescription("Optimized pipeline for internal event processing")
				.ForMessageKinds(MessageKinds.Event)
				.Use<TenantIdentityMiddleware>()
				.Use<ContractVersionCheckMiddleware>()
				.Use<TimeoutMiddleware>()
				.Use<OutboxStagingMiddleware>()
				.Use<MetricsLoggingMiddleware>())

			// Define a high-throughput pipeline with minimal overhead
			.AddPipeline("high-throughput", pipeline => pipeline
				.WithDescription("Minimal overhead for high-volume processing")
				.ForMessageKinds(MessageKinds.All)
				.Use<MetricsLoggingMiddleware>())

			// Define a debug pipeline with extensive logging
			.AddPipeline("debug", pipeline => pipeline
				.WithDescription("Pipeline with extensive debugging capabilities")
				.ForMessageKinds(MessageKinds.All)
				.Use<DebugLoggingMiddleware>()
				.Use<TimingMiddleware>()
				.Use<ValidationMiddleware>()
				.Use<ErrorLoggingMiddleware>()
				.Use<MetricsLoggingMiddleware>())

			// Configure default profiles for different message kinds
			.WithPipelineOptions(options =>
			{
				options.DefaultProfiles.Action = "strict";
				options.DefaultProfiles.Event = "internal-event";
				options.DefaultProfiles.Document = "high-throughput";
				options.DefaultProfiles.Fallback = "high-throughput";
				options.EnableProfileCaching = true;
				options.AllowFallbackToDefaultPipeline = true;
			})

			// Enable the profile-aware pipeline
			.UseProfileAwarePipeline());

		// Register example middleware implementations
		services.AddScoped<ContextEnrichmentMiddleware>();
		services.AddScoped<TenantIdentityMiddleware>();
		services.AddScoped<AuthorizationMiddleware>();
		services.AddScoped<ValidationMiddleware>();
		services.AddScoped<TransactionMiddleware>();
		services.AddScoped<OutboxStagingMiddleware>();
		services.AddScoped<MetricsLoggingMiddleware>();
		services.AddScoped<ContractVersionCheckMiddleware>();
		services.AddScoped<TimeoutMiddleware>();
		services.AddScoped<DebugLoggingMiddleware>();
		services.AddScoped<TimingMiddleware>();
		services.AddScoped<ErrorLoggingMiddleware>();
	}

	/// <summary>
	///     Example showing how to explicitly specify a pipeline profile in the message context.
	/// </summary>
	public static async Task ExplicitProfileSelection(IServiceProvider services)
	{
		var dispatcher = services.GetRequiredService<IDispatcher>();

		// Create a command that would normally use the "strict" profile
		var command = new CreateOrderCommand { /* ... */ };

		// Override to use the high-throughput profile for this specific dispatch
		var context = new MessageContext();
		context.Items[DefaultPipelineProfileSelector.PipelineProfileContextKey] = "high-throughput";

		await dispatcher.DispatchAsync(command, context, CancellationToken.None);
	}

	/// <summary>
	///     Example showing conditional middleware in profiles.
	/// </summary>
	public static void ConditionalMiddlewareExample(IServiceCollection services)
	{
		var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

		// Note: Since Sprint 70, correlation is handled automatically by Dispatcher
		services.AddDispatch(dispatch => dispatch
			.AddPipeline("adaptive", pipeline => pipeline
				.WithDescription("Pipeline that adapts based on environment")
				.Use<ContextEnrichmentMiddleware>()
				// Only add debug middleware in development
				.UseWhen<DebugLoggingMiddleware>(isDevelopment)
				// Only add authorization in production
				.UseWhen<AuthorizationMiddleware>(!isDevelopment)
				.Use<MetricsLoggingMiddleware>()));
	}

	/// <summary>
	///     Example showing profile inheritance/composition.
	/// </summary>
	public static void ProfileCompositionExample(IServiceCollection services)
	{
		// Note: Since Sprint 70, correlation is handled automatically by Dispatcher
		services.AddDispatch(dispatch => dispatch
			// Base profile with common middleware
			.AddPipeline("base", pipeline => pipeline
				.Use<ContextEnrichmentMiddleware>()
				.Use<TenantIdentityMiddleware>()
				.Use<MetricsLoggingMiddleware>())

			// Extended profile that builds on the base
			.AddPipelineFrom("extended", "base", pipeline => pipeline
				.Use<ValidationMiddleware>()
				.Use<AuthorizationMiddleware>())

			// Another variant with different additions
			.AddPipelineFrom("performance", "base", pipeline => pipeline
				.Use<CachingMiddleware>()
				.Use<CompressionMiddleware>()));
	}

	/// <summary>
	///     Example showing dynamic profile selection based on message attributes.
	/// </summary>
	public class CustomProfileSelector : IPipelineProfileSelector
	{
		private readonly IPipelineProfileRegistry _registry;

		public CustomProfileSelector(IPipelineProfileRegistry registry)
		{
			_registry = registry;
		}

		public IPipelineProfile? SelectProfile(IDispatchMessage message, IMessageContext context)
		{
			// Check for priority messages
			if (message is IPriorityMessage priorityMessage && priorityMessage.Priority == Priority.High)
			{
				return _registry.GetProfile("high-priority");
			}

			// Check for bulk operations
			if (message is IBulkOperation)
			{
				return _registry.GetProfile("bulk-processing");
			}

			// Fall back to default selection logic
			return null;
		}

		public IPipelineProfile? SelectProfile(Type messageType)
		{
			// Type-based selection logic
			if (typeof(IPriorityMessage).IsAssignableFrom(messageType))
			{
				return _registry.GetProfile("high-priority");
			}

			return null;
		}

		public IPipelineProfile? GetDefaultProfile(MessageKinds messageKind)
		{
			var profileName = _registry.GetDefaultProfileName(messageKind);
			return profileName != null ? _registry.GetProfile(profileName) : null;
		}
	}
}

// Example middleware implementations (simplified)
// Note: As of Sprint 70, correlation handling is built into the Dispatcher itself.
// This example middleware demonstrates the pattern but correlation IDs are now
// automatically managed at the Dispatcher entry point.
public class ContextEnrichmentMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Example: Add custom context enrichment
		// Note: CorrelationId is now set automatically by Dispatcher
		context.Items["ProcessedBy"] = Environment.MachineName;
		return next(message, context, cancellationToken);
	}
}

public class TenantIdentityMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Extract and validate tenant identity
		return next(message, context, cancellationToken);
	}
}

public class AuthorizationMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Perform authorization checks
		return next(message, context, cancellationToken);
	}
}

public class ValidationMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Validate message
		return next(message, context, cancellationToken);
	}
}

public class TransactionMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

	public async Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Begin transaction
		var result = await next(message, context, cancellationToken);
		// Commit or rollback based on result
		return result;
	}
}

public class OutboxStagingMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Stage messages in outbox
		return next(message, context, cancellationToken);
	}
}

public class MetricsLoggingMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public async Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		var startTime = DateTime.UtcNow;
		var result = await next(message, context, cancellationToken);
		var duration = DateTime.UtcNow - startTime;
		// Log metrics
		return result;
	}
}

public class ContractVersionCheckMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;
	public MessageKinds ApplicableMessageKinds => MessageKinds.Event;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Check message contract version
		return next(message, context, cancellationToken);
	}
}

public class TimeoutMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public async Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(TimeSpan.FromSeconds(30));
		return await next(message, context, cts.Token);
	}
}

public class DebugLoggingMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		Console.WriteLine($"Processing message: {message.GetType().Name}");
		return next(message, context, cancellationToken);
	}
}

public class TimingMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public async Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		var sw = ValueStopwatch.StartNew();
		var result = await next(message, context, cancellationToken);
		context.Items["ProcessingTime"] = (long)sw.Elapsed.TotalMilliseconds;
		return result;
	}
}

public class ErrorLoggingMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public async Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		try
		{
			return await next(message, context, cancellationToken);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error processing message: {ex}");
			throw;
		}
	}
}

public class CachingMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Check cache and potentially return cached result
		return next(message, context, cancellationToken);
	}
}

public class CompressionMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Serialization;
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public Task<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		CancellationToken cancellationToken = default)
	{
		// Apply compression if needed
		return next(message, context, cancellationToken);
	}
}

// Example message types
public class CreateOrderCommand : IDispatchAction { }
public interface IPriorityMessage : IDispatchMessage
{
	Priority Priority { get; }
}
public enum Priority { Low, Normal, High, Critical }
public interface IBulkOperation : IDispatchMessage { }
public class MessageContext : Excalibur.Dispatch.Messaging.MessageContext
{
	public MessageContext() : base(new ExampleMessage(), new ServiceCollection().BuildServiceProvider())
	{
	}

	private sealed class ExampleMessage : IDispatchMessage
	{
		public string MessageId => Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
		public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>();
		public object Body => new();
		public string MessageType => nameof(ExampleMessage);
		public Microsoft.AspNetCore.Http.Features.IFeatureCollection Features => new Microsoft.AspNetCore.Http.Features.FeatureCollection();
		public Guid Id => Guid.NewGuid();
		public MessageKinds Kind => MessageKinds.Action;
	}
}
// Note: Use IDispatcher from Excalibur.Dispatch.Abstractions instead of IMessageDispatcher
// The IMessageDispatcher interface was removed in Sprint 70 as part of the pipeline infrastructure consolidation.