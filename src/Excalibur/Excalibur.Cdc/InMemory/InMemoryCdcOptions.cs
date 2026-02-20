// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Configuration options for the in-memory CDC provider.
/// </summary>
/// <remarks>
/// <para>
/// The in-memory CDC provider is designed for testing scenarios where
/// a real database connection is not available or not desired.
/// </para>
/// </remarks>
public sealed class InMemoryCdcOptions
{
	/// <summary>
	/// Gets or sets the processor identifier for this CDC processor instance.
	/// </summary>
	/// <value>The processor identifier. Default is "inmemory-cdc".</value>
	[Required]
	public string ProcessorId { get; set; } = "inmemory-cdc";

	/// <summary>
	/// Gets or sets the batch size for CDC change processing.
	/// </summary>
	/// <value>The batch size. Default is 100.</value>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets whether to automatically flush changes after each batch.
	/// </summary>
	/// <value><see langword="true"/> to auto-flush; otherwise, <see langword="false"/>. Default is true.</value>
	public bool AutoFlush { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to preserve changes in history after processing.
	/// </summary>
	/// <value><see langword="true"/> to preserve history; otherwise, <see langword="false"/>. Default is false.</value>
	public bool PreserveHistory { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when validation fails.
	/// </exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ProcessorId))
		{
			throw new InvalidOperationException("ProcessorId is required.");
		}

		if (BatchSize <= 0)
		{
			throw new InvalidOperationException("BatchSize must be positive.");
		}
	}
}
