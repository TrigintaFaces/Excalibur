// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Executes an <see cref="IDataRequest{TConnection,TModel}"/> over an <see cref="IDbConnection"/> the
/// provider supplies and owns. Obtain via <see cref="IPersistenceProvider.GetService"/> with
/// <c>typeof(IDataRequestExecutor)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This capability is separate from <see cref="IPersistenceProvider"/> because the set of providers that
/// can honour it is narrower than the set of providers that exist. Executing a data request this way
/// requires the store to be reachable through an <see cref="IDbConnection"/>; document, key-value and
/// search stores are not, and have their own execution surfaces — a document store's is
/// <see cref="IDocumentPersistenceProvider"/>. A provider that cannot honour this contract declines it
/// from <see cref="IPersistenceProvider.GetService"/> rather than advertising a member that throws.
/// </para>
/// <para>
/// The connection type is fixed to <see cref="IDbConnection"/> rather than left open, because the
/// provider — not the caller — decides which connection it opens. A caller-named connection type could
/// only ever agree with the provider's own by coincidence, and disagreeing would be discovered at
/// runtime rather than at compile time.
/// </para>
/// </remarks>
public interface IDataRequestExecutor
{
	/// <summary>
	/// Executes a data request against a connection opened by this provider, applying the provider's
	/// retry policy.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The data request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the data request execution. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="request"/> is <see langword="null"/>. </exception>
	/// <exception cref="ObjectDisposedException"> The provider has been disposed. </exception>
	Task<TResult> ExecuteAsync<TResult>(
		IDataRequest<IDbConnection, TResult> request,
		CancellationToken cancellationToken);
}
