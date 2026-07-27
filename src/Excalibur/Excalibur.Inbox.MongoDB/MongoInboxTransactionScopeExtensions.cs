// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.MongoDB;

using MongoDB.Driver;

namespace Excalibur.Dispatch;

/// <summary>
/// Provider-specific extensions for obtaining the MongoDB-native transaction handle from an opaque
/// <see cref="IInboxTransactionScope"/> handed to an inbox handler by the scoped transactional store.
/// </summary>
public static class MongoInboxTransactionScopeExtensions
{
	/// <summary>
	/// Obtains the MongoDB <see cref="IClientSessionHandle"/> carrying the active transaction from the opaque
	/// inbox transaction scope, so a handler can enlist its own writes atomically with the processed-mark.
	/// </summary>
	/// <param name="scope">The opaque scope handed to the handler by the MongoDB scoped transactional store.</param>
	/// <returns>The client session handle whose transaction the handler's writes should enlist in.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="scope"/> did not originate from the MongoDB inbox store (a wrong-provider
	/// scope, for example a Cosmos DB scope). This fails loud rather than returning <see langword="null"/> or an
	/// obscure cast failure, surfacing a provider mismatch immediately.
	/// </exception>
	public static IClientSessionHandle AsMongoSession(this IInboxTransactionScope scope)
	{
		ArgumentNullException.ThrowIfNull(scope);

		if (scope is MongoInboxTransactionScope mongoScope)
		{
			return mongoScope.Session;
		}

		throw new InvalidOperationException(
			$"The inbox transaction scope of type '{scope.GetType().FullName}' is not a MongoDB scope. " +
			$"'{nameof(AsMongoSession)}' may only be called on a scope produced by the MongoDB inbox store; " +
			"ensure the registered inbox store is the MongoDB store before enlisting MongoDB writes.");
	}
}
