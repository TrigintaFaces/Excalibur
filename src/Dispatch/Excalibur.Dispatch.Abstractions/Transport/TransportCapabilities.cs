// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Capabilities of a transport provider.
/// </summary>
[Flags]
public enum TransportCapabilities
{
	/// <summary>
	/// No capabilities.
	/// </summary>
	None = 0,

	/// <summary>
	/// Supports creating transport adapters.
	/// </summary>
	TransportAdapter = 1 << 0,

	/// <summary>
	/// Supports creating message bus adapters.
	/// </summary>
	MessageBusAdapter = 1 << 1,

	/// <summary>
	/// Supports message batching.
	/// </summary>
	Batching = 1 << 2,

	/// <summary>
	/// Supports message transactions.
	/// </summary>
	Transactions = 1 << 3,

	/// <summary>
	/// Supports dead letter queue management.
	/// </summary>
	DeadLetterQueue = 1 << 4,

	/// <summary>
	/// Supports message priority.
	/// </summary>
	Priority = 1 << 5,

	/// <summary>
	/// Supports message scheduling/delayed delivery.
	/// </summary>
	Scheduling = 1 << 6,

	/// <summary>
	/// All capabilities.
	/// </summary>
	All = TransportAdapter | MessageBusAdapter | Batching | Transactions | DeadLetterQueue | Priority | Scheduling,
}
