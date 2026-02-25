// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Represents information about a RabbitMQ queue from the management API.
/// </summary>
/// <param name="Name">The queue name.</param>
/// <param name="Messages">The total number of messages in the queue.</param>
/// <param name="Consumers">The number of active consumers.</param>
/// <param name="State">The queue state (e.g., running, idle).</param>
/// <param name="Type">The queue type (e.g., classic, quorum, stream).</param>
public sealed record QueueInfo(
	string Name,
	long Messages,
	int Consumers,
	string State,
	string Type);
