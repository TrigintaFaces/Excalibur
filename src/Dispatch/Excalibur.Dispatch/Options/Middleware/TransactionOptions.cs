// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Transactions;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for transaction middleware.
/// </summary>
public sealed class TransactionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether transaction management is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to require transactions by default for Action messages.
	/// </summary>
	/// <value> Default is true. </value>
	public bool RequireTransactionByDefault { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable distributed transactions.
	/// </summary>
	/// <value> Default is false. </value>
	public bool EnableDistributedTransactions { get; set; }

	/// <summary>
	/// Gets or sets the default transaction isolation level.
	/// </summary>
	/// <value> Default is ReadCommitted. </value>
	public IsolationLevel DefaultIsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

	/// <summary>
	/// Gets or sets the default transaction timeout.
	/// </summary>
	/// <value> Default is 30 seconds. </value>
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets message types that bypass transaction management.
	/// </summary>
	/// <value>The current <see cref="BypassTransactionForTypes"/> value.</value>
	public string[]? BypassTransactionForTypes { get; set; }
}
