// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing the lifecycle of Elasticsearch indices including rollover, aging, and deletion.
/// </summary>
public partial class IndexLifecycleManager : IIndexLifecycleManager
{
	private readonly IIndexLifecycleOperations _ops;
	private readonly ILogger<IndexLifecycleManager> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="IndexLifecycleManager"/> class.
	/// </summary>
	/// <param name="client"> The Elasticsearch client instance. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <exception cref="ArgumentNullException"> Thrown if any parameter is null. </exception>
	public IndexLifecycleManager(ElasticsearchClient client, ILogger<IndexLifecycleManager> logger)
		: this(CreateOperations(client), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="IndexLifecycleManager"/>
	/// class using a pre-built operations adapter. Used by tests to
	/// substitute the SDK via the <see cref="IIndexLifecycleOperations"/>
	/// seam (ADR-142 §D7, S800 bd-qhzapp).
	/// </summary>
	internal IndexLifecycleManager(IIndexLifecycleOperations ops, ILogger<IndexLifecycleManager> logger)
	{
		_ops = ops ?? throw new ArgumentNullException(nameof(ops));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<bool> CreateLifecyclePolicyAsync(string policyName, IndexLifecyclePolicy policy,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
		ArgumentNullException.ThrowIfNull(policy);

		try
		{
			LogCreatingLifecyclePolicy(policyName);

			var result = await _ops.PutPolicyAsync(policyName, policy, cancellationToken).ConfigureAwait(false);

			if (result.Success)
			{
				LogLifecyclePolicyCreated(policyName);
				return true;
			}

			LogLifecyclePolicyCreationFailed(policyName, result.ErrorDetails ?? "Unknown error");
			return false;
		}
		catch (Exception ex)
		{
			LogLifecyclePolicyCreationException(policyName, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteLifecyclePolicyAsync(string policyName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

		try
		{
			LogDeletingLifecyclePolicy(policyName);

			var outcome = await _ops.DeletePolicyAsync(policyName, cancellationToken).ConfigureAwait(false);

			switch (outcome)
			{
				case LifecyclePolicyDeleteOutcome.Deleted:
					LogLifecyclePolicyDeleted(policyName);
					return true;
				case LifecyclePolicyDeleteOutcome.NotFound:
					LogLifecyclePolicyNotFound(policyName);
					return false;
				default:
					LogLifecyclePolicyDeletionFailed(policyName, "Unknown error");
					return false;
			}
		}
		catch (Exception ex)
		{
			LogLifecyclePolicyDeletionException(policyName, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<IndexRolloverResult> RolloverIndexAsync(string aliasName, RolloverConditions rolloverConditions,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aliasName);
		ArgumentNullException.ThrowIfNull(rolloverConditions);

		try
		{
			LogRollingOverIndex(aliasName);

			var result = await _ops.RolloverAsync(aliasName, rolloverConditions, cancellationToken).ConfigureAwait(false);

			if (result.Success)
			{
				LogIndexRolledOver(aliasName, result.RolledOver, result.NewIndex ?? "unknown");
				return new IndexRolloverResult
				{
					IsSuccessful = true,
					RolledOver = result.RolledOver,
					OldIndex = result.OldIndex,
					NewIndex = result.NewIndex,
				};
			}

			LogIndexRolloverFailed(aliasName, result.ErrorDetails ?? "Unknown error");
			return new IndexRolloverResult
			{
				IsSuccessful = false,
				RolledOver = false,
				Errors = [result.ErrorDetails ?? "Unknown error"],
			};
		}
		catch (Exception ex)
		{
			LogIndexRolloverException(aliasName, ex);
			return new IndexRolloverResult { IsSuccessful = false, RolledOver = false, Errors = [ex.Message] };
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<IndexLifecycleStatus>> GetIndexLifecycleStatusAsync(
		string? indexPattern,
		CancellationToken cancellationToken)
	{
		try
		{
			var pattern = indexPattern ?? "*";
			LogGettingLifecycleStatus(pattern);

			var statuses = await _ops.GetStatusAsync(pattern, cancellationToken).ConfigureAwait(false);

			LogLifecycleStatusRetrieved(pattern, statuses.Count);
			return statuses;
		}
		catch (Exception ex)
		{
			LogLifecycleStatusException(indexPattern ?? "*", ex);
			return [];
		}
	}

	/// <inheritdoc />
	public async Task<bool> MoveToNextPhaseAsync(string indexPattern, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexPattern);

		try
		{
			LogMovingToNextPhase(indexPattern);

			var statuses = await GetIndexLifecycleStatusAsync(indexPattern, cancellationToken).ConfigureAwait(false);
			var statusList = statuses.ToList();

			if (statusList.Count == 0)
			{
				LogNoIndicesFoundForPhaseMove(indexPattern);
				return false;
			}

			var allSucceeded = true;

			foreach (var status in statusList)
			{
				if (string.IsNullOrEmpty(status.PolicyName))
				{
					LogIndexHasNoPolicy(status.IndexName);
					continue;
				}

				var nextPhase = GetNextPhase(status.Phase);
				if (nextPhase is null)
				{
					LogIndexAlreadyInFinalPhase(status.IndexName, status.Phase);
					continue;
				}

				var result = await _ops
					.MoveToPhaseAsync(status.IndexName, status.Phase, nextPhase, cancellationToken)
					.ConfigureAwait(false);

				if (result.Success)
				{
					LogIndexMovedToNextPhase(status.IndexName, status.Phase, nextPhase);
				}
				else
				{
					LogIndexMoveToNextPhaseFailed(status.IndexName, status.Phase, result.ErrorDetails ?? "Unknown error");
					allSucceeded = false;
				}
			}

			return allSucceeded;
		}
		catch (Exception ex)
		{
			LogMoveToNextPhaseException(indexPattern, ex);
			return false;
		}
	}

	/// <summary>
	/// Gets the next phase in the ILM lifecycle.
	/// </summary>
	private static string? GetNextPhase(string currentPhase)
	{
		return currentPhase.ToLowerInvariant() switch
		{
			"hot" => "warm",
			"warm" => "cold",
			"cold" => "delete",
			_ => null,
		};
	}

	private static IIndexLifecycleOperations CreateOperations(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		return new IndexLifecycleOperationsAdapter(client);
	}
}
