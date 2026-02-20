// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Default implementation of message metadata containing essential message properties.
/// </summary>
/// <remarks>
/// This record contains metadata that travels with messages through the dispatch pipeline, including correlation information, versioning,
/// and tenant/user context.
/// </remarks>
/// <param name="MessageId"> Unique identifier for the message. </param>
/// <param name="CorrelationId"> Unique identifier for tracking related messages. </param>
/// <param name="CausationId"> Identifier of the message that caused this message. </param>
/// <param name="TraceParent"> W3C trace context for distributed tracing. </param>
/// <param name="TenantId"> Tenant identifier for multi-tenant scenarios. </param>
/// <param name="UserId"> User identifier associated with the message. </param>
/// <param name="ContentType"> MIME type of the message content. </param>
/// <param name="SerializerVersion"> Version of the serializer used. </param>
/// <param name="MessageVersion"> Version of the message format. </param>
/// <param name="ContractVersion"> Version of the message contract (defaults to "1.0.0"). </param>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public record MessageMetadata(
	string MessageId,
	string CorrelationId,
	string? CausationId,
	string? TraceParent,
	string? TenantId,
	string? UserId,
	string ContentType,
	string SerializerVersion,
	string MessageVersion,
	string ContractVersion = "1.0.0"
) : ITransportMessageMetadata
{
	/// <summary>
	/// Creates a MessageMetadata instance from a message context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> A new MessageMetadata instance. </returns>
	public static MessageMetadata FromContext(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return new MessageMetadata(
			MessageId: context.MessageId ?? Guid.NewGuid().ToString(),
			CorrelationId: context.CorrelationId ?? Guid.NewGuid().ToString(),
			CausationId: context.CausationId,
			TraceParent: context.TraceParent,
			TenantId: context.TenantId,
			UserId: context.UserId,
			ContentType: context.ContentType ?? "application/json",
			SerializerVersion: context.SerializerVersion() ?? "1.0.0",
			MessageVersion: context.MessageVersion() ?? "1.0.0",
			ContractVersion: context.ContractVersion() ?? "1.0.0");
	}
}
