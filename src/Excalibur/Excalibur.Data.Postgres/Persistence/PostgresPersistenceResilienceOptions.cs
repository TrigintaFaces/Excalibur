// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Resilience options for the Postgres persistence provider.
/// Controls retry behavior for transient failure recovery.
/// </summary>
public sealed class PostgresPersistenceResilienceOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures. Default is 3.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts for transient failures. Default is 3.
	/// </value>
	[Range(0, 10, ErrorMessage = "Max retry attempts must be between 0 and 10")]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts in milliseconds. Default is 1000ms.
	/// </summary>
	/// <value>
	/// The delay between retry attempts in milliseconds. Default is 1000ms.
	/// </value>
	[Range(100, 30000, ErrorMessage = "Retry delay must be between 100 and 30000 milliseconds")]
	public int RetryDelayMilliseconds { get; set; } = 1000;
}
