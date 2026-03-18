// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace TransactionalHandlers.Commands;

/// <summary>
/// Command to transfer funds between two accounts.
/// This demonstrates a transactional operation that must be atomic.
/// </summary>
public sealed class TransferFunds : IDispatchAction
{
	public Guid FromAccountId { get; init; }
	public Guid ToAccountId { get; init; }
	public decimal Amount { get; init; }
}
