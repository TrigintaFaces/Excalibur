// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.DynamoDb.Projections;

/// <summary>
/// Configuration options for the DynamoDB projection store.
/// </summary>
public sealed class DynamoDbProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the DynamoDB table name for projections.
	/// </summary>
	[Required]
	public string TableName { get; set; } = "Projections";

	/// <summary>
	/// Gets or sets the partition key attribute name. Default: "PK".
	/// </summary>
	public string PartitionKeyName { get; set; } = "PK";

	/// <summary>
	/// Gets or sets whether to auto-create the table. Default: true.
	/// </summary>
	public bool AutoCreateTable { get; set; } = true;
}
