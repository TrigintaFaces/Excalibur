// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexLifecycleManagement;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="IIndexLifecycleOperations"/> implementation that
/// forwards to <c>_inner.IndexLifecycleManagement.*</c>,
/// <c>_inner.Indices.RolloverAsync</c>, and the
/// <c>_inner.Transport.RequestAsync</c> raw-HTTP <c>_ilm/explain</c> endpoint
/// on a real <see cref="ElasticsearchClient"/>. Owns the ILM phase-descriptor
/// fluent-builder code + JSON parsing of the explain response so the seam
/// consumer sees only domain-shaped types.
/// </summary>
internal sealed class IndexLifecycleOperationsAdapter : IIndexLifecycleOperations
{
	private readonly ElasticsearchClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="IndexLifecycleOperationsAdapter"/> class.
	/// </summary>
	public IndexLifecycleOperationsAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<LifecycleOperationResult> PutPolicyAsync(
		string policyName,
		IndexLifecyclePolicy policy,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
		ArgumentNullException.ThrowIfNull(policy);

		var response = await _inner.IndexLifecycleManagement
			.PutLifecycleAsync(
				policyName,
				r => r.Policy(p => IlmPolicyDescriptorBuilder.Configure(p, policy)),
				cancellationToken)
			.ConfigureAwait(false);

		return new LifecycleOperationResult(
			response.IsValidResponse,
			response.IsValidResponse ? null : response.ElasticsearchServerError?.Error?.Reason);
	}

	/// <inheritdoc/>
	public async Task<LifecyclePolicyDeleteOutcome> DeletePolicyAsync(
		string policyName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

		var response = await _inner.IndexLifecycleManagement
			.DeleteLifecycleAsync(policyName, cancellationToken)
			.ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return LifecyclePolicyDeleteOutcome.Deleted;
		}

		return response.ElasticsearchServerError?.Status == 404
			? LifecyclePolicyDeleteOutcome.NotFound
			: LifecyclePolicyDeleteOutcome.Failed;
	}

	/// <inheritdoc/>
	public async Task<LifecycleRolloverResult> RolloverAsync(
		string aliasName,
		RolloverConditions conditions,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aliasName);
		ArgumentNullException.ThrowIfNull(conditions);

		var response = await _inner.Indices
			.RolloverAsync(
				aliasName,
				r =>
				{
					_ = r.Conditions(c =>
					{
						if (conditions.MaxAge.HasValue)
						{
							_ = c.MaxAge(conditions.MaxAge.Value);
						}

						if (conditions.MaxDocs.HasValue)
						{
							_ = c.MaxDocs(conditions.MaxDocs.Value);
						}

						if (!string.IsNullOrEmpty(conditions.MaxSize))
						{
							_ = c.MaxSize(new ByteSize(conditions.MaxSize));
						}

						if (!string.IsNullOrEmpty(conditions.MaxPrimaryShardSize))
						{
							_ = c.MaxPrimaryShardSize(new ByteSize(conditions.MaxPrimaryShardSize));
						}
					});
				},
				cancellationToken)
			.ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			return new LifecycleRolloverResult(
				Success: true,
				RolledOver: response.RolledOver,
				OldIndex: response.OldIndex,
				NewIndex: response.NewIndex,
				ErrorDetails: null);
		}

		return new LifecycleRolloverResult(
			Success: false,
			RolledOver: false,
			OldIndex: null,
			NewIndex: null,
			ErrorDetails: response.ElasticsearchServerError?.Error?.Reason);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<IndexLifecycleStatus>> GetStatusAsync(
		string indexPattern,
		CancellationToken cancellationToken)
	{
		var pattern = string.IsNullOrEmpty(indexPattern) ? "*" : indexPattern;
		var endpoint = $"/{Uri.EscapeDataString(pattern)}/_ilm/explain";
		var response = await _inner.Transport
			.RequestAsync<StringResponse>(
				Elastic.Transport.HttpMethod.GET,
				endpoint,
				cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		if (!response.ApiCallDetails.HasSuccessfulStatusCode || string.IsNullOrEmpty(response.Body))
		{
			return [];
		}

		return ParseLifecycleStatuses(response.Body);
	}

	/// <inheritdoc/>
	public async Task<LifecycleOperationResult> MoveToPhaseAsync(
		string indexName,
		string fromPhase,
		string toPhase,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(fromPhase);
		ArgumentException.ThrowIfNullOrWhiteSpace(toPhase);

		var response = await _inner.IndexLifecycleManagement
			.MoveToStepAsync(
				indexName,
				r => r.CurrentStep(cs => cs
						.Phase(fromPhase)
						.Action("complete")
						.Name("complete"))
					.NextStep(ns => ns
						.Phase(toPhase)
						.Action("complete")
						.Name("complete")),
				cancellationToken)
			.ConfigureAwait(false);

		return new LifecycleOperationResult(
			response.IsValidResponse,
			response.IsValidResponse ? null : response.ElasticsearchServerError?.Error?.Reason);
	}

	private static IReadOnlyList<IndexLifecycleStatus> ParseLifecycleStatuses(string body)
	{
		var statuses = new List<IndexLifecycleStatus>();
		var jsonDoc = JsonDocument.Parse(body);

		if (!jsonDoc.RootElement.TryGetProperty("indices", out var indicesElement))
		{
			return statuses;
		}

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

			statuses.Add(new IndexLifecycleStatus
			{
				IndexName = indexName,
				Phase = phase,
				PolicyName = policyName,
				Age = age,
			});
		}

		return statuses;
	}

	private static TimeSpan? ParseElasticsearchDuration(string? duration)
	{
		if (string.IsNullOrEmpty(duration))
		{
			return null;
		}

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
			_ => null,
		};
	}
}
