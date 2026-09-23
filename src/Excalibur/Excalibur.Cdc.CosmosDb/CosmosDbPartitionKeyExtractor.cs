// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Resolves the partition-key value of a change-feed document from a configured Cosmos DB partition-key
/// path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists as one unit.</b> Both change-feed processors in this package need the same
/// resolution, and they previously carried a copy of it each. The two copies are the reason a defect had
/// to be found twice; there is now one implementation and both call it.
/// </para>
/// <para>
/// <b>What a partition-key path means.</b> Azure Cosmos DB defines a partition-key path as a
/// slash-delimited path into the document — <c>/tenant</c> addresses a top-level property,
/// <c>/tenant/id</c> addresses <c>id</c> nested inside <c>tenant</c> — and a partition-key value may be a
/// string, a number or a boolean. This resolver implements that definition: it walks every segment, and
/// it accepts all three scalar types.
/// </para>
/// </remarks>
internal static class CosmosDbPartitionKeyExtractor
{
	/// <summary>
	/// Extracts the partition-key value addressed by <paramref name="partitionKeyPath"/> from
	/// <paramref name="root"/>.
	/// </summary>
	/// <param name="root">The root element of the change-feed document.</param>
	/// <param name="partitionKeyPath">
	/// The configured Cosmos DB partition-key path (for example <c>/tenant</c> or <c>/tenant/id</c>). A
	/// <see langword="null"/>, empty or segment-less path yields <see cref="CosmosDbPartitionKeyKind.None"/>.
	/// </param>
	/// <param name="kind">
	/// Receives the JSON type of the resolved value, so a numeric key and a string key holding the same
	/// text remain distinguishable.
	/// </param>
	/// <returns>
	/// The partition-key value in textual form — the string itself for a string, the invariant JSON text
	/// for a number, <c>true</c>/<c>false</c> for a boolean — or <see langword="null"/> when the path does
	/// not resolve to a scalar value.
	/// </returns>
	public static string? Extract(JsonElement root, string? partitionKeyPath, out CosmosDbPartitionKeyKind kind)
	{
		kind = CosmosDbPartitionKeyKind.None;

		if (string.IsNullOrEmpty(partitionKeyPath))
		{
			return null;
		}

		var current = root;
		var walkedAnySegment = false;

		foreach (var segment in partitionKeyPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
		{
			// A segment can only be resolved against an object. Anything else — a scalar reached before the
			// path was exhausted, or an array — means the document does not carry this path.
			if (current.ValueKind != JsonValueKind.Object ||
				!current.TryGetProperty(segment, out var next))
			{
				return null;
			}

			current = next;
			walkedAnySegment = true;
		}

		if (!walkedAnySegment)
		{
			// A path of "/" (or only separators) addresses nothing. Returning the document itself would be
			// worse than returning nothing.
			return null;
		}

		switch (current.ValueKind)
		{
			case JsonValueKind.String:
				kind = CosmosDbPartitionKeyKind.String;
				return current.GetString();

			case JsonValueKind.Number:
				// GetRawText is the value exactly as the document carried it, which is culture-invariant and
				// round-trips a partition key the SDK would accept back.
				kind = CosmosDbPartitionKeyKind.Number;
				return current.GetRawText();

			case JsonValueKind.True:
			case JsonValueKind.False:
				kind = CosmosDbPartitionKeyKind.Boolean;
				return current.GetRawText();

			case JsonValueKind.Null:
				// Present, and explicitly null — which Cosmos treats as its own partition, distinct from a
				// document that carries no value at the path at all.
				kind = CosmosDbPartitionKeyKind.Null;
				return null;

			default:
				// Object, Array or Undefined: not a partition-key value Cosmos can express.
				return null;
		}
	}
}
