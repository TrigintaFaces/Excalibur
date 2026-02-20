// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga.Reminders;

/// <summary>
/// Configuration options for saga reminder scheduling.
/// </summary>
public sealed class SagaReminderOptions
{
	/// <summary>
	/// Gets or sets the default delay for reminders when no explicit delay is specified.
	/// Default: 5 minutes.
	/// </summary>
	/// <value>The default reminder delay.</value>
	public TimeSpan DefaultDelay { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum number of active reminders allowed per saga instance.
	/// Default: 10.
	/// </summary>
	/// <value>The maximum reminders per saga. Must be at least 1.</value>
	[Range(1, 1000)]
	public int MaxRemindersPerSaga { get; set; } = 10;

	/// <summary>
	/// Gets or sets the minimum allowed delay for a reminder.
	/// Default: 1 second.
	/// </summary>
	/// <value>The minimum reminder delay to prevent excessive scheduling.</value>
	public TimeSpan MinimumDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum allowed delay for a reminder.
	/// Default: 30 days.
	/// </summary>
	/// <value>The maximum reminder delay.</value>
	public TimeSpan MaximumDelay { get; set; } = TimeSpan.FromDays(30);
}
