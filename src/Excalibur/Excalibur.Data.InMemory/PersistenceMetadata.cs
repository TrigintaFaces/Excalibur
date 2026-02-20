// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.InMemory;

/// <summary>
/// Metadata for persistence operations.
/// </summary>
internal sealed class PersistenceMetadata
{
	/// <summary>
	/// Gets or sets the persistence format version.
	/// </summary>
	/// <value>
	/// The persistence format version.
	/// </value>
	public string? Version { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of persistence operation.
	/// </summary>
	/// <value>
	/// The timestamp of persistence operation.
	/// </value>
	public string? Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the provider type identifier.
	/// </summary>
	/// <value>
	/// The provider type identifier.
	/// </value>
	public string? Provider { get; set; }
}
