// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// A context that represents a bulk collection of contexts.
/// </summary>
internal sealed class BulkContext : IMessageContext
{
	private IDictionary<string, object?>? _properties;

	public BulkContext(IList<IMessageContext> contexts)
	{
		Contexts = contexts;
		Items = new Dictionary<string, object>(StringComparer.Ordinal);

		// Use first context as primary context for bulk-level properties
		var primaryContext = contexts.FirstOrDefault();
		if (primaryContext != null)
		{
			MessageId = primaryContext.MessageId;
			ExternalId = primaryContext.ExternalId;
			UserId = primaryContext.UserId;
			CorrelationId = primaryContext.CorrelationId;
			CausationId = primaryContext.CausationId;
			TraceParent = primaryContext.TraceParent;
			SerializerVersion = primaryContext.SerializerVersion();
			MessageVersion = primaryContext.MessageVersion();
			ContractVersion = primaryContext.ContractVersion();
			DesiredVersion = int.TryParse(primaryContext.DesiredVersion(), out var version) ? version : null;
			TenantId = primaryContext.TenantId;
			Source = primaryContext.Source;
			MessageType = primaryContext.MessageType;
			ContentType = primaryContext.ContentType;
			DeliveryCount = primaryContext.DeliveryCount;
			PartitionKey = primaryContext.PartitionKey();
			ReplyTo = primaryContext.ReplyTo();
			VersionMetadata = primaryContext.VersionMetadata() as IMessageVersionMetadata;
			Message = primaryContext.Message;
			Result = primaryContext.Result;
			ValidationResult = primaryContext.ValidationResult() as IValidationResult;
			AuthorizationResult = primaryContext.AuthorizationResult() as IAuthorizationResult;
			RoutingDecision = primaryContext.RoutingDecision;
			RequestServices = primaryContext.RequestServices;
			ReceivedTimestampUtc = primaryContext.ReceivedTimestampUtc;
			SentTimestampUtc = primaryContext.SentTimestampUtc;
			Metadata = primaryContext.Metadata() as IMessageMetadata;
		}
		else
		{
			VersionMetadata = new EmptyVersionMetadata();
			ValidationResult = new EmptyValidationResult();
			AuthorizationResult = new EmptyAuthorizationResult();
			RoutingDecision = RoutingDecision.Success("local", []);
		}
	}

	public IList<IMessageContext> Contexts { get; }

	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <inheritdoc />
	public string? ExternalId { get; set; }

	/// <inheritdoc />
	public string? UserId { get; set; }

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public string? CausationId { get; set; }

	/// <inheritdoc />
	public string? TraceParent { get; set; }

	/// <inheritdoc />
	public string? SerializerVersion { get; set; }

	/// <inheritdoc />
	public string? MessageVersion { get; set; }

	/// <inheritdoc />
	public string? ContractVersion { get; set; }

	/// <inheritdoc />
	public int? DesiredVersion { get; set; }

	/// <inheritdoc />
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the session identifier for message grouping and ordering.
	/// </summary>
	/// <value>The current <see cref="SessionId"/> value.</value>
	public string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the workflow identifier for saga orchestration.
	/// </summary>
	/// <value>The current <see cref="WorkflowId"/> value.</value>
	public string? WorkflowId { get; set; }

	/// <inheritdoc />
	public string? Source { get; set; }

	/// <inheritdoc />
	public string? MessageType { get; set; }

	/// <inheritdoc />
	public string? ContentType { get; set; }

	/// <inheritdoc />
	public int DeliveryCount { get; set; }

	/// <inheritdoc />
	public string? PartitionKey { get; set; }

	/// <inheritdoc />
	public string? ReplyTo { get; set; }

	/// <inheritdoc />
	public IMessageVersionMetadata? VersionMetadata { get; set; }

	/// <inheritdoc />
	public IDispatchMessage? Message { get; set; }

	/// <inheritdoc />
	public object? Result { get; set; }

	/// <inheritdoc />
	public IValidationResult? ValidationResult { get; set; }

	/// <inheritdoc />
	public IAuthorizationResult? AuthorizationResult { get; set; }

	/// <inheritdoc />
	public RoutingDecision? RoutingDecision { get; set; } =
		RoutingDecision.Success("local", []);

	/// <inheritdoc />
	public IServiceProvider RequestServices { get; set; } = null!;

	/// <inheritdoc />
	public DateTimeOffset ReceivedTimestampUtc { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? SentTimestampUtc { get; set; }

	/// <inheritdoc />
	public IMessageMetadata? Metadata { get; set; }

	/// <inheritdoc />
	public IDictionary<string, object> Items { get; }

	/// <inheritdoc />
	public bool Success => ValidationResult?.IsValid == true &&
	                       AuthorizationResult?.IsAuthorized == true &&
	                       (RoutingDecision?.IsSuccess ?? false);

	/// <inheritdoc />
	public IDictionary<string, object?> Properties => _properties ??= new PropertyDictionary(Items);

	// ==========================================
	// HOT-PATH PROPERTIES (Sprint 71)
	// ==========================================

	/// <inheritdoc />
	public int ProcessingAttempts { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? FirstAttemptTime { get; set; }

	/// <inheritdoc />
	public bool IsRetry { get; set; }

	/// <inheritdoc />
	public bool ValidationPassed { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? ValidationTimestamp { get; set; }

	/// <inheritdoc />
	public object? Transaction { get; set; }

	/// <inheritdoc />
	public string? TransactionId { get; set; }

	/// <inheritdoc />
	public bool TimeoutExceeded { get; set; }

	/// <inheritdoc />
	public TimeSpan? TimeoutElapsed { get; set; }

	/// <inheritdoc />
	public bool RateLimitExceeded { get; set; }

	/// <inheritdoc />
	public TimeSpan? RateLimitRetryAfter { get; set; }

	/// <inheritdoc />
	public bool ContainsItem(string key) => Items.ContainsKey(key);

	/// <inheritdoc />
	public T? GetItem<T>(string key) => Items.TryGetValue(key, out var value) && value is T typed ? typed : default;

	/// <inheritdoc />
	public T GetItem<T>(string key, T defaultValue) => Items.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

	/// <inheritdoc />
	public void RemoveItem(string key) => Items.Remove(key);

	/// <inheritdoc />
	public void SetItem<T>(string key, T value) => Items[key] = value!;

	/// <inheritdoc />
	public IMessageContext CreateChildContext()
	{
		// For bulk contexts, create a child from the primary (first) context
		var primaryContext = Contexts.FirstOrDefault();
		return primaryContext?.CreateChildContext() ?? new MessageContext();
	}
}
