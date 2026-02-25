// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Processing;

/// <summary>
/// Defines the contract for a CDC background processor that can be invoked by the hosted service.
/// </summary>
/// <remarks>
/// <para>
/// This interface is provider-agnostic and lives in <c>Excalibur.Cdc</c> so the hosted service
/// does not depend on any specific provider package (SQL Server, Postgres, etc.).
/// </para>
/// <para>
/// Provider-specific extensions register an implementation of this interface in DI.
/// The <see cref="CdcProcessingHostedService"/> resolves it to drive the polling loop.
/// </para>
/// </remarks>
public interface ICdcBackgroundProcessor
{
	/// <summary>
	/// Processes a single batch of CDC changes.
	/// </summary>
	/// <param name="cancellationToken">A token to observe while processing.</param>
	/// <returns>The number of changes processed in this batch.</returns>
	/// <exception cref="OperationCanceledException">
	/// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
	/// </exception>
	Task<int> ProcessChangesAsync(CancellationToken cancellationToken);
}
