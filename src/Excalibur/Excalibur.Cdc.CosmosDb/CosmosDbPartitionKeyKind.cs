// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Identifies the JSON type of the partition-key value extracted from a change-feed document.
/// </summary>
/// <remarks>
/// <para>
/// Azure Cosmos DB partition-key values may be a string, a number or a boolean, and the three are
/// distinct partitions: a document partitioned by the number <c>42</c> does not share a partition with
/// one partitioned by the string <c>"42"</c>. <see cref="CosmosDbDataChangeEvent.PartitionKey"/> carries
/// the value in its textual form, which cannot by itself tell those two apart; this kind restores the
/// distinction, so a consumer routing or re-issuing a request from a change event can rebuild the
/// original partition key unambiguously.
/// </para>
/// <para>
/// <see cref="None"/> and <see cref="Null"/> are also distinct, matching Cosmos itself: <see cref="None"/>
/// means no partition-key value was extracted (no path is configured, or the document does not carry the
/// configured path, or the value at the path is not a scalar), while <see cref="Null"/> means the path
/// resolved to an explicit JSON <c>null</c>.
/// </para>
/// </remarks>
public enum CosmosDbPartitionKeyKind
{
	/// <summary>
	/// No partition-key value is present on the event.
	/// </summary>
	None = 0,

	/// <summary>
	/// The configured path resolved to an explicit JSON <c>null</c>.
	/// </summary>
	Null = 1,

	/// <summary>
	/// The configured path resolved to a JSON string.
	/// </summary>
	String = 2,

	/// <summary>
	/// The configured path resolved to a JSON number.
	/// </summary>
	Number = 3,

	/// <summary>
	/// The configured path resolved to a JSON boolean.
	/// </summary>
	Boolean = 4,
}
