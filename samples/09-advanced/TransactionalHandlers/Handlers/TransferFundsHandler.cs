// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Data.SqlClient;

using TransactionalHandlers.Commands;
using TransactionalHandlers.Requests;

namespace TransactionalHandlers.Handlers;

/// <summary>
/// Handles the <see cref="TransferFunds"/> command within a transaction.
/// </summary>
/// <remarks>
/// <para>
/// The <c>TransactionMiddleware</c> wraps this handler's execution in a
/// <see cref="System.Transactions.TransactionScope"/>. Connections created
/// by the factory auto-enlist in the ambient transaction.
/// </para>
/// <para>
/// If either the debit or credit fails, the transaction rolls back automatically --
/// no manual transaction management required.
/// </para>
/// </remarks>
public sealed class TransferFundsHandler : IActionHandler<TransferFunds>
{
	private readonly Func<SqlConnection> _connectionFactory;

	public TransferFundsHandler(Func<SqlConnection> connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task HandleAsync(TransferFunds command, CancellationToken cancellationToken)
	{
		// Both operations use connections from the factory.
		// TransactionMiddleware has already started a TransactionScope,
		// so these connections auto-enlist in it.

		// Debit the source account
		using (var connection = _connectionFactory())
		{
			var debitRequest = new DebitAccount(command.FromAccountId, command.Amount, cancellationToken: cancellationToken);
			var affectedRows = await connection.Ready().ResolveAsync(debitRequest).ConfigureAwait(false);

			if (affectedRows == 0)
			{
				throw new InvalidOperationException(
					$"Insufficient funds in account {command.FromAccountId} for transfer of {command.Amount}");
			}
		}

		// Credit the destination account
		using (var connection = _connectionFactory())
		{
			var creditRequest = new CreditAccount(command.ToAccountId, command.Amount, cancellationToken: cancellationToken);
			await connection.Ready().ResolveAsync(creditRequest).ConfigureAwait(false);
		}

		// If we reach here, TransactionMiddleware commits.
		// If any exception was thrown, TransactionMiddleware rolls back both operations.
	}
}
