// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Firestore.Projections;

/// <summary>
/// Configuration options for the Firestore projection store.
/// </summary>
public sealed class FirestoreProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the Firestore collection name for projections.
	/// </summary>
	[Required]
	public string CollectionName { get; set; } = "projections";

	/// <summary>
	/// Gets or sets the GCP project ID. Required unless using emulator.
	/// </summary>
	public string? ProjectId { get; set; }
}
