// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines a service for scheduling messages to be delivered at a future time.
/// </summary>
public interface IMessageScheduler
{
	/// <summary>
	/// Schedules a message to be delivered at the specified time.
	/// </summary>
	/// <param name="message"> The message to schedule. </param>
	/// <param name="scheduleTime"> The time when the message should be delivered. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The schedule identifier. </returns>
	Task<string> ScheduleAsync(IDispatchMessage message, DateTimeOffset scheduleTime, CancellationToken cancellationToken);

	/// <summary>
	/// Schedules a message to be delivered at the specified time using a generic type.
	/// </summary>
	/// <typeparam name="T"> The type of message to schedule. </typeparam>
	/// <param name="message"> The message to schedule. </param>
	/// <param name="scheduledTime"> The time when the message should be delivered. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The schedule identifier. </returns>
	Task<string> ScheduleMessageAsync<T>(T message, DateTimeOffset scheduledTime, CancellationToken cancellationToken);

	/// <summary>
	/// Cancels a scheduled message.
	/// </summary>
	/// <param name="scheduleId"> The schedule identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the schedule was cancelled, false otherwise. </returns>
	Task<bool> CancelAsync(string scheduleId, CancellationToken cancellationToken);

	/// <summary>
	/// Cancels a scheduled message (void return for backward compatibility).
	/// </summary>
	/// <param name="scheduleId"> The schedule identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task CancelScheduledMessageAsync(string scheduleId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets information about a scheduled message.
	/// </summary>
	/// <param name="scheduleId"> The schedule identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The schedule information, or null if not found. </returns>
	Task<ScheduleInfo?> GetScheduleAsync(string scheduleId, CancellationToken cancellationToken);
}
