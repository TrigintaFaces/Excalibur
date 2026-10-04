// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Gcs.DependencyInjection;

/// <summary>
/// Configuration options for the Google Cloud Storage cold event store.
/// </summary>
public sealed class GcsColdEventStoreOptions
{
	/// <summary>Gets or sets the archive layout captured at provider construction.</summary>
	/// <value>Defaults to <see cref="ColdArchiveLayout.Legacy"/>.</value>
	/// <remarks>Configuration does not activate or migrate archives. Supplied clients must preserve raw bytes for TypedV2.</remarks>
	public ColdArchiveLayout Layout { get; set; } = ColdArchiveLayout.Legacy;

	/// <summary>
	/// Gets or sets the GCS bucket name for cold event storage.
	/// </summary>
	[Required]
	public string? BucketName { get; set; }

	/// <summary>
	/// Gets or sets the object prefix for archived event objects.
	/// </summary>
	/// <value>Default is "excalibur-cold-events".</value>
	public string ObjectPrefix { get; set; } = "excalibur-cold-events";
}
