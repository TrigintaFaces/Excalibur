// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Options for transactional send operations on Azure Service Bus.
/// </summary>
/// <remarks>
/// <para>
/// Azure Service Bus supports transactions that span multiple send operations
/// within the same partition. Messages sent within a transaction are either
/// all committed or all rolled back.
/// </para>
/// <para>
/// The <see cref="TransactionGroup"/> groups related messages into a transaction scope,
/// and the <see cref="PartitionKey"/> ensures all messages in the transaction are
/// routed to the same partition for atomicity.
/// </para>
/// </remarks>
public sealed class TransactionalSendOptions
{
	/// <summary>
	/// Gets or sets the transaction group identifier.
	/// </summary>
	/// <remarks>
	/// Groups related send operations into a single transaction scope.
	/// All operations with the same transaction group are committed or rolled back together.
	/// </remarks>
	/// <value>The transaction group identifier. Default is <c>null</c>.</value>
	public string? TransactionGroup { get; set; }

	/// <summary>
	/// Gets or sets the partition key for transactional messages.
	/// </summary>
	/// <remarks>
	/// All messages in a Service Bus transaction must target the same partition.
	/// Setting the partition key ensures consistent routing for transactional atomicity.
	/// </remarks>
	/// <value>The partition key. Default is <c>null</c>.</value>
	public string? PartitionKey { get; set; }
}
