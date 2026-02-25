// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Configuration options for Firestore CDC state storage.
/// </summary>
public sealed class FirestoreCdcStateStoreOptions
{
	/// <summary>
	/// Gets or sets the collection name for storing CDC positions.
	/// </summary>
	/// <value>The collection name. Defaults to "_cdc_positions".</value>
	[Required]
	public string CollectionName { get; set; } = "_cdc_positions";

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(CollectionName))
		{
			throw new InvalidOperationException("CollectionName is required.");
		}
	}
}
