// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Excalibur.Dispatch.Patterns.CQRS.CQRS.Commands;

namespace examples.Excalibur.Patterns.CQRS.Examples.Banking.Commands;

/// <summary>
///     Command to transfer money between accounts.
/// </summary>
public class TransferMoneyCommand : CommandBase<string>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="TransferMoneyCommand" /> class.
	/// </summary>
	public TransferMoneyCommand(string fromAccountId, string toAccountId, decimal amount, string? reference)
	{
		if (string.IsNullOrWhiteSpace(fromAccountId))
		{
			throw new ArgumentException("Source account ID is required", nameof(fromAccountId));
		}

		if (string.IsNullOrWhiteSpace(toAccountId))
		{
			throw new ArgumentException("Destination account ID is required", nameof(toAccountId));
		}

		if (amount <= 0)
		{
			throw new ArgumentException("Transfer amount must be positive", nameof(amount));
		}

		FromAccountId = fromAccountId;
		ToAccountId = toAccountId;
		Amount = amount;
		Reference = reference ?? $"TRF-{Guid.NewGuid():N}";
	}

	/// <summary>
	///     Gets the source account ID.
	/// </summary>
	public string FromAccountId { get; }

	/// <summary>
	///     Gets the destination account ID.
	/// </summary>
	public string ToAccountId { get; }

	/// <summary>
	///     Gets the transfer amount.
	/// </summary>
	public decimal Amount { get; }

	/// <summary>
	///     Gets the transfer Excalibur.Dispatch.Transport.Kafka.
	/// </summary>
	public string Reference { get; }
}
