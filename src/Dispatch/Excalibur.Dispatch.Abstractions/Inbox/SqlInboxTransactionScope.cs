// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Dispatch;

/// <summary>
/// Relational implementation of the opaque <see cref="IInboxTransactionScope"/>, wrapping the active BCL
/// <see cref="IDbTransaction"/> whose transaction enlists an inbox handler's own writes together with the
/// processed-mark.
/// </summary>
/// <remarks>
/// The relational inbox providers (SqlServer, Postgres) drive an exactly-once
/// <see cref="IScopedTransactionalInboxStore"/> over a single local <see cref="IDbTransaction"/>. Because the
/// underlying handle is the BCL <see cref="IDbTransaction"/> — not a provider-native type — a single shared
/// scope and a single <c>AsSqlTransaction()</c> accessor serve every relational provider (unlike the
/// per-provider MongoDB/Cosmos scopes, whose handles have no BCL equivalent).
/// </remarks>
public sealed class SqlInboxTransactionScope : IInboxTransactionScope
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SqlInboxTransactionScope"/> class.
	/// </summary>
	/// <param name="transaction">The active local database transaction the handler's writes should enlist in.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="transaction"/> is <see langword="null"/>.</exception>
	public SqlInboxTransactionScope(IDbTransaction transaction)
	{
		ArgumentNullException.ThrowIfNull(transaction);
		Transaction = transaction;
	}

	/// <summary>
	/// Gets the active local database transaction. Handler writes issued on
	/// <see cref="IDbTransaction.Connection"/> and passed <c>transaction: this</c> commit atomically with the
	/// processed-mark.
	/// </summary>
	public IDbTransaction Transaction { get; }
}
