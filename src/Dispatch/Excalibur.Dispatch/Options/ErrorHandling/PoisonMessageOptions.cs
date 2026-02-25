// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.ErrorHandling;

/// <summary>
/// Configuration options for poison message handling.
/// </summary>
public sealed class PoisonMessageOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether poison message detection is enabled.
	/// </summary>
	/// <value>The current <see cref="Enabled"/> value.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts before a message is considered poison.
	/// </summary>
	/// <value>The current <see cref="MaxRetryAttempts"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the maximum processing time before a message is considered poison.
	/// </summary>
	/// <value>
	/// The maximum processing time before a message is considered poison.
	/// </value>
	public TimeSpan MaxProcessingTime { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the retention period for dead letter messages.
	/// </summary>
	/// <value>
	/// The retention period for dead letter messages.
	/// </value>
	public TimeSpan DeadLetterRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets or sets a value indicating whether to automatically clean up old dead letter messages.
	/// </summary>
	/// <value>The current <see cref="EnableAutoCleanup"/> value.</value>
	public bool EnableAutoCleanup { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval for automatic cleanup of old dead letter messages.
	/// </summary>
	/// <value>
	/// The interval for automatic cleanup of old dead letter messages.
	/// </value>
	public TimeSpan AutoCleanupInterval { get; set; } = TimeSpan.FromDays(1);

	/// <summary>
	/// Gets or sets a value indicating whether to capture full exception details in dead letter messages.
	/// </summary>
	/// <value>The current <see cref="CaptureExceptionDetails"/> value.</value>
	public bool CaptureExceptionDetails { get; set; } = true;

	/// <summary>
	/// Gets the exception types that should immediately poison a message.
	/// </summary>
	/// <value>The current <see cref="PoisonExceptionTypes"/> value.</value>
	public HashSet<Type> PoisonExceptionTypes { get; } =
	[
		typeof(InvalidOperationException),
		typeof(NotSupportedException),
		typeof(FormatException),
		typeof(ArgumentException),
		typeof(TypeLoadException),
	];

	/// <summary>
	/// Gets the exception types that should not poison a message.
	/// </summary>
	/// <value>The current <see cref="TransientExceptionTypes"/> value.</value>
	public HashSet<Type> TransientExceptionTypes { get; } =
	[
		typeof(TimeoutException),
		typeof(OperationCanceledException),
		typeof(TaskCanceledException),
	];

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection for poison messages.
	/// </summary>
	/// <value>The current <see cref="EnableMetrics"/> value.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable alerting for poison messages.
	/// </summary>
	/// <value>The current <see cref="EnableAlerting"/> value.</value>
	public bool EnableAlerting { get; set; } = true;

	/// <summary>
	/// Gets or sets the threshold for triggering alerts.
	/// </summary>
	/// <value>The current <see cref="AlertThreshold"/> value.</value>
	[Range(1, int.MaxValue)]
	public int AlertThreshold { get; set; } = 10;

	/// <summary>
	/// Gets or sets the time window for alert threshold calculation.
	/// </summary>
	/// <value>
	/// The time window for alert threshold calculation.
	/// </value>
	public TimeSpan AlertTimeWindow { get; set; } = TimeSpan.FromMinutes(15);
}
