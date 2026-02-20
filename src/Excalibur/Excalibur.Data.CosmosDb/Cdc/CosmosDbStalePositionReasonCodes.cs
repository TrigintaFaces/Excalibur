// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Provides standardized reason codes for CosmosDB CDC stale position scenarios.
/// </summary>
/// <remarks>
/// <para>
/// These codes categorize why a Change Feed continuation token became invalid in CosmosDB.
/// Unlike MongoDB oplog or Postgres WAL, CosmosDB stale positions typically occur due to:
/// <list type="bullet">
/// <item><description>Continuation token expiry (7-day retention)</description></item>
/// <item><description>Container deletion/recreation</description></item>
/// <item><description>Partition splits or merges</description></item>
/// <item><description>Throughput changes causing repartitioning</description></item>
/// </list>
/// </para>
/// <para>
/// These codes are used in <c>CdcPositionResetEventArgs.ReasonCode</c> to enable consistent
/// logging, alerting, and handling across different stale position scenarios.
/// </para>
/// </remarks>
public static class CosmosDbStalePositionReasonCodes
{
	/// <summary>
	/// The continuation token has expired beyond the 7-day retention window.
	/// </summary>
	/// <remarks>
	/// <para>
	/// CosmosDB Change Feed maintains data for up to 7 days. If a consumer falls behind
	/// by more than 7 days, the continuation token becomes invalid.
	/// HTTP status code 410 (Gone).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Consumer offline for more than 7 days</description></item>
	/// <item><description>Processing lag exceeding retention window</description></item>
	/// <item><description>Container TTL settings affecting change retention</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ContinuationTokenExpired = "COSMOSDB_CONTINUATION_TOKEN_EXPIRED";

	/// <summary>
	/// The partition was not found, typically after a split or merge.
	/// </summary>
	/// <remarks>
	/// <para>
	/// CosmosDB may split or merge partitions based on storage and throughput.
	/// When a partition is split, the original partition ID becomes invalid.
	/// HTTP status code 404 (NotFound).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Partition split due to storage growth</description></item>
	/// <item><description>Partition merge during scale-down</description></item>
	/// <item><description>Physical partition reorganization</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string PartitionNotFound = "COSMOSDB_PARTITION_NOT_FOUND";

	/// <summary>
	/// The container was deleted and potentially recreated.
	/// </summary>
	/// <remarks>
	/// <para>
	/// If a container is deleted, all continuation tokens become invalid.
	/// Recreating a container with the same name does not restore tokens.
	/// HTTP status code 404 (NotFound).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Container deleted during maintenance</description></item>
	/// <item><description>Container recreated with different configuration</description></item>
	/// <item><description>Database recreation</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ContainerDeleted = "COSMOSDB_CONTAINER_DELETED";

	/// <summary>
	/// The ETag does not match, indicating a concurrent modification conflict.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the continuation token's ETag no longer matches the partition state.
	/// HTTP status code 412 (PreconditionFailed).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Concurrent Change Feed readers with outdated tokens</description></item>
	/// <item><description>Partition state changed during processing</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ETagMismatch = "COSMOSDB_ETAG_MISMATCH";

	/// <summary>
	/// A partition split occurred affecting the continuation token.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When a partition splits, the Change Feed processor must handle the new partitions.
	/// The original partition's continuation token may become partially or fully invalid.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Storage exceeding partition limit (50GB)</description></item>
	/// <item><description>Hot partition auto-split</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string PartitionSplit = "COSMOSDB_PARTITION_SPLIT";

	/// <summary>
	/// Throughput change caused partition repartitioning.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Changing throughput (RU/s) can cause CosmosDB to repartition data,
	/// potentially invalidating existing continuation tokens.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Manual throughput adjustment</description></item>
	/// <item><description>Autoscale throughput changes</description></item>
	/// <item><description>Provisioned to serverless migration</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ThroughputChange = "COSMOSDB_THROUGHPUT_CHANGE";

	/// <summary>
	/// The reason for the stale position could not be determined.
	/// </summary>
	/// <remarks>
	/// This is used when the specific cause cannot be identified from the CosmosDB error
	/// or when an unexpected error occurs during position validation.
	/// </remarks>
	public const string Unknown = "COSMOSDB_UNKNOWN";

	/// <summary>
	/// Determines the reason code from an HTTP status code.
	/// </summary>
	/// <param name="statusCode">The HTTP status code.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// <para>
	/// CosmosDB HTTP status codes relevant to stale positions:
	/// <list type="bullet">
	/// <item><description>410 - Gone (continuation token expired)</description></item>
	/// <item><description>404 - NotFound (partition/container not found)</description></item>
	/// <item><description>412 - PreconditionFailed (ETag mismatch)</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static string FromStatusCode(int statusCode) =>
		statusCode switch
		{
			410 => ContinuationTokenExpired,
			404 => PartitionNotFound,
			412 => ETagMismatch,
			_ => Unknown
		};

	/// <summary>
	/// Determines the reason code from a CosmosDB error message pattern.
	/// </summary>
	/// <param name="errorMessage">The CosmosDB error message.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// This method provides fallback detection when status codes are not available
	/// by analyzing common error message patterns.
	/// </remarks>
	public static string FromErrorMessage(string? errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage))
		{
			return Unknown;
		}

		if (errorMessage.Contains("CONTINUATION", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("GONE", StringComparison.OrdinalIgnoreCase)))
		{
			return ContinuationTokenExpired;
		}

		if (errorMessage.Contains("PARTITION", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("SPLIT", StringComparison.OrdinalIgnoreCase)))
		{
			return errorMessage.Contains("SPLIT", StringComparison.OrdinalIgnoreCase)
				? PartitionSplit
				: PartitionNotFound;
		}

		if (errorMessage.Contains("CONTAINER", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DELETED", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase)))
		{
			return ContainerDeleted;
		}

		if (errorMessage.Contains("ETAG", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("PRECONDITION", StringComparison.OrdinalIgnoreCase))
		{
			return ETagMismatch;
		}

		if (errorMessage.Contains("THROUGHPUT", StringComparison.OrdinalIgnoreCase) ||
			(errorMessage.Contains("RU", StringComparison.OrdinalIgnoreCase) &&
			 errorMessage.Contains("CHANGE", StringComparison.OrdinalIgnoreCase)))
		{
			return ThroughputChange;
		}

		return Unknown;
	}
}
