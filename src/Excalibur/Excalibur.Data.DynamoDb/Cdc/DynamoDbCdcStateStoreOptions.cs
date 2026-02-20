// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Configuration options for DynamoDB CDC state storage.
/// </summary>
public sealed class DynamoDbCdcStateStoreOptions
{
	/// <summary>
	/// Gets or sets the table name for CDC state storage.
	/// </summary>
	/// <value>The table name. Defaults to "cdc_state".</value>
	[Required]
	public string TableName { get; set; } = "cdc_state";

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(TableName))
		{
			throw new InvalidOperationException("TableName is required.");
		}
	}
}
