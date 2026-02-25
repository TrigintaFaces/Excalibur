// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing Elasticsearch index aliases.
/// </summary>
public interface IIndexAliasManager
{
	/// <summary>
	/// Creates or updates an alias for the specified indices.
	/// </summary>
	/// <param name="aliasName"> The name of the alias. </param>
	/// <param name="indexNames"> The names of the indices to include in the alias. </param>
	/// <param name="aliasConfiguration"> Optional alias configuration settings. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> CreateAliasAsync(string aliasName, IEnumerable<string> indexNames, Alias? aliasConfiguration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes an alias from Elasticsearch.
	/// </summary>
	/// <param name="aliasName"> The name of the alias to delete. </param>
	/// <param name="indexNames"> The names of the indices to remove the alias from. If null, removes from all indices. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> DeleteAliasAsync(string aliasName, IEnumerable<string>? indexNames, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if an alias exists in Elasticsearch.
	/// </summary>
	/// <param name="aliasName"> The name of the alias to check. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the alias exists. </returns>
	Task<bool> AliasExistsAsync(string aliasName, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all aliases that match the specified pattern.
	/// </summary>
	/// <param name="aliasPattern"> The pattern to match alias names against. If null, returns all aliases. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{IEnumerable}" /> containing the matching aliases. </returns>
	Task<IEnumerable<AliasDefinition>> GetAliasesAsync(string? aliasPattern, CancellationToken cancellationToken);

	/// <summary>
	/// Performs atomic alias operations to switch indices behind an alias.
	/// </summary>
	/// <param name="operations"> The alias operations to perform atomically. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> UpdateAliasesAsync(IEnumerable<AliasOperation> operations, CancellationToken cancellationToken);
}
