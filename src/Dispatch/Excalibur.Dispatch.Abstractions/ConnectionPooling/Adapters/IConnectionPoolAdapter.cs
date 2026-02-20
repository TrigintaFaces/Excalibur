// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Base interface for adapters that bridge incompatible IConnectionPool variants to the unified interface. Provides common functionality
/// for all connection pool adapters.
/// </summary>
/// <typeparam name="TConnection"> The type of connection managed by the pool. </typeparam>
public interface IConnectionPoolAdapter<TConnection> : IUnifiedConnectionPool<TConnection>
	where TConnection : class
{
	/// <summary>
	/// Gets the name of the adapter implementation for diagnostics.
	/// </summary>
	/// <value> The adapter implementation name. </value>
	string AdapterName { get; }

	/// <summary>
	/// Gets whether the underlying pool supports the requested operation.
	/// </summary>
	/// <param name="operation"> The operation to check. </param>
	/// <returns> <see langword="true" /> when the operation is supported; otherwise, <see langword="false" />. </returns>
	bool SupportsOperation(PoolOperation operation);
}
