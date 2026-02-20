// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Provides functionality to initialize Elasticsearch indexes.
/// </summary>
public interface IIndexInitializer
{
	/// <summary>
	/// Initializes all Elasticsearch indexes by invoking the <see cref="IInitializeElasticIndex.InitializeAsync" /> method on each
	/// registered initializer.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous initialization operation. </returns>
	/// <remarks>
	/// This method iterates through all registered <see cref="IInitializeElasticIndex" /> instances and ensures that each one is
	/// executed sequentially.
	/// </remarks>
	Task InitializeIndexesAsync();
}
