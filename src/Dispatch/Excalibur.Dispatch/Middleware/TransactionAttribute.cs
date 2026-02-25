// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Transactions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Attribute to configure transaction behavior for a message type.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TransactionAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the transaction isolation level.
	/// </summary>
	/// <value>The current <see cref="IsolationLevel"/> value.</value>
	public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

	/// <summary>
	/// Gets or sets the transaction timeout in seconds.
	/// </summary>
	/// <value>The current <see cref="TimeoutSeconds"/> value.</value>
	public int TimeoutSeconds { get; set; } = 30;
}
