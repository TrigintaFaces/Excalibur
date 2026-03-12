// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Feature interface for transaction context during message processing.
/// </summary>
public interface IMessageTransactionFeature
{
	/// <summary>
	/// Gets or sets the active transaction for this message processing.
	/// </summary>
	/// <value>The active transaction object, or <see langword="null"/> if no transaction.</value>
	object? Transaction { get; set; }

	/// <summary>
	/// Gets or sets the unique identifier of the active transaction.
	/// </summary>
	/// <value>The transaction identifier, or <see langword="null"/> if no transaction.</value>
	string? TransactionId { get; set; }
}
