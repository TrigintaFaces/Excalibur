// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Extension methods for accessing feature interfaces on <see cref="IMessageContext"/>.
/// </summary>
public static class MessageContextFeatureExtensions
{
	/// <summary>
	/// Gets the feature of the specified type from the context's Features dictionary.
	/// </summary>
	/// <typeparam name="TFeature">The feature type.</typeparam>
	/// <param name="context">The message context.</param>
	/// <returns>The feature instance, or <see langword="null"/> if not set.</returns>
	public static TFeature? GetFeature<TFeature>(this IMessageContext context)
		where TFeature : class
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Features.TryGetValue(typeof(TFeature), out var value) ? value as TFeature : null;
	}

	/// <summary>
	/// Sets the feature of the specified type in the context's Features dictionary.
	/// </summary>
	/// <typeparam name="TFeature">The feature type.</typeparam>
	/// <param name="context">The message context.</param>
	/// <param name="feature">The feature instance, or <see langword="null"/> to remove.</param>
	public static void SetFeature<TFeature>(this IMessageContext context, TFeature? feature)
		where TFeature : class
	{
		ArgumentNullException.ThrowIfNull(context);

		if (feature is null)
		{
			context.Features.Remove(typeof(TFeature));
		}
		else
		{
			context.Features[typeof(TFeature)] = feature;
		}
	}

	/// <summary>
	/// Gets or creates the feature of the specified type.
	/// </summary>
	/// <typeparam name="TFeature">The feature interface type.</typeparam>
	/// <typeparam name="TDefault">The default implementation type.</typeparam>
	/// <param name="context">The message context.</param>
	/// <returns>The existing or newly created feature instance.</returns>
	public static TFeature GetOrCreateFeature<TFeature, TDefault>(this IMessageContext context)
		where TFeature : class
		where TDefault : TFeature, new()
	{
		ArgumentNullException.ThrowIfNull(context);

		var features = context.Features;
		if (features.TryGetValue(typeof(TFeature), out var value) && value is TFeature existing)
		{
			return existing;
		}

		var newFeature = new TDefault();
		features[typeof(TFeature)] = newFeature;
		return newFeature;
	}

	/// <summary>
	/// Gets the processing feature, or <see langword="null"/> if not set.
	/// </summary>
	public static IMessageProcessingFeature? GetProcessingFeature(this IMessageContext context) =>
		context.GetFeature<IMessageProcessingFeature>();

	/// <summary>
	/// Gets or creates the processing feature with default implementation.
	/// </summary>
	public static IMessageProcessingFeature GetOrCreateProcessingFeature(this IMessageContext context) =>
		context.GetOrCreateFeature<IMessageProcessingFeature, MessageProcessingFeature>();

	/// <summary>
	/// Gets the validation feature, or <see langword="null"/> if not set.
	/// </summary>
	public static IMessageValidationFeature? GetValidationFeature(this IMessageContext context) =>
		context.GetFeature<IMessageValidationFeature>();

	/// <summary>
	/// Gets or creates the validation feature with default implementation.
	/// </summary>
	public static IMessageValidationFeature GetOrCreateValidationFeature(this IMessageContext context) =>
		context.GetOrCreateFeature<IMessageValidationFeature, MessageValidationFeature>();

	/// <summary>
	/// Gets the timeout feature, or <see langword="null"/> if not set.
	/// </summary>
	public static IMessageTimeoutFeature? GetTimeoutFeature(this IMessageContext context) =>
		context.GetFeature<IMessageTimeoutFeature>();

	/// <summary>
	/// Gets or creates the timeout feature with default implementation.
	/// </summary>
	public static IMessageTimeoutFeature GetOrCreateTimeoutFeature(this IMessageContext context) =>
		context.GetOrCreateFeature<IMessageTimeoutFeature, MessageTimeoutFeature>();

	/// <summary>
	/// Gets the rate limit feature, or <see langword="null"/> if not set.
	/// </summary>
	public static IMessageRateLimitFeature? GetRateLimitFeature(this IMessageContext context) =>
		context.GetFeature<IMessageRateLimitFeature>();

	/// <summary>
	/// Gets or creates the rate limit feature with default implementation.
	/// </summary>
	public static IMessageRateLimitFeature GetOrCreateRateLimitFeature(this IMessageContext context) =>
		context.GetOrCreateFeature<IMessageRateLimitFeature, MessageRateLimitFeature>();

	/// <summary>
	/// Gets the routing feature, or <see langword="null"/> if not set.
	/// </summary>
	public static IMessageRoutingFeature? GetRoutingFeature(this IMessageContext context) =>
		context.GetFeature<IMessageRoutingFeature>();

	/// <summary>
	/// Gets or creates the routing feature with default implementation.
	/// </summary>
	public static IMessageRoutingFeature GetOrCreateRoutingFeature(this IMessageContext context) =>
		context.GetOrCreateFeature<IMessageRoutingFeature, MessageRoutingFeature>();

	/// <summary>
	/// Gets the identity feature, or <see langword="null"/> if not set.
	/// </summary>
	public static IMessageIdentityFeature? GetIdentityFeature(this IMessageContext context) =>
		context.GetFeature<IMessageIdentityFeature>();

	/// <summary>
	/// Gets or creates the identity feature with default implementation.
	/// </summary>
	public static IMessageIdentityFeature GetOrCreateIdentityFeature(this IMessageContext context) =>
		context.GetOrCreateFeature<IMessageIdentityFeature, MessageIdentityFeature>();

	/// <summary>
	/// Gets the transaction feature, or <see langword="null"/> if not set.
	/// </summary>
	public static IMessageTransactionFeature? GetTransactionFeature(this IMessageContext context) =>
		context.GetFeature<IMessageTransactionFeature>();

	/// <summary>
	/// Gets or creates the transaction feature with default implementation.
	/// </summary>
	public static IMessageTransactionFeature GetOrCreateTransactionFeature(this IMessageContext context) =>
		context.GetOrCreateFeature<IMessageTransactionFeature, MessageTransactionFeature>();

	// ===== Convenience property accessors (backward-compat friendly) =====

	/// <summary>
	/// Gets the user ID from the identity feature.
	/// </summary>
	public static string? GetUserId(this IMessageContext context) =>
		context.GetIdentityFeature()?.UserId;

	/// <summary>
	/// Gets the tenant ID from the identity feature.
	/// </summary>
	public static string? GetTenantId(this IMessageContext context) =>
		context.GetIdentityFeature()?.TenantId;

	/// <summary>
	/// Gets the session ID from the identity feature.
	/// </summary>
	public static string? GetSessionId(this IMessageContext context) =>
		context.GetIdentityFeature()?.SessionId;

	/// <summary>
	/// Gets the workflow ID from the identity feature.
	/// </summary>
	public static string? GetWorkflowId(this IMessageContext context) =>
		context.GetIdentityFeature()?.WorkflowId;

	/// <summary>
	/// Gets the external ID from the identity feature.
	/// </summary>
	public static string? GetExternalId(this IMessageContext context) =>
		context.GetIdentityFeature()?.ExternalId;

	/// <summary>
	/// Gets the trace parent from the identity feature.
	/// </summary>
	public static string? GetTraceParent(this IMessageContext context) =>
		context.GetIdentityFeature()?.TraceParent;

	/// <summary>
	/// Gets the partition key from the routing feature.
	/// </summary>
	public static string? GetPartitionKey(this IMessageContext context) =>
		context.GetRoutingFeature()?.PartitionKey;

	/// <summary>
	/// Gets the source from the routing feature.
	/// </summary>
	public static string? GetSource(this IMessageContext context) =>
		context.GetRoutingFeature()?.Source;

	/// <summary>
	/// Gets the processing attempts from the processing feature.
	/// </summary>
	public static int GetProcessingAttempts(this IMessageContext context) =>
		context.GetProcessingFeature()?.ProcessingAttempts ?? 0;

	/// <summary>
	/// Gets whether this is a retry from the processing feature.
	/// </summary>
	public static bool GetIsRetry(this IMessageContext context) =>
		context.GetProcessingFeature()?.IsRetry ?? false;

	/// <summary>
	/// Gets the delivery count from the processing feature.
	/// </summary>
	public static int GetDeliveryCount(this IMessageContext context) =>
		context.GetProcessingFeature()?.DeliveryCount ?? 0;

	/// <summary>
	/// Gets the first attempt time from the processing feature.
	/// </summary>
	public static DateTimeOffset? GetFirstAttemptTime(this IMessageContext context) =>
		context.GetProcessingFeature()?.FirstAttemptTime;

	// ===== Routing convenience accessors =====

	/// <summary>
	/// Gets the routing decision from the routing feature.
	/// </summary>
	public static RoutingDecision? GetRoutingDecision(this IMessageContext context) =>
		context.GetRoutingFeature()?.RoutingDecision;

	// ===== Timeout convenience accessors =====

	/// <summary>
	/// Gets whether the timeout was exceeded from the timeout feature.
	/// </summary>
	public static bool GetTimeoutExceeded(this IMessageContext context) =>
		context.GetTimeoutFeature()?.TimeoutExceeded ?? false;

	/// <summary>
	/// Gets the timeout elapsed from the timeout feature.
	/// </summary>
	public static TimeSpan? GetTimeoutElapsed(this IMessageContext context) =>
		context.GetTimeoutFeature()?.TimeoutElapsed;

	// ===== Rate limit convenience accessors =====

	/// <summary>
	/// Gets whether the rate limit was exceeded from the rate limit feature.
	/// </summary>
	public static bool GetRateLimitExceeded(this IMessageContext context) =>
		context.GetRateLimitFeature()?.RateLimitExceeded ?? false;

	/// <summary>
	/// Gets the rate limit retry-after duration from the rate limit feature.
	/// </summary>
	public static TimeSpan? GetRateLimitRetryAfter(this IMessageContext context) =>
		context.GetRateLimitFeature()?.RateLimitRetryAfter;

	// ===== Validation convenience accessors =====

	/// <summary>
	/// Gets whether validation passed from the validation feature.
	/// </summary>
	public static bool GetValidationPassed(this IMessageContext context) =>
		context.GetValidationFeature()?.ValidationPassed ?? false;

	/// <summary>
	/// Gets the validation timestamp from the validation feature.
	/// </summary>
	public static DateTimeOffset? GetValidationTimestamp(this IMessageContext context) =>
		context.GetValidationFeature()?.ValidationTimestamp;

	// ===== Transaction convenience accessors =====

	/// <summary>
	/// Gets the transaction object from the transaction feature.
	/// </summary>
	public static object? GetTransaction(this IMessageContext context) =>
		context.GetTransactionFeature()?.Transaction;

	/// <summary>
	/// Gets the transaction ID from the transaction feature.
	/// </summary>
	public static string? GetTransactionId(this IMessageContext context) =>
		context.GetTransactionFeature()?.TransactionId;

	/// <summary>
	/// Creates a child context for dispatching related messages.
	/// Propagates cross-cutting identifiers (correlation, identity) from the parent.
	/// </summary>
	/// <param name="context">The parent message context.</param>
	/// <param name="requestServices">The service provider for the child context.</param>
	/// <returns>A new <see cref="IMessageContext"/> with propagated identifiers.</returns>
	public static IMessageContext CreateChildContext(this IMessageContext context, IServiceProvider? requestServices = null)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Use a simple dictionary-based implementation for child contexts
		var child = new ChildMessageContext(requestServices ?? context.RequestServices)
		{
			CorrelationId = context.CorrelationId,
			CausationId = context.MessageId ?? context.CorrelationId,
			MessageId = Guid.NewGuid().ToString(),
		};

		// Propagate identity feature
		var parentIdentity = context.GetIdentityFeature();
		if (parentIdentity is not null)
		{
			child.SetFeature<IMessageIdentityFeature>(new MessageIdentityFeature
			{
				UserId = parentIdentity.UserId,
				TenantId = parentIdentity.TenantId,
				SessionId = parentIdentity.SessionId,
				WorkflowId = parentIdentity.WorkflowId,
				ExternalId = parentIdentity.ExternalId,
				TraceParent = parentIdentity.TraceParent,
			});
		}

		// Propagate routing source
		var parentRouting = context.GetRoutingFeature();
		if (parentRouting?.Source is not null)
		{
			child.SetFeature<IMessageRoutingFeature>(new MessageRoutingFeature
			{
				Source = parentRouting.Source,
			});
		}

		return child;
	}

	/// <summary>
	/// Lightweight message context for child/related messages.
	/// </summary>
	private sealed class ChildMessageContext(IServiceProvider requestServices) : IMessageContext
	{
		private Dictionary<string, object>? _items;
		private Dictionary<Type, object>? _features;

		public string? MessageId { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public IDispatchMessage? Message { get; set; }
		public object? Result { get; set; }
		public IServiceProvider RequestServices { get; set; } = requestServices;
		public IDictionary<string, object> Items => _items ??= new Dictionary<string, object>(StringComparer.Ordinal);
		public IDictionary<Type, object> Features => _features ??= [];
	}
}
