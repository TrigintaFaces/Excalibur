// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Metadata;

public sealed partial class MessageMetadataBuilder
{
	/// <inheritdoc />
	public IMessageMetadataBuilder WithProperty(string key, object? value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		switch (key)
		{
			// Core (also on IMessageMetadata interface, set via builder extension methods)
			case MetadataPropertyKeys.Source:
				_source = value as string;
				break;
			case MetadataPropertyKeys.MessageType:
				if (value is string mt && !string.IsNullOrWhiteSpace(mt))
				{
					_messageType = mt;
				}

				break;
			case MetadataPropertyKeys.ContentType:
				if (value is string ct && !string.IsNullOrWhiteSpace(ct))
				{
					_contentType = ct;
				}

				break;
			case MetadataPropertyKeys.CreatedTimestampUtc:
				if (value is DateTimeOffset ctu)
				{
					_createdTimestampUtc = ctu;
				}

				break;

			// Identity/Security
			case MetadataPropertyKeys.ExternalId:
				_externalId = value as string;
				break;
			case MetadataPropertyKeys.TraceParent:
				_traceParent = value as string;
				break;
			case MetadataPropertyKeys.TraceState:
				_traceState = value as string;
				break;
			case MetadataPropertyKeys.Baggage:
				_baggage = value as string;
				break;
			case MetadataPropertyKeys.UserId:
				_userId = value as string;
				break;
			case MetadataPropertyKeys.TenantId:
				_tenantId = value as string;
				break;
			case MetadataPropertyKeys.Roles:
				_roles.Clear();
				if (value is IEnumerable<string> roles)
				{
					_roles.AddRange(roles);
				}

				break;
			case MetadataPropertyKeys.Claims:
				_claims.Clear();
				if (value is IEnumerable<Claim> claims)
				{
					_claims.AddRange(claims);
				}

				break;

			// Versioning
			case MetadataPropertyKeys.ContentEncoding:
				_contentEncoding = value as string;
				break;
			case MetadataPropertyKeys.MessageVersion:
				if (value is string mv && !string.IsNullOrWhiteSpace(mv))
				{
					_messageVersion = mv;
				}

				break;
			case MetadataPropertyKeys.SerializerVersion:
				if (value is string sv && !string.IsNullOrWhiteSpace(sv))
				{
					_serializerVersion = sv;
				}

				break;
			case MetadataPropertyKeys.ContractVersion:
				if (value is string cv && !string.IsNullOrWhiteSpace(cv))
				{
					_contractVersion = cv;
				}

				break;

			// Routing
			case MetadataPropertyKeys.Destination:
				_destination = value as string;
				break;
			case MetadataPropertyKeys.ReplyTo:
				_replyTo = value as string;
				break;
			case MetadataPropertyKeys.SessionId:
				_sessionId = value as string;
				break;
			case MetadataPropertyKeys.PartitionKey:
				_partitionKey = value as string;
				break;
			case MetadataPropertyKeys.RoutingKey:
				_routingKey = value as string;
				break;
			case MetadataPropertyKeys.GroupId:
				_groupId = value as string;
				break;
			case MetadataPropertyKeys.GroupSequence:
				_groupSequence = value is long gs ? gs : value is int gsi ? gsi : null;
				break;

			// Temporal
			case MetadataPropertyKeys.SentTimestampUtc:
				_sentTimestampUtc = value is DateTimeOffset st ? st : null;
				break;
			case MetadataPropertyKeys.ReceivedTimestampUtc:
				_receivedTimestampUtc = value is DateTimeOffset rt ? rt : null;
				break;
			case MetadataPropertyKeys.ScheduledEnqueueTimeUtc:
				_scheduledEnqueueTimeUtc = value is DateTimeOffset se ? se : null;
				break;
			case MetadataPropertyKeys.TimeToLive:
				_timeToLive = value is TimeSpan ttl ? ttl : null;
				break;
			case MetadataPropertyKeys.ExpiresAtUtc:
				_expiresAtUtc = value is DateTimeOffset ea ? ea : null;
				break;

			// Transport/Delivery
			case MetadataPropertyKeys.DeliveryCount:
				_deliveryCount = value is int dc ? dc : 0;
				break;
			case MetadataPropertyKeys.MaxDeliveryCount:
				_maxDeliveryCount = value is int mdc ? mdc : null;
				break;
			case MetadataPropertyKeys.LastDeliveryError:
				_lastDeliveryError = value as string;
				break;
			case MetadataPropertyKeys.DeadLetterQueue:
				_deadLetterQueue = value as string;
				break;
			case MetadataPropertyKeys.DeadLetterReason:
				_deadLetterReason = value as string;
				break;
			case MetadataPropertyKeys.DeadLetterErrorDescription:
				_deadLetterErrorDescription = value as string;
				break;
			case MetadataPropertyKeys.Priority:
				_priority = value is int p ? p : null;
				break;
			case MetadataPropertyKeys.Durable:
				_durable = value is bool d ? d : null;
				break;
			case MetadataPropertyKeys.RequiresDuplicateDetection:
				_requiresDuplicateDetection = value is bool rdd ? rdd : null;
				break;
			case MetadataPropertyKeys.DuplicateDetectionWindow:
				_duplicateDetectionWindow = value is TimeSpan ddw ? ddw : null;
				break;

			// Event Sourcing
			case MetadataPropertyKeys.AggregateId:
				_aggregateId = value as string;
				break;
			case MetadataPropertyKeys.AggregateType:
				_aggregateType = value as string;
				break;
			case MetadataPropertyKeys.AggregateVersion:
				_aggregateVersion = value is long av ? av : value is int avi ? avi : null;
				break;
			case MetadataPropertyKeys.StreamName:
				_streamName = value as string;
				break;
			case MetadataPropertyKeys.StreamPosition:
				_streamPosition = value is long sp ? sp : value is int spi ? spi : null;
				break;
			case MetadataPropertyKeys.GlobalPosition:
				_globalPosition = value is long gp ? gp : value is int gpi ? gpi : null;
				break;
			case MetadataPropertyKeys.EventType:
				_eventType = value as string;
				break;
			case MetadataPropertyKeys.EventVersion:
				_eventVersion = value is int ev ? ev : null;
				break;

			// Removed collections
			case MetadataPropertyKeys.Attributes:
				_attributes.Clear();
				if (value is IEnumerable<KeyValuePair<string, object>> attrs)
				{
					foreach (var attr in attrs)
					{
						_attributes[attr.Key] = attr.Value;
					}
				}

				break;
			case MetadataPropertyKeys.Items:
				_items.Clear();
				if (value is IEnumerable<KeyValuePair<string, object>> items)
				{
					foreach (var item in items)
					{
						_items[item.Key] = item.Value;
					}
				}

				break;

			default:
				if (value != null)
				{
					_properties[key] = value;
				}
				else
				{
					_properties.Remove(key);
				}

				break;
		}

		return this;
	}
}
