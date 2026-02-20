// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexLifecycleManagement;
using Elastic.Transport;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing the lifecycle of Elasticsearch indices including rollover, aging, and deletion.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="IndexLifecycleManager" /> class.
/// </remarks>
/// <param name="client"> The Elasticsearch client instance. </param>
/// <param name="logger"> The logger instance. </param>
/// <exception cref="ArgumentNullException"> Thrown if any parameter is null. </exception>
public partial class IndexLifecycleManager(ElasticsearchClient client, ILogger<IndexLifecycleManager> logger) : IIndexLifecycleManager
{
	private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly ILogger<IndexLifecycleManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task<bool> CreateLifecyclePolicyAsync(string policyName, IndexLifecyclePolicy policy,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
		ArgumentNullException.ThrowIfNull(policy);

		try
		{
			LogCreatingLifecyclePolicy(policyName);

			var response = await _client.IndexLifecycleManagement.PutLifecycleAsync(
				policyName,
				r => r.Policy(p => ConfigureIlmPolicy(p, policy)),
				cancellationToken).ConfigureAwait(false);

			if (response.IsValidResponse)
			{
				LogLifecyclePolicyCreated(policyName);
				return true;
			}

			LogLifecyclePolicyCreationFailed(policyName, response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error");
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

			var response = await _client.IndexLifecycleManagement.DeleteLifecycleAsync(
				policyName,
				cancellationToken).ConfigureAwait(false);

			if (response.IsValidResponse)
			{
				LogLifecyclePolicyDeleted(policyName);
				return true;
			}

			// Policy not found is not necessarily an error
			if (response.ElasticsearchServerError?.Status == 404)
			{
				LogLifecyclePolicyNotFound(policyName);
				return false;
			}

			LogLifecyclePolicyDeletionFailed(policyName, response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error");
			return false;
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

			var response = await _client.Indices.RolloverAsync(
				aliasName,
				r =>
				{
					_ = r.Conditions(c =>
					{
						if (rolloverConditions.MaxAge.HasValue)
						{
							_ = c.MaxAge(rolloverConditions.MaxAge.Value);
						}

						if (rolloverConditions.MaxDocs.HasValue)
						{
							_ = c.MaxDocs(rolloverConditions.MaxDocs.Value);
						}

						if (!string.IsNullOrEmpty(rolloverConditions.MaxSize))
						{
							_ = c.MaxSize(new ByteSize(rolloverConditions.MaxSize));
						}

						if (!string.IsNullOrEmpty(rolloverConditions.MaxPrimaryShardSize))
						{
							_ = c.MaxPrimaryShardSize(new ByteSize(rolloverConditions.MaxPrimaryShardSize));
						}
					});
				},
				cancellationToken).ConfigureAwait(false);

			if (response.IsValidResponse)
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

			LogIndexRolloverFailed(aliasName, response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error");
			return new IndexRolloverResult
			{
				IsSuccessful = false,
				RolledOver = false,
				Errors = [response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"]
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

			// Use raw transport request since ExplainLifecycle isn't exposed in this client version
			var endpoint = $"/{Uri.EscapeDataString(pattern)}/_ilm/explain";
			var response = await _client.Transport.RequestAsync<StringResponse>(
				Elastic.Transport.HttpMethod.GET,
				endpoint,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (!response.ApiCallDetails.HasSuccessfulStatusCode)
			{
				LogLifecycleStatusFailed(pattern, response.Body ?? "Unknown error");
				return [];
			}

			var statuses = new List<IndexLifecycleStatus>();

			if (!string.IsNullOrEmpty(response.Body))
			{
				var jsonDoc = JsonDocument.Parse(response.Body);

				if (jsonDoc.RootElement.TryGetProperty("indices", out var indicesElement))
				{
					foreach (var indexProperty in indicesElement.EnumerateObject())
					{
						var indexName = indexProperty.Name;
						var indexData = indexProperty.Value;

						var phase = indexData.TryGetProperty("phase", out var phaseElement)
							? phaseElement.GetString() ?? "unknown"
							: "unknown";

						var policyName = indexData.TryGetProperty("policy", out var policyElement)
							? policyElement.GetString()
							: null;

						TimeSpan? age = null;
						if (indexData.TryGetProperty("age", out var ageElement))
						{
							age = ParseElasticsearchDuration(ageElement.GetString());
						}

						statuses.Add(new IndexLifecycleStatus { IndexName = indexName, Phase = phase, PolicyName = policyName, Age = age });
					}
				}
			}

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

			// First, get the current lifecycle status for matching indices
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

				// Use ILM move to lifecycle step API to force the transition
				var response = await _client.IndexLifecycleManagement.MoveToStepAsync(
					status.IndexName,
					r => r.CurrentStep(cs => cs
							.Phase(status.Phase)
							.Action("complete")
							.Name("complete"))
						.NextStep(ns => ns
							.Phase(nextPhase)
							.Action("complete")
							.Name("complete")),
					cancellationToken).ConfigureAwait(false);

				if (response.IsValidResponse)
				{
					LogIndexMovedToNextPhase(status.IndexName, status.Phase, nextPhase);
				}
				else
				{
					LogIndexMoveToNextPhaseFailed(status.IndexName, status.Phase,
						response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error");
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
	/// Parses an Elasticsearch duration string (e.g., "5d", "2h", "30m") to a TimeSpan.
	/// </summary>
	/// <param name="duration"> The duration string to parse. </param>
	/// <returns> The parsed TimeSpan, or null if parsing fails. </returns>
	private static TimeSpan? ParseElasticsearchDuration(string? duration)
	{
		if (string.IsNullOrEmpty(duration))
		{
			return null;
		}

		// Handle format like "5.2d" or "30m" or "2h"
		var span = duration.AsSpan();
		if (span.Length < 2)
		{
			return null;
		}

		var unit = span[^1];
		var valueSpan = span[..^1];

		if (!double.TryParse(valueSpan, out var value))
		{
			return null;
		}

		return unit switch
		{
			'd' => TimeSpan.FromDays(value),
			'h' => TimeSpan.FromHours(value),
			'm' => TimeSpan.FromMinutes(value),
			's' => TimeSpan.FromSeconds(value),
			_ => null
		};
	}

	/// <summary>
	/// Gets the next phase in the ILM lifecycle.
	/// </summary>
	/// <param name="currentPhase"> The current phase name. </param>
	/// <returns> The next phase name, or null if already in the final phase. </returns>
	private static string? GetNextPhase(string currentPhase)
	{
		return currentPhase.ToLowerInvariant() switch
		{
			"hot" => "warm",
			"warm" => "cold",
			"cold" => "delete",
			_ => null
		};
	}

	/// <summary>
	/// Converts a TimeSpan to an Elasticsearch Duration string.
	/// </summary>
	/// <param name="timeSpan"> The time span to convert. </param>
	/// <returns> An Elasticsearch-compatible duration string. </returns>
	private static Duration ToDuration(TimeSpan timeSpan)
	{
		// Elasticsearch duration format: nanos (ns), micros (us), millis (ms), seconds (s), minutes (m), hours (h), days (d)
		if (timeSpan.TotalDays >= 1)
		{
			return new Duration($"{(long)timeSpan.TotalDays}d");
		}

		if (timeSpan.TotalHours >= 1)
		{
			return new Duration($"{(long)timeSpan.TotalHours}h");
		}

		if (timeSpan.TotalMinutes >= 1)
		{
			return new Duration($"{(long)timeSpan.TotalMinutes}m");
		}

		if (timeSpan.TotalSeconds >= 1)
		{
			return new Duration($"{(long)timeSpan.TotalSeconds}s");
		}

		return new Duration($"{(long)timeSpan.TotalMilliseconds}ms");
	}

	/// <summary>
	/// Configures an ILM policy descriptor from the domain model.
	/// </summary>
	/// <param name="descriptor"> The ILM policy descriptor to configure. </param>
	/// <param name="policy"> The lifecycle policy configuration. </param>
	/// <returns> The configured descriptor. </returns>
	private static IlmPolicyDescriptor ConfigureIlmPolicy(IlmPolicyDescriptor descriptor, IndexLifecyclePolicy policy)
	{
		return descriptor.Phases(phases =>
		{
			if (policy.Hot != null)
			{
				_ = phases.Hot(hot => ConfigureHotPhase(hot, policy.Hot));
			}

			if (policy.Warm != null)
			{
				_ = phases.Warm(warm => ConfigureWarmPhase(warm, policy.Warm));
			}

			if (policy.Cold != null)
			{
				_ = phases.Cold(cold => ConfigureColdPhase(cold, policy.Cold));
			}

			if (policy.Delete != null)
			{
				_ = phases.Delete(delete => ConfigureDeletePhase(delete, policy.Delete));
			}
		});
	}

	private static PhaseDescriptor ConfigureHotPhase(PhaseDescriptor descriptor, HotPhaseConfiguration config)
	{
		if (config.MinAge.HasValue)
		{
			_ = descriptor.MinAge(ToDuration(config.MinAge.Value));
		}

		_ = descriptor.Actions(actions =>
		{
			if (config.Rollover != null)
			{
				_ = actions.Rollover(rollover =>
				{
					if (config.Rollover.MaxAge.HasValue)
					{
						_ = rollover.MaxAge(ToDuration(config.Rollover.MaxAge.Value));
					}

					if (config.Rollover.MaxDocs.HasValue)
					{
						_ = rollover.MaxDocs(config.Rollover.MaxDocs.Value);
					}

					if (!string.IsNullOrEmpty(config.Rollover.MaxSize))
					{
						_ = rollover.MaxSize(new ByteSize(config.Rollover.MaxSize));
					}

					if (!string.IsNullOrEmpty(config.Rollover.MaxPrimaryShardSize))
					{
						_ = rollover.MaxPrimaryShardSize(new ByteSize(config.Rollover.MaxPrimaryShardSize));
					}
				});
			}

			if (config.Priority.HasValue)
			{
				_ = actions.SetPriority(sp => sp.Priority(config.Priority.Value));
			}
		});

		return descriptor;
	}

	private static PhaseDescriptor ConfigureWarmPhase(PhaseDescriptor descriptor, WarmPhaseConfiguration config)
	{
		if (config.MinAge.HasValue)
		{
			_ = descriptor.MinAge(ToDuration(config.MinAge.Value));
		}

		_ = descriptor.Actions(actions =>
		{
			if (config.ShrinkNumberOfShards.HasValue)
			{
				_ = actions.Shrink(shrink => shrink.NumberOfShards(config.ShrinkNumberOfShards.Value));
			}

			if (config.Priority.HasValue)
			{
				_ = actions.SetPriority(sp => sp.Priority(config.Priority.Value));
			}

			if (config.NumberOfReplicas.HasValue)
			{
				_ = actions.Allocate(allocate => allocate.NumberOfReplicas(config.NumberOfReplicas.Value));
			}
		});

		return descriptor;
	}

	private static PhaseDescriptor ConfigureColdPhase(PhaseDescriptor descriptor, ColdPhaseConfiguration config)
	{
		if (config.MinAge.HasValue)
		{
			_ = descriptor.MinAge(ToDuration(config.MinAge.Value));
		}

		_ = descriptor.Actions(actions =>
		{
			if (config.Priority.HasValue)
			{
				_ = actions.SetPriority(sp => sp.Priority(config.Priority.Value));
			}

			if (config.NumberOfReplicas.HasValue)
			{
				_ = actions.Allocate(allocate => allocate.NumberOfReplicas(config.NumberOfReplicas.Value));
			}
		});

		return descriptor;
	}

	private static PhaseDescriptor ConfigureDeletePhase(PhaseDescriptor descriptor, DeletePhaseConfiguration config)
	{
		if (config.MinAge.HasValue)
		{
			_ = descriptor.MinAge(ToDuration(config.MinAge.Value));
		}

		_ = descriptor.Actions(actions =>
		{
			_ = actions.Delete(_ => { });

			if (!string.IsNullOrEmpty(config.WaitForSnapshotPolicy))
			{
				_ = actions.WaitForSnapshot(w => w.Policy(config.WaitForSnapshotPolicy));
			}
		});

		return descriptor;
	}
}
