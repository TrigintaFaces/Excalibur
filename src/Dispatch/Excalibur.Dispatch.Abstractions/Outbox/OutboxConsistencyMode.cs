// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Outbox;

/// <summary>
/// Defines the consistency guarantee for outbox message staging.
/// </summary>
public enum OutboxConsistencyMode
{
	/// <summary>
	/// Messages are buffered and staged after the handler completes.
	/// If the process crashes between handler completion and outbox staging,
	/// messages may be lost.
	/// </summary>
	EventuallyConsistent = 0,

	/// <summary>
	/// Messages are written to the outbox store within the ambient transaction,
	/// guaranteeing atomicity with the business state change.
	/// Requires a registered <see cref="IOutboxStore"/> and
	/// <c>TransactionMiddleware</c> in the pipeline.
	/// </summary>
	Transactional = 1,
}
