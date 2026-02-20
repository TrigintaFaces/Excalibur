// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents information about a scheduled message.
/// </summary>
public sealed class ScheduleInfo
{
	/// <summary>
	/// Gets or sets the schedule identifier.
	/// </summary>
	/// <value>The current <see cref="ScheduleId"/> value.</value>
	public string ScheduleId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the scheduled message.
	/// </summary>
	/// <value>The current <see cref="Message"/> value.</value>
	public IDispatchMessage? Message { get; set; }

	/// <summary>
	/// Gets or sets the scheduled delivery time.
	/// </summary>
	/// <value>The current <see cref="ScheduledTime"/> value.</value>
	public DateTimeOffset ScheduledTime { get; set; }

	/// <summary>
	/// Gets or sets the creation time.
	/// </summary>
	/// <value>The current <see cref="CreatedTime"/> value.</value>
	public DateTimeOffset CreatedTime { get; set; }

	/// <summary>
	/// Gets or sets the schedule status.
	/// </summary>
	/// <value>The current <see cref="Status"/> value.</value>
	public ScheduleStatus Status { get; set; }

	/// <summary>
	/// Gets or sets the last error message, if any.
	/// </summary>
	/// <value>The current <see cref="LastError"/> value.</value>
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets the number of delivery attempts.
	/// </summary>
	/// <value>The current <see cref="DeliveryAttempts"/> value.</value>
	public int DeliveryAttempts { get; set; }
}
