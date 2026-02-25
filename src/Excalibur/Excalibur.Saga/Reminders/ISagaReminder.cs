// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Reminders;

/// <summary>
/// Schedules and manages reminder notifications for saga instances.
/// </summary>
/// <remarks>
/// <para>
/// Reminders allow sagas to schedule delayed self-notifications, useful for
/// implementing timeout patterns, periodic checks, and deadline enforcement.
/// When a reminder fires, the saga receives a callback to take action.
/// </para>
/// <para>
/// This follows the actor model reminder pattern (similar to Orleans grains)
/// where a saga can request to be woken up after a specified delay.
/// </para>
/// </remarks>
public interface ISagaReminder
{
	/// <summary>
	/// Schedules a reminder for a saga instance.
	/// </summary>
	/// <param name="sagaId">The identifier of the saga to remind.</param>
	/// <param name="reminderName">A unique name for the reminder within the saga.</param>
	/// <param name="delay">The delay before the reminder fires.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The unique identifier assigned to the scheduled reminder.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sagaId"/> or <paramref name="reminderName"/> is null or empty.
	/// </exception>
	Task<string> ScheduleReminderAsync(
		string sagaId,
		string reminderName,
		TimeSpan delay,
		CancellationToken cancellationToken);

	/// <summary>
	/// Cancels a previously scheduled reminder.
	/// </summary>
	/// <param name="sagaId">The identifier of the saga that owns the reminder.</param>
	/// <param name="reminderId">The unique identifier of the reminder to cancel.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the reminder was found and cancelled;
	/// <see langword="false"/> if the reminder was not found or already fired.
	/// </returns>
	Task<bool> CancelReminderAsync(
		string sagaId,
		string reminderId,
		CancellationToken cancellationToken);
}
