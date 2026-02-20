// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Specifies the Change Feed mode for CDC processing.
/// </summary>
public enum CosmosDbCdcMode
{
	/// <summary>
	/// Captures only the latest version of each document (inserts and updates only).
	/// </summary>
	/// <remarks>
	/// This is the default mode and does not require any special container configuration.
	/// </remarks>
	LatestVersion,

	/// <summary>
	/// Captures all versions of changes including deletes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This mode requires the container to have the change feed policy configured:
	/// <code>
	/// container.ChangeFeedPolicy = new ChangeFeedPolicy { FullFidelityRetention = TimeSpan.FromMinutes(10) }
	/// </code>
	/// </para>
	/// <para>
	/// Available in Azure Cosmos DB serverless and provisioned throughput accounts.
	/// </para>
	/// </remarks>
	AllVersionsAndDeletes,
}
