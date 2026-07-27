// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Dispatch;

/// <summary>
/// Extensions for obtaining the relational BCL transaction handle from an opaque
/// <see cref="IInboxTransactionScope"/> handed to an inbox handler by a relational scoped transactional store
/// (SqlServer, Postgres).
/// </summary>
public static class SqlInboxTransactionScopeExtensions
{
	/// <summary>
	/// Obtains the active BCL <see cref="IDbTransaction"/> from the opaque inbox transaction scope, so a
	/// handler can enlist its own writes atomically with the processed-mark.
	/// </summary>
	/// <param name="scope">The opaque scope handed to the handler by a relational scoped transactional store.</param>
	/// <returns>The active local database transaction the handler's writes should enlist in.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="scope"/> did not originate from a relational inbox store (a wrong-provider
	/// scope, for example a MongoDB or Cosmos DB scope). This fails loud rather than returning
	/// <see langword="null"/> or an obscure cast failure, surfacing a provider mismatch immediately.
	/// </exception>
	public static IDbTransaction AsSqlTransaction(this IInboxTransactionScope scope)
	{
		ArgumentNullException.ThrowIfNull(scope);

		if (scope is SqlInboxTransactionScope sqlScope)
		{
			return sqlScope.Transaction;
		}

		throw new InvalidOperationException(
			$"The inbox transaction scope of type '{scope.GetType().FullName}' is not a relational (SQL) scope. " +
			$"'{nameof(AsSqlTransaction)}' may only be called on a scope produced by a relational inbox store " +
			"(SqlServer or Postgres); ensure the registered inbox store is a relational store before enlisting SQL writes.");
	}
}
