// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.CosmosDb;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Dispatch;

/// <summary>
/// Provider-specific extensions for obtaining the Cosmos DB-native transactional batch from an opaque
/// <see cref="IInboxTransactionScope"/> handed to an inbox handler by the scoped transactional store.
/// </summary>
public static class CosmosInboxTransactionScopeExtensions
{
	/// <summary>
	/// Obtains the Cosmos DB <see cref="TransactionalBatch"/> from the opaque inbox transaction scope, so a
	/// handler can add its own operations to the batch and have them commit atomically with the processed-mark.
	/// </summary>
	/// <param name="scope">The opaque scope handed to the handler by the Cosmos DB scoped transactional store.</param>
	/// <returns>The transactional batch the handler's operations should be added to.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="scope"/> did not originate from the Cosmos DB inbox store (a wrong-provider
	/// scope, for example a MongoDB scope). This fails loud rather than returning <see langword="null"/> or an
	/// obscure cast failure, surfacing a provider mismatch immediately.
	/// </exception>
	public static TransactionalBatch AsCosmosBatch(this IInboxTransactionScope scope)
	{
		ArgumentNullException.ThrowIfNull(scope);

		if (scope is CosmosInboxTransactionScope cosmosScope)
		{
			return cosmosScope.Batch;
		}

		throw new InvalidOperationException(
			$"The inbox transaction scope of type '{scope.GetType().FullName}' is not a Cosmos DB scope. " +
			$"'{nameof(AsCosmosBatch)}' may only be called on a scope produced by the Cosmos DB inbox store; " +
			"ensure the registered inbox store is the Cosmos DB store before enlisting Cosmos DB writes.");
	}
}
