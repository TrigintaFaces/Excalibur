// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Security.Claims;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Extension methods for working with unified message metadata.
/// </summary>
public static class MessageMetadataExtensions
{
	/// <summary>
	/// Converts legacy IMessageMetadata to unified metadata.
	/// </summary>
	public static MessageMetadata ToUnified(this IMessageMetadata legacy, IMessageContext? context = null) =>
		MessageMetadata.FromLegacyMetadata(legacy, context);

	/// <summary>
	/// Converts unified metadata back to legacy IMessageMetadata for backward compatibility.
	/// </summary>
	public static IMessageMetadata ToLegacy(this IMessageMetadata unified)
	{
		ArgumentNullException.ThrowIfNull(unified);

		// Return the unified metadata directly since it implements IMessageMetadata
		return unified;
	}

	/// <summary>
	/// Updates an IMessageContext with values from unified metadata.
	/// </summary>
	public static void ApplyToContext(this IMessageMetadata metadata, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(metadata);
		ArgumentNullException.ThrowIfNull(context);

		// Core identity fields
		context.MessageId = metadata.MessageId;

		var identityFeature = context.GetOrCreateIdentityFeature();
		identityFeature.ExternalId = metadata.GetExternalId();
		identityFeature.UserId = metadata.GetUserId();

		// Correlation and causation
		if (!string.IsNullOrWhiteSpace(metadata.CorrelationId))
		{
			context.CorrelationId = metadata.CorrelationId;
		}

		if (!string.IsNullOrWhiteSpace(metadata.CausationId))
		{
			context.CausationId = metadata.CausationId;
		}

		// Tenant context
		var tenantId = metadata.GetTenantId();
		if (!string.IsNullOrWhiteSpace(tenantId))
		{
			identityFeature.TenantId = tenantId;
		}

		// Tracing
		identityFeature.TraceParent = metadata.GetTraceParent();

		// Message type and versioning
		context.SetMessageType(metadata.MessageType);
		context.SetContentType(metadata.ContentType);
		context.SerializerVersion(metadata.GetSerializerVersion());
		context.MessageVersion(metadata.GetMessageVersion());
		context.ContractVersion(metadata.GetContractVersion());

		// Routing
		var routingFeature = context.GetOrCreateRoutingFeature();
		routingFeature.Source = metadata.Source;
		context.PartitionKey(metadata.GetPartitionKey());
		context.ReplyTo(metadata.GetReplyTo());

		// Delivery state
		context.GetOrCreateProcessingFeature().DeliveryCount = metadata.GetDeliveryCount();

		// Timing
		var receivedTimestamp = metadata.GetReceivedTimestampUtc();
		if (receivedTimestamp.HasValue)
		{
			context.SetReceivedTimestampUtc(receivedTimestamp.Value);
		}

		context.SetSentTimestampUtc(metadata.GetSentTimestampUtc());

		// Copy extensible items
		var items = metadata.GetItems();
		foreach (var item in items)
		{
			context.SetItem(item.Key, item.Value);
		}
	}

	/// <summary>
	/// Creates unified metadata from an IMessageContext.
	/// </summary>
	public static IMessageMetadata ExtractMetadata(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var builder = new MessageMetadataBuilder()
			.WithMessageId(context.MessageId ?? Guid.NewGuid().ToString())
			.WithCorrelationId(context.CorrelationId ?? context.MessageId ?? Guid.NewGuid().ToString())
			.WithCausationId(context.CausationId)
			.WithExternalId(context.GetExternalId())
			.WithUserId(context.GetUserId())
			.WithTenantId(context.GetTenantId())
			.WithTraceParent(context.GetTraceParent())
			.WithMessageType(context.GetMessageType() ?? "Unknown")
			.WithContentType(context.GetContentType() ?? "application/json")
			.WithSerializerVersion(context.SerializerVersion() ?? "1.0")
			.WithMessageVersion(context.MessageVersion() ?? "1.0")
			.WithContractVersion(context.ContractVersion() ?? "1.0.0")
			.WithSource(context.GetSource())
			.WithPartitionKey(context.PartitionKey())
			.WithReplyTo(context.ReplyTo())
			.WithDeliveryCount(context.GetDeliveryCount());

		var receivedTimestamp = context.GetReceivedTimestampUtc();
		if (receivedTimestamp.HasValue && receivedTimestamp.Value != default)
		{
			_ = builder.WithReceivedTimestampUtc(receivedTimestamp.Value);
		}

		var sentTimestamp = context.GetSentTimestampUtc();
		if (sentTimestamp.HasValue)
		{
			_ = builder.WithSentTimestampUtc(sentTimestamp.Value);
		}

		// Copy items
		_ = builder.AddItems(context.Items);

		return builder.Build();
	}

