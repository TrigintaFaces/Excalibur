// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Inbox.CosmosDb;

/// <summary>
/// Cosmos DB implementation of the opaque <see cref="IInboxTransactionScope"/>, wrapping the single-partition
/// <see cref="TransactionalBatch"/> onto which an inbox handler enlists its own writes so they commit atomically
/// with the processed-mark.
/// </summary>
internal sealed class CosmosInboxTransactionScope : IInboxTransactionScope
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosInboxTransactionScope"/> class.
	/// </summary>
	/// <param name="batch">The transactional batch the handler enlists its writes on.</param>
	/// <param name="partitionKey">The single partition key the batch targets.</param>
	public CosmosInboxTransactionScope(TransactionalBatch batch, PartitionKey partitionKey)
	{
		ArgumentNullException.ThrowIfNull(batch);
		Batch = batch;
		PartitionKey = partitionKey;
	}

	/// <summary>
	/// Gets the transactional batch. Operations the handler adds to this batch commit atomically with the
	/// inbox processed-mark when the batch executes.
	/// </summary>
	public TransactionalBatch Batch { get; }

	/// <summary>
	/// Gets the single partition key the batch targets. Every operation added to the batch must belong to this
	/// partition.
	/// </summary>
	public PartitionKey PartitionKey { get; }
}
