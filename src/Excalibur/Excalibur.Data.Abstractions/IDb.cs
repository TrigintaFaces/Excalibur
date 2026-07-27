// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data;

/// <summary>
/// Defines a contract for database access, providing basic operations to manage a database connection.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IDb"/> models the <em>connection lifecycle</em> only (open/close and access to the underlying
/// <see cref="IDbConnection"/>); it does not itself execute commands. Command execution — and therefore the
/// mapping of an optimistic-concurrency conflict to a typed <see cref="ConcurrencyException"/> — happens on
/// the execute path over <see cref="Connection"/>, notably <see cref="DbConnectionExtensions"/>. That path
/// propagates a <see cref="ConcurrencyException"/> (and any other application exception) to the caller
/// unchanged; only non-application infrastructure faults are wrapped as an <see cref="OperationFailedException"/>.
/// </para>
/// <para>
/// The lifecycle members below neither perform writes nor raise <see cref="ConcurrencyException"/>: an
/// implementation MUST NOT repurpose them to surface conflict semantics.
/// </para>
/// </remarks>
public interface IDb
{
	/// <summary>
	/// Gets the underlying database connection.
	/// </summary>
	/// <value>
	/// The underlying database connection.
	/// </value>
	IDbConnection Connection { get; }

	/// <summary>
	/// Opens the database connection.
	/// </summary>
	void Open();

	/// <summary>
	/// Closes the database connection.
	/// </summary>
	void Close();

	/// <summary>
	/// Opens the database connection asynchronously.
	/// Default implementation delegates to the synchronous <see cref="Open"/> method.
	/// Implementations backed by <see cref="System.Data.Common.DbConnection"/> should override
	/// to call <see cref="System.Data.Common.DbConnection.OpenAsync(CancellationToken)"/>.
	/// </summary>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous open operation.</returns>
	Task OpenAsync(CancellationToken cancellationToken)
	{
		Open();
		return Task.CompletedTask;
	}

	/// <summary>
	/// Closes the database connection asynchronously.
	/// Default implementation delegates to the synchronous <see cref="Close"/> method.
	/// Implementations backed by <see cref="System.Data.Common.DbConnection"/> should override
	/// to call <see cref="System.Data.Common.DbConnection.CloseAsync"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous close operation.</returns>
	Task CloseAsync()
	{
		Close();
		return Task.CompletedTask;
	}
}
