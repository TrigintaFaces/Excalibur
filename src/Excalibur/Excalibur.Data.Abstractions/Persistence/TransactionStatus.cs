// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Represents the status of a transaction.
/// </summary>
public enum TransactionStatus
{
	/// <summary>
	/// The transaction is active.
	/// </summary>
	Active = 0,

	/// <summary>
	/// The transaction is being committed.
	/// </summary>
	Committing = 1,

	/// <summary>
	/// The transaction has been committed.
	/// </summary>
	Committed = 2,

	/// <summary>
	/// The transaction is being rolled back.
	/// </summary>
	RollingBack = 3,

	/// <summary>
	/// The transaction has been rolled back.
	/// </summary>
	RolledBack = 4,

	/// <summary>
	/// The transaction has failed.
	/// </summary>
	Failed = 5,

	/// <summary>
	/// The transaction has timed out.
	/// </summary>
	TimedOut = 6,

	/// <summary>
	/// The transaction has been disposed.
	/// </summary>
	Disposed = 7,
}