	/// <summary>
	/// Enriches metadata with OpenTelemetry trace context from the current activity.
	/// </summary>
	public static IMessageMetadata WithCurrentTraceContext(this IMessageMetadata metadata)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return metadata;
		}

		var builder = metadata.ToBuilder();

		// Set W3C trace parent
		if (activity.Id != null)
		{
			_ = builder.WithTraceParent(activity.Id);
		}

		// Set trace state if available
		var traceState = activity.TraceStateString;
		if (!string.IsNullOrWhiteSpace(traceState))
		{
			_ = builder.WithTraceState(traceState);
		}

		// Build baggage string from activity baggage
		var baggageString = BuildBaggageString(activity.Baggage);
		if (baggageString is not null)
		{
			_ = builder.WithBaggage(baggageString);
		}

		return builder.Build();
	}

	/// <summary>
	/// Creates a reply metadata from the current metadata, setting up proper correlation.
	/// </summary>
	public static IMessageMetadata CreateReplyMetadata(this IMessageMetadata metadata, string? replyMessageType = null)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		var replyId = Guid.NewGuid().ToString();
		var builder = new MessageMetadataBuilder()
			.WithMessageId(replyId)
			.WithCorrelationId(metadata.CorrelationId) // Keep the same correlation
			.WithCausationId(metadata.MessageId) // The original message caused this reply
			.WithUserId(metadata.GetUserId())
			.WithTenantId(metadata.GetTenantId())
			.WithTraceParent(metadata.GetTraceParent())
			.WithTraceState(metadata.GetTraceState())
			.WithBaggage(metadata.GetBaggage())
			.WithMessageType(replyMessageType ?? $"Reply.{metadata.MessageType}")
			.WithContentType(metadata.ContentType)
			.WithSerializerVersion(metadata.GetSerializerVersion())
			.WithMessageVersion(metadata.GetMessageVersion())
			.WithContractVersion(metadata.GetContractVersion())
			.WithSource(metadata.GetDestination()) // Swap source and destination for reply
			.WithDestination(metadata.GetReplyTo() ?? metadata.Source)
			.WithSessionId(metadata.GetSessionId())
			.WithPartitionKey(metadata.GetPartitionKey());

		// Copy roles and claims
		_ = builder.WithRoles(metadata.GetRoles());
		_ = builder.WithClaims(metadata.GetClaims());

		// Copy relevant headers
		foreach (var header in metadata.Headers)
		{
			if (!IsRelevantForReply(header.Key))
			{
				continue;
			}

			_ = builder.AddHeader(header.Key, header.Value);
		}

		return builder.Build();
	}

	/// <summary>
	/// Checks if the message has expired based on TTL or explicit expiration.
	/// </summary>
	public static bool IsExpired(this IMessageMetadata metadata, DateTimeOffset? currentUtc = null)
	{
		var now = currentUtc ?? DateTimeOffset.UtcNow;

		// Check explicit expiration
		var expiresAtUtc = metadata.GetExpiresAtUtc();
		if (expiresAtUtc.HasValue && now >= expiresAtUtc.Value)
		{
			return true;
		}

		// Check TTL-based expiration
		var timeToLive = metadata.GetTimeToLive();
		var sentTimestampUtc = metadata.GetSentTimestampUtc();
		if (timeToLive is not null && sentTimestampUtc is not null)
		{
			var expirationTime = sentTimestampUtc.Value.Add(timeToLive.Value);
			return now >= expirationTime;
		}

		return false;
	}

	/// <summary>
	/// Checks if the message should be dead-lettered based on delivery count.
	/// </summary>
	public static bool ShouldDeadLetter(this IMessageMetadata metadata)
	{
		var maxDeliveryCount = metadata.GetMaxDeliveryCount();
		if (!maxDeliveryCount.HasValue)
		{
			return false;
		}

		return metadata.GetDeliveryCount() >= maxDeliveryCount.Value;
	}

	/// <summary>
	/// Creates metadata for a dead-lettered message.
	/// </summary>
	public static IMessageMetadata CreateDeadLetterMetadata(
		this IMessageMetadata metadata,
		string reason,
		string? errorDescription = null,
		string? deadLetterQueue = null)
	{
		ArgumentNullException.ThrowIfNull(metadata);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		return metadata.ToBuilder()
			.WithDeadLetterReason(reason)
			.WithDeadLetterErrorDescription(errorDescription)
			.WithDeadLetterQueue(deadLetterQueue ?? "dead-letter")
			.WithLastDeliveryError(errorDescription)
			.Build();
	}

	/// <summary>
	/// Extracts user claims from metadata for authorization.
	/// </summary>
	public static ClaimsPrincipal? GetClaimsPrincipal(this IMessageMetadata metadata)
	{
		var userId = metadata.GetUserId();
		var metadataClaims = metadata.GetClaims();
		var metadataRoles = metadata.GetRoles();

		if (string.IsNullOrWhiteSpace(userId) && metadataClaims.Count == 0 && metadataRoles.Count == 0)
		{
			return null;
		}

		var claims = new List<Claim>(metadataClaims);

		// Add user ID as name claim if not already present
		if (!string.IsNullOrWhiteSpace(userId) &&
			!claims.Exists(c => string.Equals(c.Type, ClaimTypes.NameIdentifier, StringComparison.Ordinal)))
		{
			claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
		}

		// Add roles as role claims
		foreach (var role in metadataRoles)
		{
			if (!claims.Exists(c =>
					string.Equals(c.Type, ClaimTypes.Role, StringComparison.Ordinal) &&
					string.Equals(c.Value, role, StringComparison.Ordinal)))
			{
				claims.Add(new Claim(ClaimTypes.Role, role));
			}
		}

		// Add tenant as a custom claim
		var tenantId = metadata.GetTenantId();
		if (!string.IsNullOrWhiteSpace(tenantId))
		{
			claims.Add(new Claim("TenantId", tenantId));
		}

		var identity = new ClaimsIdentity(claims, "MessageMetadata");
		return new ClaimsPrincipal(identity);
	}

	/// <summary>
	/// Merges two metadata instances, with values from the second taking precedence.
	/// </summary>
	public static IMessageMetadata Merge(this IMessageMetadata primary, IMessageMetadata secondary)
	{
		ArgumentNullException.ThrowIfNull(primary);
		ArgumentNullException.ThrowIfNull(secondary);

		var builder = primary.ToBuilder();

		// Override with non-null values from secondary
		if (!string.Equals(secondary.MessageId, primary.MessageId, StringComparison.Ordinal))
		{
			_ = builder.WithMessageId(secondary.MessageId);
		}

		if (!string.Equals(secondary.CorrelationId, primary.CorrelationId, StringComparison.Ordinal))
		{
			_ = builder.WithCorrelationId(secondary.CorrelationId);
		}

		if (secondary.CausationId != null)
		{
			_ = builder.WithCausationId(secondary.CausationId);
		}

		if (secondary.GetUserId() != null)
		{
			_ = builder.WithUserId(secondary.GetUserId());
		}

		if (secondary.GetTenantId() != null)
		{
			_ = builder.WithTenantId(secondary.GetTenantId());
		}

		// Merge collections
		_ = builder.AddHeaders(secondary.Headers);
		_ = builder.AddProperties(secondary.Properties);

		// Merge roles and claims
		var mergedRoles = primary.GetRoles().Union(secondary.GetRoles(), StringComparer.Ordinal);
		_ = builder.WithRoles(mergedRoles);

		var mergedClaims = primary.GetClaims().Union(secondary.GetClaims(), new ClaimComparer());
		_ = builder.WithClaims(mergedClaims);

		return builder.Build();
	}

	/// <summary>
	/// Converts metadata to a dictionary for serialization or transport.
	/// </summary>
	public static Dictionary<string, object?> ToDictionary(this IMessageMetadata metadata, bool includeNullValues = false)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

		// Core fields (on interface)
		AddIfNotNull(dict, "MessageId", metadata.MessageId, includeNullValues);
		AddIfNotNull(dict, "CorrelationId", metadata.CorrelationId, includeNullValues);
		AddIfNotNull(dict, "CausationId", metadata.CausationId, includeNullValues);
		AddIfNotNull(dict, "MessageType", metadata.MessageType, includeNullValues);
		AddIfNotNull(dict, "ContentType", metadata.ContentType, includeNullValues);
		AddIfNotNull(dict, "Source", metadata.Source, includeNullValues);

		// Identity (from extension methods)
		AddIfNotNull(dict, "ExternalId", metadata.GetExternalId(), includeNullValues);
		AddIfNotNull(dict, "TraceParent", metadata.GetTraceParent(), includeNullValues);
		AddIfNotNull(dict, "TraceState", metadata.GetTraceState(), includeNullValues);
		AddIfNotNull(dict, "Baggage", metadata.GetBaggage(), includeNullValues);
		AddIfNotNull(dict, "UserId", metadata.GetUserId(), includeNullValues);
		AddIfNotNull(dict, "TenantId", metadata.GetTenantId(), includeNullValues);

		// Versioning (from extension methods)
		AddIfNotNull(dict, "ContentEncoding", metadata.GetContentEncoding(), includeNullValues);
		AddIfNotNull(dict, "MessageVersion", metadata.GetMessageVersion(), includeNullValues);
		AddIfNotNull(dict, "SerializerVersion", metadata.GetSerializerVersion(), includeNullValues);
		AddIfNotNull(dict, "ContractVersion", metadata.GetContractVersion(), includeNullValues);

		// Routing (from extension methods)
		AddIfNotNull(dict, "Destination", metadata.GetDestination(), includeNullValues);
		AddIfNotNull(dict, "ReplyTo", metadata.GetReplyTo(), includeNullValues);
		AddIfNotNull(dict, "SessionId", metadata.GetSessionId(), includeNullValues);
		AddIfNotNull(dict, "PartitionKey", metadata.GetPartitionKey(), includeNullValues);
		AddIfNotNull(dict, "RoutingKey", metadata.GetRoutingKey(), includeNullValues);
		AddIfNotNull(dict, "GroupId", metadata.GetGroupId(), includeNullValues);

		// Numeric values
		var groupSequence = metadata.GetGroupSequence();
		if (groupSequence.HasValue || includeNullValues)
		{
			dict["GroupSequence"] = groupSequence;
		}

		var deliveryCount = metadata.GetDeliveryCount();
		if (deliveryCount > 0 || includeNullValues)
		{
			dict["DeliveryCount"] = deliveryCount;
		}

		var priority = metadata.GetPriority();
		if (priority.HasValue || includeNullValues)
		{
			dict["Priority"] = priority;
		}

		// Timestamps
		dict["CreatedTimestampUtc"] = metadata.CreatedTimestampUtc.ToString("O");

		var sentTimestamp = metadata.GetSentTimestampUtc();
		if (sentTimestamp.HasValue || includeNullValues)
		{
			dict["SentTimestampUtc"] = sentTimestamp?.ToString("O");
		}

		var receivedTimestamp = metadata.GetReceivedTimestampUtc();
		if (receivedTimestamp.HasValue || includeNullValues)
		{
			dict["ReceivedTimestampUtc"] = receivedTimestamp?.ToString("O");
		}

		// Collections
		var roles = metadata.GetRoles();
		if (roles.Count > 0)
		{
			dict["Roles"] = roles.ToList();
		}

		if (metadata.Headers.Count > 0)
		{
			dict["Headers"] = new Dictionary<string, string>(metadata.Headers, StringComparer.Ordinal);
		}

		var attributes = metadata.GetAttributes();
		if (attributes.Count > 0)
		{
			dict["Attributes"] = new Dictionary<string, object>(attributes, StringComparer.Ordinal);
		}

		if (metadata.Properties.Count > 0)
		{
			dict["Properties"] = new Dictionary<string, object>(metadata.Properties, StringComparer.Ordinal);
		}

		return dict;
	}

	private static void AddIfNotNull(Dictionary<string, object?> dict, string key, object? value, bool includeNull)
	{
		if (value != null || includeNull)
		{
			dict[key] = value;
		}
	}

	private static string? BuildBaggageString(IEnumerable<KeyValuePair<string, string?>> baggage)
	{
		StringBuilder? builder = null;
		foreach (var baggageItem in baggage)
		{
			builder ??= new StringBuilder();
			if (builder.Length > 0)
			{
				_ = builder.Append(',');
			}

			_ = builder.Append(baggageItem.Key);
			_ = builder.Append('=');
			_ = builder.Append(baggageItem.Value);
		}

		return builder?.ToString();
	}

	private static readonly string[] RelevantReplyHeaders =
	[
		"X-Request-Id", "X-Session-Id", "X-Client-Id", "X-API-Version", "Accept-Language", "X-Forwarded-For",
	];

	private static bool IsRelevantForReply(string headerKey)
	{
		for (var i = 0; i < RelevantReplyHeaders.Length; i++)
		{
			if (headerKey.Equals(RelevantReplyHeaders[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private sealed class ClaimComparer : IEqualityComparer<Claim>
	{
		public bool Equals(Claim? x, Claim? y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}

			if (x == null || y == null)
			{
				return false;
			}

			return string.Equals(x.Type, y.Type, StringComparison.Ordinal) && string.Equals(x.Value, y.Value, StringComparison.Ordinal);
		}

		public int GetHashCode(Claim obj) => HashCode.Combine(obj.Type, obj.Value);
	}
}
