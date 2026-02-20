// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Represents basic AMQP properties for AOT serialization.
/// </summary>
public sealed record BasicProperties(
	string? ContentType,
	string? ContentEncoding,
	IDictionary<string, object>? Headers,
	byte? DeliveryMode,
	byte? Priority,
	string? CorrelationId,
	string? ReplyTo,
	string? Expiration,
	string? MessageId,
	AmqpTimestamp? Timestamp,
	string? Type,
	string? UserId,
	string? AppId,
	string? ClusterId);
