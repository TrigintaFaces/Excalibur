// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;

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
		context.ExternalId = metadata.ExternalId;
		context.UserId = metadata.UserId;

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
		if (!string.IsNullOrWhiteSpace(metadata.TenantId))
		{
			context.TenantId = metadata.TenantId;
		}

		// Tracing
		context.TraceParent = metadata.TraceParent;

		// Message type and versioning
		context.MessageType = metadata.MessageType;
		context.ContentType = metadata.ContentType;
		context.SerializerVersion(metadata.SerializerVersion);
		context.MessageVersion(metadata.MessageVersion);
		context.ContractVersion(metadata.ContractVersion);

		// Routing
		context.Source = metadata.Source;
		context.PartitionKey(metadata.PartitionKey);
		context.ReplyTo(metadata.ReplyTo);

		// Delivery state
		context.DeliveryCount = metadata.DeliveryCount;

		// Timing
		if (metadata.ReceivedTimestampUtc.HasValue)
		{
			context.ReceivedTimestampUtc = metadata.ReceivedTimestampUtc.Value;
		}

		context.SentTimestampUtc = metadata.SentTimestampUtc;

		// Copy extensible items
		foreach (var item in metadata.Items)
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
			.WithExternalId(context.ExternalId)
			.WithUserId(context.UserId)
			.WithTenantId(context.TenantId)
			.WithTraceParent(context.TraceParent)
			.WithMessageType(context.MessageType ?? "Unknown")
			.WithContentType(context.ContentType ?? "application/json")
			.WithSerializerVersion(context.SerializerVersion() ?? "1.0")
			.WithMessageVersion(context.MessageVersion() ?? "1.0")
			.WithContractVersion(context.ContractVersion() ?? "1.0.0")
			.WithSource(context.Source)
			.WithPartitionKey(context.PartitionKey())
			.WithReplyTo(context.ReplyTo())
			.WithDeliveryCount(context.DeliveryCount);

		if (context.ReceivedTimestampUtc != default)
		{
			_ = builder.WithReceivedTimestampUtc(context.ReceivedTimestampUtc);
		}

		if (context.SentTimestampUtc.HasValue)
		{
			_ = builder.WithSentTimestampUtc(context.SentTimestampUtc.Value);
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
		var baggageItems = activity.Baggage.ToList();
		if (baggageItems.Count > 0)
		{
			var baggageString = string.Join(',', baggageItems.Select(static kv => $"{kv.Key}={kv.Value}"));
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
			.WithUserId(metadata.UserId)
			.WithTenantId(metadata.TenantId)
			.WithTraceParent(metadata.TraceParent)
			.WithTraceState(metadata.TraceState)
			.WithBaggage(metadata.Baggage)
			.WithMessageType(replyMessageType ?? $"Reply.{metadata.MessageType}")
			.WithContentType(metadata.ContentType)
			.WithSerializerVersion(metadata.SerializerVersion)
			.WithMessageVersion(metadata.MessageVersion)
			.WithContractVersion(metadata.ContractVersion)
			.WithSource(metadata.Destination) // Swap source and destination for reply
			.WithDestination(metadata.ReplyTo ?? metadata.Source)
			.WithSessionId(metadata.SessionId)
			.WithPartitionKey(metadata.PartitionKey);

		// Copy roles and claims
		_ = builder.WithRoles(metadata.Roles);
		_ = builder.WithClaims(metadata.Claims);

		// Copy relevant headers
		foreach (var header in metadata.Headers.Where(static h => IsRelevantForReply(h.Key)))
		{
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
		if (metadata.ExpiresAtUtc.HasValue && now >= metadata.ExpiresAtUtc.Value)
		{
			return true;
		}

		// Check TTL-based expiration
		if (metadata is { TimeToLive: not null, SentTimestampUtc: not null })
		{
			var expirationTime = metadata.SentTimestampUtc.Value.Add(metadata.TimeToLive.Value);
			return now >= expirationTime;
		}

		return false;
	}

	/// <summary>
	/// Checks if the message should be dead-lettered based on delivery count.
	/// </summary>
	public static bool ShouldDeadLetter(this IMessageMetadata metadata)
	{
		if (!metadata.MaxDeliveryCount.HasValue)
		{
			return false;
		}

		return metadata.DeliveryCount >= metadata.MaxDeliveryCount.Value;
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
		if (string.IsNullOrWhiteSpace(metadata.UserId) && metadata.Claims.Count == 0 && metadata.Roles.Count == 0)
		{
			return null;
		}

		var claims = new List<Claim>(metadata.Claims);

		// Add user ID as name claim if not already present
		if (!string.IsNullOrWhiteSpace(metadata.UserId) &&
			!claims.Exists(c => string.Equals(c.Type, ClaimTypes.NameIdentifier, StringComparison.Ordinal)))
		{
			claims.Add(new Claim(ClaimTypes.NameIdentifier, metadata.UserId));
		}

		// Add roles as role claims
		foreach (var role in metadata.Roles)
		{
			if (!claims.Exists(c =>
					string.Equals(c.Type, ClaimTypes.Role, StringComparison.Ordinal) &&
					string.Equals(c.Value, role, StringComparison.Ordinal)))
			{
				claims.Add(new Claim(ClaimTypes.Role, role));
			}
		}

		// Add tenant as a custom claim
		if (!string.IsNullOrWhiteSpace(metadata.TenantId))
		{
			claims.Add(new Claim("TenantId", metadata.TenantId));
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

		if (secondary.UserId != null)
		{
			_ = builder.WithUserId(secondary.UserId);
		}

		if (secondary.TenantId != null)
		{
			_ = builder.WithTenantId(secondary.TenantId);
		}

		// Merge collections
		_ = builder.AddHeaders(secondary.Headers);
		_ = builder.AddAttributes(secondary.Attributes);
		_ = builder.AddProperties(secondary.Properties);
		_ = builder.AddItems(secondary.Items);

		// Merge roles and claims
		var mergedRoles = primary.Roles.Union(secondary.Roles, StringComparer.Ordinal).Distinct(StringComparer.Ordinal);
		_ = builder.WithRoles(mergedRoles);

		var mergedClaims = primary.Claims.Union(secondary.Claims, new ClaimComparer()).Distinct(new ClaimComparer());
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

		// Add all non-null values or all values if requested
		AddIfNotNull(dict, "MessageId", metadata.MessageId, includeNullValues);
		AddIfNotNull(dict, "CorrelationId", metadata.CorrelationId, includeNullValues);
		AddIfNotNull(dict, "CausationId", metadata.CausationId, includeNullValues);
		AddIfNotNull(dict, "ExternalId", metadata.ExternalId, includeNullValues);
		AddIfNotNull(dict, "TraceParent", metadata.TraceParent, includeNullValues);
		AddIfNotNull(dict, "TraceState", metadata.TraceState, includeNullValues);
		AddIfNotNull(dict, "Baggage", metadata.Baggage, includeNullValues);
		AddIfNotNull(dict, "UserId", metadata.UserId, includeNullValues);
		AddIfNotNull(dict, "TenantId", metadata.TenantId, includeNullValues);
		AddIfNotNull(dict, "MessageType", metadata.MessageType, includeNullValues);
		AddIfNotNull(dict, "ContentType", metadata.ContentType, includeNullValues);
		AddIfNotNull(dict, "ContentEncoding", metadata.ContentEncoding, includeNullValues);
		AddIfNotNull(dict, "MessageVersion", metadata.MessageVersion, includeNullValues);
		AddIfNotNull(dict, "SerializerVersion", metadata.SerializerVersion, includeNullValues);
		AddIfNotNull(dict, "ContractVersion", metadata.ContractVersion, includeNullValues);
		AddIfNotNull(dict, "Source", metadata.Source, includeNullValues);
		AddIfNotNull(dict, "Destination", metadata.Destination, includeNullValues);
		AddIfNotNull(dict, "ReplyTo", metadata.ReplyTo, includeNullValues);
		AddIfNotNull(dict, "SessionId", metadata.SessionId, includeNullValues);
		AddIfNotNull(dict, "PartitionKey", metadata.PartitionKey, includeNullValues);
		AddIfNotNull(dict, "RoutingKey", metadata.RoutingKey, includeNullValues);
		AddIfNotNull(dict, "GroupId", metadata.GroupId, includeNullValues);

		// Add numeric values
		if (metadata.GroupSequence.HasValue || includeNullValues)
		{
			dict["GroupSequence"] = metadata.GroupSequence;
		}

		if (metadata.DeliveryCount > 0 || includeNullValues)
		{
			dict["DeliveryCount"] = metadata.DeliveryCount;
		}

		if (metadata.Priority.HasValue || includeNullValues)
		{
			dict["Priority"] = metadata.Priority;
		}

		// Add timestamps
		dict["CreatedTimestampUtc"] = metadata.CreatedTimestampUtc.ToString("O");

		if (metadata.SentTimestampUtc.HasValue || includeNullValues)
		{
			dict["SentTimestampUtc"] = metadata.SentTimestampUtc?.ToString("O");
		}

		if (metadata.ReceivedTimestampUtc.HasValue || includeNullValues)
		{
			dict["ReceivedTimestampUtc"] = metadata.ReceivedTimestampUtc?.ToString("O");
		}

		// Add collections if not empty
		if (metadata.Roles.Count > 0)
		{
			dict["Roles"] = metadata.Roles.ToList();
		}

		if (metadata.Headers.Count > 0)
		{
			dict["Headers"] = new Dictionary<string, string>(metadata.Headers, StringComparer.Ordinal);
		}

		if (metadata.Attributes.Count > 0)
		{
			dict["Attributes"] = new Dictionary<string, object>(metadata.Attributes, StringComparer.Ordinal);
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

	private static readonly string[] RelevantReplyHeaders =
	[
		"X-Request-Id", "X-Session-Id", "X-Client-Id", "X-API-Version", "Accept-Language", "X-Forwarded-For",
	];

	private static bool IsRelevantForReply(string headerKey) =>
		RelevantReplyHeaders.Any(h => h.Equals(headerKey, StringComparison.OrdinalIgnoreCase));

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
