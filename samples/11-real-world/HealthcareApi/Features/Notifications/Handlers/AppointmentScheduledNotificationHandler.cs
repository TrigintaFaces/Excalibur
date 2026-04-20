// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Appointments.Events;

namespace HealthcareApi.Features.Notifications.Handlers;

/// <summary>
/// Cross-slice event handler: reacts to AppointmentScheduled from the Appointments slice.
/// This demonstrates how vertical slices communicate via domain events.
/// </summary>
public sealed class AppointmentScheduledNotificationHandler : IEventHandler<AppointmentScheduled>
{
	private readonly INotificationService _notifications;

	public AppointmentScheduledNotificationHandler(INotificationService notifications)
	{
		_notifications = notifications;
	}

	public async Task HandleAsync(AppointmentScheduled eventMessage, CancellationToken cancellationToken)
	{
		await _notifications.SendAsync(
			$"patient-{eventMessage.PatientId}",
			"Appointment Confirmed",
			$"Your appointment with {eventMessage.PhysicianName} is scheduled for {eventMessage.ScheduledAt:g}.",
			cancellationToken).ConfigureAwait(false);
	}
}
