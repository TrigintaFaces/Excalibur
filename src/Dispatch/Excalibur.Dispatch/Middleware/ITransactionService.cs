// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Service interface for managing transaction lifecycle.
/// </summary>
public interface ITransactionService
{
	/// <summary>
	/// Begins a new transaction with the specified configuration.
	/// </summary>
	/// <param name="configuration"> Transaction configuration. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A transaction instance. </returns>
	Task<ITransaction> BeginTransactionAsync(
		object configuration,
		CancellationToken cancellationToken);
}
