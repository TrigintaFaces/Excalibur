// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Resilience;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Exposes how a persistence provider reaches its store, and how it behaves when reaching it fails.
/// Obtain via <see cref="IPersistenceProvider.GetService"/> with
/// <c>typeof(IPersistenceProviderConnection)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This capability answers "where is the store, and what does this provider do about transient
/// failure". It says nothing about whether the store can run a multi-statement transaction — that is
/// <see cref="IPersistenceProviderTransaction"/>, resolved separately.
/// </para>
/// <para>
/// The two are separate because the sets of providers that can honour them differ. Every provider that
/// holds a connection can describe it and can describe its retry policy. Only stores with an ambient,
/// open-ended transaction model can honour a transaction scope; document and key-value stores
/// generally cannot. A single contract spanning both would force those providers to choose between
/// advertising a transaction facility they cannot deliver and withholding connection details they can,
/// and neither answer is true.
/// </para>
/// </remarks>
public interface IPersistenceProviderConnection
{
	/// <summary>
	/// Gets the connection string or connection configuration for the provider.
	/// </summary>
	/// <value>
	/// The connection string or connection configuration for the provider.
	/// </value>
	string ConnectionString { get; }

	/// <summary>
	/// Gets the retry policy used for DataRequest execution.
	/// </summary>
	/// <value>
	/// The retry policy used for DataRequest execution.
	/// </value>
	IDataRequestRetryPolicy RetryPolicy { get; }
}
