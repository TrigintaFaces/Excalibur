// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Represents consumer state.
/// </summary>
public sealed record ConsumerState(
	string ConsumerTag,
	string QueueName,
	bool AutoAck,
	bool Exclusive,
	IDictionary<string, object>? Arguments,
	int PrefetchCount,
	bool IsActive);
