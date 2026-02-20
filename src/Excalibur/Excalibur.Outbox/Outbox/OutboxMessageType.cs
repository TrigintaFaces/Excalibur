// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Indicates the type of message being stored or processed in the Outbox for categorization and routing purposes. This enumeration helps
/// identify message semantics and enables type-specific processing strategies.
/// </summary>
public enum OutboxMessageType
{
	/// <summary>
	/// Unknown or unspecified message type. Used as default when message type cannot be determined.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Command message that requests an action to be performed by a specific handler or service. Commands typically have single recipients
	/// and represent business operations or state changes.
	/// </summary>
	Command = 1,

	/// <summary>
	/// Event message that notifies about something that has already happened in the system. Events can have multiple subscribers and
	/// represent state changes or business occurrences.
	/// </summary>
	Event = 2,

	/// <summary>
	/// Document message containing structured data or information to be processed or stored. Documents typically carry business data
	/// without explicit action semantics.
	/// </summary>
	Document = 3,

	/// <summary>
	/// Scheduled message that should be processed at a specific time or after a delay. These messages support delayed execution and
	/// time-based workflow scenarios.
	/// </summary>
	Scheduled = 4,
}
