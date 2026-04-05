// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Extension methods for <see cref="IIndexOperationsManager"/>.
/// </summary>
public static class IndexOperationsManagerExtensions
{
	/// <summary>Optimizes index settings based on usage patterns.</summary>
	public static Task<IndexOptimizationResult> OptimizeIndexAsync(this IIndexOperationsManager manager, string indexName, IndexOptimizationOptions optimizationOptions, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(manager);
		return ((IndexOperationsManager)manager).OptimizeIndexAsync(indexName, optimizationOptions, cancellationToken);
	}

	/// <summary>Forces a merge operation on the specified index.</summary>
	public static Task<bool> ForceMergeAsync(this IIndexOperationsManager manager, string indexName, int? maxNumSegments, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(manager);
		return ((IndexOperationsManager)manager).ForceMergeAsync(indexName, maxNumSegments, cancellationToken);
	}
}
