// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for dead letter queue behavior.
/// </summary>
public sealed class DeadLetterOptions
{
	/// <summary>
	/// Gets or sets the maximum number of processing attempts before dead lettering.
	/// </summary>
	/// <value> Default is 3. </value>
	[Range(1, int.MaxValue)]
	public int MaxAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the name of the dead letter queue.
	/// </summary>
	/// <value> Default is "deadletter". </value>
	[Required]
	public string QueueName { get; set; } = "deadletter";

	/// <summary>
	/// Gets or sets a value indicating whether to preserve original message metadata.
	/// </summary>
	/// <value> Default is true. </value>
	public bool PreserveMetadata { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include exception details.
	/// </summary>
	/// <value> Default is true. </value>
	public bool IncludeExceptionDetails { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether automatic recovery is enabled.
	/// </summary>
	/// <value> Default is false. </value>
	public bool EnableRecovery { get; set; }

	/// <summary>
	/// Gets or sets the recovery processing interval.
	/// </summary>
	/// <value> Default is 1 hour. </value>
	public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromHours(1);
}
