// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Represents queue configuration.
/// </summary>
public sealed record QueueConfiguration(
	string Name,
	bool Durable,
	bool Exclusive,
	bool AutoDelete,
	IDictionary<string, object>? Arguments = null,
	int? MaxLength = null,
	int? MaxLengthBytes = null,
	string? DeadLetterExchange = null,
	string? DeadLetterRoutingKey = null,
	int? MessageTtl = null,
	int? MaxPriority = null);
