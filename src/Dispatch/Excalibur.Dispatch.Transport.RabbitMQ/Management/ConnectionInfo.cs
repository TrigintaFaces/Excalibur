// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Represents information about a RabbitMQ connection from the management API.
/// </summary>
/// <param name="Name">The connection name.</param>
/// <param name="State">The connection state (e.g., running, blocked).</param>
/// <param name="Channels">The number of channels on this connection.</param>
/// <param name="ClientProperties">The client-provided properties.</param>
/// <param name="ConnectedAt">The timestamp when the connection was established.</param>
public sealed record ConnectionInfo(
	string Name,
	string State,
	int Channels,
	IReadOnlyDictionary<string, string> ClientProperties,
	DateTimeOffset ConnectedAt);
