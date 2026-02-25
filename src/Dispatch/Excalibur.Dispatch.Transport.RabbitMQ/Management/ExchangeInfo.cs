// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Represents information about a RabbitMQ exchange from the management API.
/// </summary>
/// <param name="Name">The exchange name.</param>
/// <param name="Type">The exchange type (e.g., direct, fanout, topic, headers).</param>
/// <param name="Durable">Whether the exchange survives broker restart.</param>
/// <param name="AutoDelete">Whether the exchange is deleted when the last queue unbinds.</param>
/// <param name="Internal">Whether the exchange is internal (cannot be published to directly).</param>
public sealed record ExchangeInfo(
	string Name,
	string Type,
	bool Durable,
	bool AutoDelete,
	bool Internal);
