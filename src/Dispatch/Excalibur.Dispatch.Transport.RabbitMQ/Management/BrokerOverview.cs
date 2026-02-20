// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Represents an overview of the RabbitMQ broker from the management API.
/// </summary>
/// <param name="ClusterName">The name of the RabbitMQ cluster.</param>
/// <param name="RabbitMqVersion">The RabbitMQ server version.</param>
/// <param name="ErlangVersion">The Erlang/OTP version.</param>
/// <param name="TotalQueues">The total number of queues.</param>
/// <param name="TotalConnections">The total number of connections.</param>
public sealed record BrokerOverview(
	string ClusterName,
	string RabbitMqVersion,
	string ErlangVersion,
	int TotalQueues,
	int TotalConnections);
