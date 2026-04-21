// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;

using OpenSearch.Client;

namespace Excalibur.Data.OpenSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing OpenSearch index aliases.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="IndexAliasManager" /> class. </remarks>
/// <param name="client"> The OpenSearch client instance. </param>
/// <param name="logger"> The logger instance. </param>
/// <exception cref="ArgumentNullException"> Thrown if any parameter is null. </exception>
internal sealed class IndexAliasManager(IOpenSearchClient client, ILogger<IndexAliasManager> logger) : IIndexAliasManager
{
	private readonly IOpenSearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly ILogger<IndexAliasManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task<bool> CreateAliasAsync(string aliasName, IEnumerable<string> indexNames,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aliasName);
		ArgumentNullException.ThrowIfNull(indexNames);

		var indexNamesList = indexNames.ToList();
		if (indexNamesList.Count == 0)
		{
			throw new ArgumentException("Index names collection cannot be empty", nameof(indexNames));
		}

		try
		{
			_logger.LogInformation(
				"Creating alias: {AliasName} for indices: {IndexNames}",
				aliasName, string.Join(", ", indexNamesList));

			var response = await _client.Indices.BulkAliasAsync(b =>
			{
				foreach (var indexName in indexNamesList)
				{
					_ = b.Add(a => a.Index(indexName).Alias(aliasName));
				}

				return b;
			}, cancellationToken).ConfigureAwait(false);

			if (response.IsValid)
			{
				_logger.LogInformation("Successfully created alias: {AliasName}", aliasName);
				return true;
			}

			_logger.LogError(
				"Failed to create alias: {AliasName}. Error: {Error}",
				aliasName, response.DebugInformation);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create alias: {AliasName}", aliasName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAliasAsync(string aliasName, IEnumerable<string>? indexNames,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aliasName);

		try
		{
			_logger.LogInformation(
				"Deleting alias: {AliasName} from indices: {IndexNames}",
				aliasName, indexNames != null ? string.Join(", ", indexNames) : "all");

			var response = await _client.Indices.BulkAliasAsync(b =>
			{
				if (indexNames != null)
				{
					foreach (var indexName in indexNames)
					{
						_ = b.Remove(r => r.Index(indexName).Alias(aliasName));
					}
				}
				else
				{
					_ = b.Remove(r => r.Index("*").Alias(aliasName));
				}

				return b;
			}, cancellationToken).ConfigureAwait(false);

			if (response.IsValid)
			{
				_logger.LogInformation("Successfully deleted alias: {AliasName}", aliasName);
				return true;
			}

			_logger.LogError(
				"Failed to delete alias: {AliasName}. Error: {Error}",
				aliasName, response.DebugInformation);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete alias: {AliasName}", aliasName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> AliasExistsAsync(string aliasName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aliasName);

		try
		{
			var response = await _client.Indices.AliasExistsAsync(aliasName, ct: cancellationToken).ConfigureAwait(false);
			return response.Exists;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check if alias exists: {AliasName}", aliasName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<AliasDefinition>> GetAliasesAsync(
		string? aliasPattern,
		CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogInformation("Getting aliases with pattern: {Pattern}", aliasPattern ?? "all");

			var response = await _client.Indices.GetAliasAsync(
				Indices.All,
				a => a.Name(aliasPattern ?? "*"),
				cancellationToken).ConfigureAwait(false);

			if (response.IsValid)
			{
				var aliases = new List<AliasDefinition>();
				if (response.Indices != null)
				{
					foreach (var indexEntry in response.Indices)
					{
						foreach (var alias in indexEntry.Value.Aliases)
						{
							aliases.Add(new AliasDefinition
							{
								AliasName = alias.Key,
								Indices = [indexEntry.Key.ToString()],
								Filter = alias.Value.Filter as QueryContainer,
								IndexRouting = alias.Value.IndexRouting,
								SearchRouting = alias.Value.SearchRouting,
								IsWriteIndex = alias.Value.IsWriteIndex,
							});
						}
					}
				}

				return aliases;
			}

			_logger.LogError("Failed to get aliases. Error: {Error}", response.DebugInformation);
			return [];
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get aliases with pattern: {Pattern}", aliasPattern ?? "all");
			return [];
		}
	}

	/// <inheritdoc />
	public async Task<bool> UpdateAliasesAsync(IEnumerable<AliasOperation> operations, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operations);

		var operationsList = operations.ToList();
		if (operationsList.Count == 0)
		{
			throw new ArgumentException("Operations collection cannot be empty", nameof(operations));
		}

		try
		{
			_logger.LogInformation("Executing {Count} alias operations", operationsList.Count);

			var response = await _client.Indices.BulkAliasAsync(b =>
			{
				foreach (var op in operationsList)
				{
					if (op.OperationType == AliasOperationType.Add)
					{
						_ = b.Add(a => a.Index(op.IndexName).Alias(op.AliasName));
					}
					else
					{
						_ = b.Remove(r => r.Index(op.IndexName).Alias(op.AliasName));
					}
				}

				return b;
			}, cancellationToken).ConfigureAwait(false);

			if (response.IsValid)
			{
				_logger.LogInformation("Successfully executed alias operations");
				return true;
			}

			_logger.LogError("Failed to execute alias operations. Error: {Error}", response.DebugInformation);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to execute alias operations");
			return false;
		}
	}
}
