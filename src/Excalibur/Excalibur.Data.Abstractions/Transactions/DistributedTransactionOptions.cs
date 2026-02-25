// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Data;

namespace Excalibur.Data.Abstractions.Transactions;

/// <summary>
/// Configuration options for distributed transaction coordination.
/// </summary>
/// <remarks>
/// Follows the <c>IOptions&lt;T&gt;</c> pattern from <c>Microsoft.Extensions.Options</c>.
/// Property count: 4 (within the ≤10-property quality gate).
/// </remarks>
public class DistributedTransactionOptions
{
	/// <summary>
	/// Gets or sets the timeout for the entire distributed transaction.
	/// </summary>
	/// <value>The transaction timeout. Defaults to 30 seconds.</value>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the isolation level for the distributed transaction.
	/// </summary>
	/// <value>The isolation level. Defaults to <see cref="System.Data.IsolationLevel.ReadCommitted"/>.</value>
	public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

	/// <summary>
	/// Gets or sets the maximum number of participants allowed in a single transaction.
	/// </summary>
	/// <value>The maximum number of participants. Defaults to 10.</value>
	[Range(1, 100)]
	public int MaxParticipants { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic rollback on prepare failure.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to automatically roll back all participants when any participant
	/// fails the prepare phase; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.
	/// </value>
	public bool AutoRollbackOnPrepareFailure { get; set; } = true;
}
