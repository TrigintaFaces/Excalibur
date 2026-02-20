// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Prescriptions.Events;

namespace HealthcareApi.Features.Notifications.Handlers;

public sealed class PrescriptionCreatedNotificationHandler : IEventHandler<PrescriptionCreated>
{
	private readonly INotificationService _notifications;

	public PrescriptionCreatedNotificationHandler(INotificationService notifications)
	{
		_notifications = notifications;
	}

	public async Task HandleAsync(PrescriptionCreated eventMessage, CancellationToken cancellationToken)
	{
		await _notifications.SendAsync(
			$"patient-{eventMessage.PatientId}",
			"New Prescription",
			$"A prescription for {eventMessage.Medication} has been created.",
			cancellationToken).ConfigureAwait(false);
	}
}
