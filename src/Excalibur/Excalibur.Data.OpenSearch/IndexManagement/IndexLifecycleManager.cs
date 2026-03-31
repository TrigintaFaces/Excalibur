// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Microsoft.Extensions.Logging;

using OpenSearch.Client;
using OpenSearch.Net;

using HttpMethod = OpenSearch.Net.HttpMethod;

namespace Excalibur.Data.OpenSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing the lifecycle of OpenSearch indices including rollover, aging, and deletion.
/// </summary>
/// <remarks>
/// <para>
/// OpenSearch uses Index State Management (ISM) instead of Elasticsearch's Index Lifecycle Management (ILM).
/// ISM policies are managed via the <c>_plugins/_ism/policies</c> REST API.
/// </para>
/// <para>
/// Initializes a new instance of the <see cref="IndexLifecycleManager" /> class.
/// </para>
/// </remarks>
/// <param name="client"> The OpenSearch client instance. </param>
/// <param name="logger"> The logger instance. </param>
/// <exception cref="ArgumentNullException"> Thrown if any parameter is null. </exception>
internal partial class IndexLifecycleManager(IOpenSearchClient client, ILogger<IndexLifecycleManager> logger) : IIndexLifecycleManager
{
	private readonly IOpenSearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly ILogger<IndexLifecycleManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task<bool> CreateLifecyclePolicyAsync(string policyName, IndexLifecyclePolicy policy,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
		ArgumentNullException.ThrowIfNull(policy);

		try
		{
			LogCreatingIsmPolicy(policyName);

			// OpenSearch ISM policies are managed via REST API: PUT _plugins/_ism/policies/{policyName}
			var ismPolicyBody = BuildIsmPolicyBody(policy);
			var response = await _client.LowLevel.DoRequestAsync<StringResponse>(
				HttpMethod.PUT,
				$"_plugins/_ism/policies/{Uri.EscapeDataString(policyName)}",
				cancellationToken,
				PostData.String(ismPolicyBody)).ConfigureAwait(false);

			if (response.Success)
			{
				LogIsmPolicyCreated(policyName);
				return true;
			}

			LogIsmPolicyCreationFailed(policyName, response.Body ?? "Unknown error");
			return false;
		}
		catch (Exception ex)
		{
			LogIsmPolicyCreationException(policyName, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteLifecyclePolicyAsync(string policyName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

		try
		{
			LogDeletingIsmPolicy(policyName);

			var response = await _client.LowLevel.DoRequestAsync<StringResponse>(
				HttpMethod.DELETE,
				$"_plugins/_ism/policies/{Uri.EscapeDataString(policyName)}",
				cancellationToken).ConfigureAwait(false);

			if (response.Success)
			{
				LogIsmPolicyDeleted(policyName);
				return true;
			}

			// Policy not found is not necessarily an error
			if (response.HttpStatusCode == 404)
			{
				LogIsmPolicyNotFound(policyName);
				return false;
			}

			LogIsmPolicyDeletionFailed(policyName, response.Body ?? "Unknown error");
			return false;
		}
		catch (Exception ex)
		{
			LogIsmPolicyDeletionException(policyName, ex);
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

			var response = await _client.Indices.RolloverAsync(aliasName, r => r
				.Conditions(c =>
				{
					if (rolloverConditions.MaxAge.HasValue)
					{
						_ = c.MaxAge($"{(long)rolloverConditions.MaxAge.Value.TotalDays}d");
					}

					if (rolloverConditions.MaxDocs.HasValue)
					{
						_ = c.MaxDocs(rolloverConditions.MaxDocs.Value);
					}

					if (!string.IsNullOrEmpty(rolloverConditions.MaxSize))
					{
						_ = c.MaxSize(rolloverConditions.MaxSize);
					}

					// Note: MaxPrimaryShardSize not available in OpenSearch.Client RolloverConditionsDescriptor

					return c;
				}), cancellationToken).ConfigureAwait(false);

			if (response.IsValid)
			{
				LogIndexRolledOver(aliasName, response.RolledOver, response.NewIndex ?? "unknown");
				return new IndexRolloverResult
				{
					IsSuccessful = true,
					RolledOver = response.RolledOver,
					OldIndex = response.OldIndex,
					NewIndex = response.NewIndex
				};
			}

			var errorReason = response.ServerError?.Error?.Reason ?? "Unknown error";
			LogIndexRolloverFailed(aliasName, errorReason);
			return new IndexRolloverResult
			{
				IsSuccessful = false,
				RolledOver = false,
				Errors = [errorReason]
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
			LogGettingIsmStatus(pattern);

			// OpenSearch ISM explain API: GET _plugins/_ism/explain/{index}
			var response = await _client.LowLevel.DoRequestAsync<StringResponse>(
				HttpMethod.GET,
				$"_plugins/_ism/explain/{Uri.EscapeDataString(pattern)}",
				cancellationToken).ConfigureAwait(false);

			if (!response.Success)
			{
				LogIsmStatusFailed(pattern, response.Body ?? "Unknown error");
				return [];
			}

			var statuses = new List<IndexLifecycleStatus>();

			if (!string.IsNullOrEmpty(response.Body))
			{
				var jsonDoc = JsonDocument.Parse(response.Body);

				// ISM explain response has index names as top-level keys
				foreach (var indexProperty in jsonDoc.RootElement.EnumerateObject())
				{
					// Skip metadata keys like "total_managed_indices"
					if (indexProperty.Value.ValueKind != JsonValueKind.Object)
					{
						continue;
					}

					var indexName = indexProperty.Name;
					var indexData = indexProperty.Value;

					// ISM uses "policy_id" and state structure differs from ILM
					var policyId = indexData.TryGetProperty("policy_id", out var policyElement)
						? policyElement.GetString()
						: null;

					// ISM state is nested under "state"
					var phase = "unknown";
					if (indexData.TryGetProperty("state", out var stateElement) &&
						stateElement.TryGetProperty("name", out var stateNameElement))
					{
						phase = stateNameElement.GetString() ?? "unknown";
					}

					statuses.Add(new IndexLifecycleStatus
					{
						IndexName = indexName,
						Phase = phase,
						PolicyName = policyId,
						Age = null // ISM does not directly expose index age in explain
					});
				}
			}

			LogIsmStatusRetrieved(pattern, statuses.Count);
			return statuses;
		}
		catch (Exception ex)
		{
			LogIsmStatusException(indexPattern ?? "*", ex);
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

			// First, get the current ISM status for matching indices
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

				// Determine the next phase based on current phase
				var nextPhase = GetNextPhase(status.Phase);
				if (nextPhase == null)
				{
					LogIndexAlreadyInFinalPhase(status.IndexName, status.Phase);
					continue;
				}

				// Use ISM change policy or retry API to force transition
				// POST _plugins/_ism/change_policy/{index}
				var changePolicyBody = JsonSerializer.Serialize(new
				{
					state = nextPhase
				});

				var response = await _client.LowLevel.DoRequestAsync<StringResponse>(
					HttpMethod.POST,
					$"_plugins/_ism/retry/{Uri.EscapeDataString(status.IndexName)}",
					cancellationToken,
					PostData.String(changePolicyBody)).ConfigureAwait(false);

				if (response.Success)
				{
					LogIndexMovedToNextPhase(status.IndexName, status.Phase, nextPhase);
				}
				else
				{
					LogIndexMoveToNextPhaseFailed(status.IndexName, status.Phase,
						response.Body ?? "Unknown error");
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
	/// Builds an ISM policy JSON body from the domain model.
	/// </summary>
	/// <param name="policy"> The lifecycle policy configuration. </param>
	/// <returns> The ISM policy JSON string. </returns>
	private static string BuildIsmPolicyBody(IndexLifecyclePolicy policy)
	{
		// OpenSearch ISM uses a state machine model with states, transitions, and actions.
		// We map the ILM-style phases (hot/warm/cold/delete) to ISM states.
		var states = new List<object>();
		var stateNames = new List<string>();

		if (policy.Hot != null)
		{
			stateNames.Add("hot");
			states.Add(BuildHotState(policy.Hot, policy.Warm != null ? "warm" : policy.Cold != null ? "cold" : policy.Delete != null ? "delete" : null));
		}

		if (policy.Warm != null)
		{
			stateNames.Add("warm");
			states.Add(BuildWarmState(policy.Warm, policy.Cold != null ? "cold" : policy.Delete != null ? "delete" : null));
		}

		if (policy.Cold != null)
		{
			stateNames.Add("cold");
			states.Add(BuildColdState(policy.Cold, policy.Delete != null ? "delete" : null));
		}

		if (policy.Delete != null)
		{
			stateNames.Add("delete");
			states.Add(BuildDeleteState(policy.Delete));
		}

		var ismPolicy = new
		{
			policy = new
			{
				default_state = stateNames.Count > 0 ? stateNames[0] : "hot",
				states
			}
		};

		return JsonSerializer.Serialize(ismPolicy);
	}

	private static object BuildHotState(HotPhaseConfiguration config, string? nextState)
	{
		var actions = new List<object>();

		if (config.Rollover != null)
		{
			var rolloverAction = new Dictionary<string, object> { ["rollover"] = new Dictionary<string, object?>() };
			var rolloverConfig = (Dictionary<string, object?>)((Dictionary<string, object>)rolloverAction.First().Value).Values.First()!;

			if (config.Rollover.MaxAge.HasValue)
			{
				rolloverConfig["min_index_age"] = $"{(long)config.Rollover.MaxAge.Value.TotalDays}d";
			}

			if (config.Rollover.MaxDocs.HasValue)
			{
				rolloverConfig["min_doc_count"] = config.Rollover.MaxDocs.Value;
			}

			if (!string.IsNullOrEmpty(config.Rollover.MaxSize))
			{
				rolloverConfig["min_size"] = config.Rollover.MaxSize;
			}

			actions.Add(rolloverAction);
		}

		var transitions = new List<object>();
		if (nextState != null && config.MinAge.HasValue)
		{
			transitions.Add(new { state_name = nextState, conditions = new { min_index_age = $"{(long)config.MinAge.Value.TotalDays}d" } });
		}
		else if (nextState != null)
		{
			transitions.Add(new { state_name = nextState });
		}

		return new { name = "hot", actions, transitions };
	}

	private static object BuildWarmState(WarmPhaseConfiguration config, string? nextState)
	{
		var actions = new List<object>();

		if (config.NumberOfReplicas.HasValue)
		{
			actions.Add(new { replica_count = new { number_of_replicas = config.NumberOfReplicas.Value } });
		}

		var transitions = new List<object>();
		if (nextState != null && config.MinAge.HasValue)
		{
			transitions.Add(new { state_name = nextState, conditions = new { min_index_age = $"{(long)config.MinAge.Value.TotalDays}d" } });
		}
		else if (nextState != null)
		{
			transitions.Add(new { state_name = nextState });
		}

		return new { name = "warm", actions, transitions };
	}

	private static object BuildColdState(ColdPhaseConfiguration config, string? nextState)
	{
		var actions = new List<object>();

		if (config.NumberOfReplicas.HasValue)
		{
			actions.Add(new { replica_count = new { number_of_replicas = config.NumberOfReplicas.Value } });
		}

		var transitions = new List<object>();
		if (nextState != null && config.MinAge.HasValue)
		{
			transitions.Add(new { state_name = nextState, conditions = new { min_index_age = $"{(long)config.MinAge.Value.TotalDays}d" } });
		}
		else if (nextState != null)
		{
			transitions.Add(new { state_name = nextState });
		}

		return new { name = "cold", actions, transitions };
	}

	private static object BuildDeleteState(DeletePhaseConfiguration _)
	{
		var actions = new List<object> { new { delete = new { } } };

		return new { name = "delete", actions, transitions = Array.Empty<object>() };
	}

	/// <summary>
	/// Gets the next phase in the ISM lifecycle.
	/// </summary>
	/// <param name="currentPhase"> The current phase name. </param>
	/// <returns> The next phase name, or null if already in the final phase. </returns>
	private static string? GetNextPhase(string currentPhase)
	{
		// ISM phase names are protocol-defined lowercase values; ToLowerInvariant is correct here
#pragma warning disable CA1308
		return currentPhase.ToLowerInvariant() switch
		{
			"hot" => "warm",
			"warm" => "cold",
			"cold" => "delete",
			_ => null
		};
	}
}
