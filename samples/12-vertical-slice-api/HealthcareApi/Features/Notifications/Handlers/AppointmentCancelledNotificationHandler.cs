// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Appointments.Events;

namespace HealthcareApi.Features.Notifications.Handlers;

public sealed class AppointmentCancelledNotificationHandler : IEventHandler<AppointmentCancelled>
{
	private readonly INotificationService _notifications;

	public AppointmentCancelledNotificationHandler(INotificationService notifications)
	{
		_notifications = notifications;
	}

	public async Task HandleAsync(AppointmentCancelled eventMessage, CancellationToken cancellationToken)
	{
		await _notifications.SendAsync(
			$"patient-{eventMessage.PatientId}",
			"Appointment Cancelled",
			$"Your appointment {eventMessage.AppointmentId} has been cancelled.",
			cancellationToken).ConfigureAwait(false);
	}
}
