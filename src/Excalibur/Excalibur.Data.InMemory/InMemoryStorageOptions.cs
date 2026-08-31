// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.InMemory;

/// <summary>
/// Storage and resilience options for the in-memory provider.
/// </summary>
public sealed class InMemoryStorageOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether metrics are enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if metrics are enabled; otherwise, <c>false</c>.
	/// </value>
	public bool EnableMetrics { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether disk persistence is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if disk persistence is enabled; otherwise, <c>false</c>.
	/// </value>
	public bool PersistToDisk { get; set; }

	/// <summary>
	/// Gets or sets the file path for persistence.
	/// </summary>
	/// <value>
	/// The file path for persistence.
	/// </value>
	public string? PersistenceFilePath { get; set; }

	/// <summary>
	/// Validates the storage options.
	/// </summary>
	/// <exception cref="ArgumentException">Thrown when PersistToDisk is enabled but PersistenceFilePath is not specified.</exception>
	public void Validate()
	{
		if (PersistToDisk && string.IsNullOrEmpty(PersistenceFilePath))
		{
			throw new ArgumentException("PersistenceFilePath must be specified when PersistToDisk is enabled");
		}
	}
}
