// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Defines the contract for an in-memory CDC processor.
/// </summary>
/// <remarks>
/// <para>
/// This processor handles simulated CDC changes from an <see cref="IInMemoryCdcStore"/>
/// for testing scenarios.
/// </para>
/// </remarks>
public interface IInMemoryCdcProcessor : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Processes all pending CDC changes from the store.
	/// </summary>
	/// <param name="changeHandler">A delegate that handles each CDC change.</param>
	/// <param name="cancellationToken">A token to observe while processing.</param>
	/// <returns>The total number of changes processed.</returns>
	Task<int> ProcessChangesAsync(
		Func<InMemoryCdcChange, CancellationToken, Task> changeHandler,
		CancellationToken cancellationToken);
}
