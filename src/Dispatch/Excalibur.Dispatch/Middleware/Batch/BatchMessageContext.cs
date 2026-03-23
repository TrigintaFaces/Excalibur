// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Middleware.Batch;

/// <summary>
/// A composite message context that represents a batch of contexts.
/// </summary>
internal sealed class BatchMessageContext : IMessageContext
{
	private static readonly IMessageVersionMetadata DefaultVersionMetadata = new MessageVersionMetadata();
	private static readonly IValidationResult DefaultValidationResult = SerializableValidationResult.Success();
	private static readonly IAuthorizationResult DefaultAuthorizationResult = Abstractions.AuthorizationResult.Success();
	public BatchMessageContext(IList<IMessageContext> contexts)
	{
		if (contexts.Count == 0)
		{
			throw new InvalidOperationException("Cannot create batch context from empty context list.");
		}

		Contexts = contexts;
		Items = new Dictionary<string, object>(StringComparer.Ordinal);
		Features = new Dictionary<Type, object>();

		// Use first context as primary context for batch-level properties
		var primaryContext = contexts[0];
		MessageId = primaryContext.MessageId;
		CorrelationId = primaryContext.CorrelationId;
		CausationId = primaryContext.CausationId;
		RequestServices = primaryContext.RequestServices;

		// Copy features from primary context
		foreach (var kvp in primaryContext.Features)
		{
			Features[kvp.Key] = kvp.Value;
		}

		// Copy Items metadata
		SerializerVersion = primaryContext.SerializerVersion();
		MessageVersion = primaryContext.MessageVersion();
		ContractVersion = primaryContext.ContractVersion();
		DesiredVersion = int.TryParse(primaryContext.DesiredVersion(), out var version) ? version : null;
		PartitionKey = primaryContext.PartitionKey();
		ReplyTo = primaryContext.ReplyTo();
		MessageType = "BatchMessage";
		ContentType = primaryContext.GetContentType();
		VersionMetadata = primaryContext.VersionMetadata() as IMessageVersionMetadata;
		ValidationResult = primaryContext.ValidationResult() as IValidationResult;
		AuthorizationResult = primaryContext.AuthorizationResult() as IAuthorizationResult;
		Metadata = primaryContext.Metadata() as IMessageMetadata;
	}

	public IList<IMessageContext> Contexts { get; }

	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the external identifier.
	/// </summary>
	public string? ExternalId { get; set; }

	/// <summary>
	/// Gets or sets the user identifier.
	/// </summary>
	public string? UserId { get; set; }

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public string? CausationId { get; set; }

	/// <summary>
	/// Gets or sets the trace parent for distributed tracing.
	/// </summary>
	public string? TraceParent { get; set; }

	/// <summary>
	/// Gets or sets the serializer version.
	/// </summary>
	public string? SerializerVersion { get; set; }

	/// <summary>
	/// Gets or sets the message version.
	/// </summary>
	public string? MessageVersion { get; set; }

	/// <summary>
	/// Gets or sets the contract version.
	/// </summary>
	public string? ContractVersion { get; set; }

	/// <summary>
	/// Gets or sets the desired version.
	/// </summary>
	public int? DesiredVersion { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier.
	/// </summary>
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the session identifier for message grouping and ordering.
	/// </summary>
	public string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the workflow identifier for saga orchestration.
	/// </summary>
	public string? WorkflowId { get; set; }

	/// <summary>
	/// Gets or sets the message source.
	/// </summary>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the content type.
	/// </summary>
	public string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the delivery count.
	/// </summary>
	public int DeliveryCount { get; set; }

	/// <summary>
	/// Gets or sets the partition key.
	/// </summary>
	public string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the reply-to address.
	/// </summary>
	public string? ReplyTo { get; set; }

	/// <summary>
	/// Gets or sets the version metadata.
	/// </summary>
	public IMessageVersionMetadata? VersionMetadata { get; set; }

	/// <inheritdoc />
	public IDispatchMessage? Message { get; set; }

	/// <inheritdoc />
	public object? Result { get; set; }

	/// <summary>
	/// Gets or sets the validation result.
	/// </summary>
	public IValidationResult? ValidationResult { get; set; }

	/// <summary>
	/// Gets or sets the authorization result.
	/// </summary>
	public IAuthorizationResult? AuthorizationResult { get; set; }

	/// <summary>
	/// Gets or sets the routing decision.
	/// </summary>
	public RoutingDecision? RoutingDecision { get; set; } =
		Abstractions.Routing.RoutingDecision.Local;

	/// <inheritdoc />
	public IServiceProvider RequestServices { get; set; }

	/// <summary>
	/// Gets or sets the received timestamp.
	/// </summary>
	public DateTimeOffset ReceivedTimestampUtc { get; set; }

	/// <summary>
	/// Gets or sets the sent timestamp.
	/// </summary>
	public DateTimeOffset? SentTimestampUtc { get; set; }

	/// <summary>
	/// Gets or sets the message metadata.
	/// </summary>
	public IMessageMetadata? Metadata { get; set; }

	/// <inheritdoc />
	public IDictionary<string, object> Items { get; }

	/// <inheritdoc />
	public IDictionary<Type, object> Features { get; }

	/// <summary>
	/// Gets a value indicating whether both validation and authorization succeeded.
	/// </summary>
	public bool Success => ValidationResult?.IsValid == true &&
						   AuthorizationResult?.IsAuthorized == true;

	// ========================================== LEGACY PROPERTIES ==========================================

	/// <summary>
	/// Gets or sets the processing attempts count.
	/// </summary>
	public int ProcessingAttempts { get; set; }

	/// <summary>
	/// Gets or sets the time of the first processing attempt.
	/// </summary>
	public DateTimeOffset? FirstAttemptTime { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is a retry.
	/// </summary>
	public bool IsRetry { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether validation passed.
	/// </summary>
	public bool ValidationPassed { get; set; }

	/// <summary>
	/// Gets or sets the validation timestamp.
	/// </summary>
	public DateTimeOffset? ValidationTimestamp { get; set; }

	/// <summary>
	/// Gets or sets the transaction object.
	/// </summary>
	public object? Transaction { get; set; }

	/// <summary>
	/// Gets or sets the transaction identifier.
	/// </summary>
	public string? TransactionId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the timeout was exceeded.
	/// </summary>
	public bool TimeoutExceeded { get; set; }

	/// <summary>
	/// Gets or sets the elapsed timeout duration.
	/// </summary>
	public TimeSpan? TimeoutElapsed { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the rate limit was exceeded.
	/// </summary>
	public bool RateLimitExceeded { get; set; }

	/// <summary>
	/// Gets or sets the rate limit retry-after duration.
	/// </summary>
	public TimeSpan? RateLimitRetryAfter { get; set; }

}
