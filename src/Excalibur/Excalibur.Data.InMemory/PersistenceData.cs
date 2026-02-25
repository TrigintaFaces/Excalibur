// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.InMemory;

/// <summary>
/// Data structure for persistence to disk.
/// </summary>
internal sealed class PersistenceData
{
	/// <summary>
	/// Gets or sets the collections dictionary.
	/// </summary>
	/// <value>
	/// The collections dictionary.
	/// </value>
	public Dictionary<string, Dictionary<string, object>>? Collections { get; set; }

	/// <summary>
	/// Gets or sets the persistence metadata.
	/// </summary>
	/// <value>
	/// The persistence metadata.
	/// </value>
	public PersistenceMetadata? Metadata { get; set; }
}
