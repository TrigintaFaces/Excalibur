// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Provides functionality to initialize an Elasticsearch index.
/// </summary>
public interface IInitializeElasticIndex
{
	/// <summary>
	/// Initializes the Elasticsearch index asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	Task InitializeAsync(CancellationToken cancellationToken);
}
