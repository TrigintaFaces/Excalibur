// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Data.OpenSearch.Monitoring;

/// <summary>
/// Extracts server-side response metrics (server <c>took</c> time, total hits, shard failures) from
/// OpenSearch.Client responses via reflection.
/// </summary>
/// <remarks>
/// OpenSearch.Client (NEST 7.x style) exposes these on the <em>invariant</em> generic
/// <c>ISearchResponse&lt;T&gt;</c>; at a diagnostics seam the document type <c>T</c> is unknown, so a
/// typed cast cannot be used (a <c>ISearchResponse&lt;object&gt;</c> cast fails for a real
/// <c>ISearchResponse&lt;Order&gt;</c> — generic invariance). Reflection is the sanctioned mechanism on
/// this opt-in observability path; <see cref="PropertyInfo"/> handles are cached once per concrete
/// response <see cref="Type"/> so this never taxes the hot path. The path is best-effort and fails open:
/// on any missing property or reflection error it records what is available and never throws.
/// </remarks>
internal static class OpenSearchResponseMetricsReflector
{
	private sealed record Accessors(PropertyInfo? Took, PropertyInfo? Total, PropertyInfo? Shards, PropertyInfo? ShardsFailed);

	private static readonly ConcurrentDictionary<Type, Accessors> AccessorCache = new();

	/// <summary>
	/// Attempts to read the server-side response metrics from an OpenSearch.Client response object.
	/// </summary>
	/// <param name="response"> The response object (typically an <c>ISearchResponse&lt;T&gt;</c> / bulk response). </param>
	/// <param name="tookMs"> The server-reported processing time in milliseconds, if present. </param>
	/// <param name="totalHits"> The total hit count, if present. </param>
	/// <param name="shardFailures"> The number of failed shards, if present. </param>
	/// <returns> <see langword="true"/> if at least one metric was read; otherwise <see langword="false"/>. </returns>
	[RequiresUnreferencedCode("Reads OpenSearch.Client response properties via reflection; the property types must be preserved under trimming.")]
	[RequiresDynamicCode("Uses reflection to inspect OpenSearch.Client response types that may require dynamic code.")]
	public static bool TryGetMetrics(object response, out long? tookMs, out long? totalHits, out int? shardFailures)
	{
		tookMs = null;
		totalHits = null;
		shardFailures = null;

		try
		{
			var accessors = AccessorCache.GetOrAdd(response.GetType(), static type =>
			{
				var shards = type.GetProperty("Shards");
				return new Accessors(
					type.GetProperty("Took"),
					type.GetProperty("Total"),
					shards,
					shards?.PropertyType.GetProperty("Failed"));
			});

			var any = false;

			if (accessors.Took?.GetValue(response) is long took)
			{
				tookMs = took;
				any = true;
			}

			if (accessors.Total?.GetValue(response) is long total)
			{
				totalHits = total;
				any = true;
			}

			if (accessors.Shards is not null && accessors.ShardsFailed is not null)
			{
				var shardsObj = accessors.Shards.GetValue(response);
				if (shardsObj is not null && accessors.ShardsFailed.GetValue(shardsObj) is int failed)
				{
					shardFailures = failed;
					any = true;
				}
			}

			return any;
		}
		catch (Exception) when (LogAndSwallow())
		{
			// Observability fails open: never throw from the diagnostics path.
			return false;
		}
	}

	// Filter that always swallows — keeps the catch best-effort while making the intent explicit.
	private static bool LogAndSwallow() => true;
}
