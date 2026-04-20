// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Appointments.Shared;

namespace HealthcareApi.Features.Appointments.ScheduleAppointment;

public sealed class ScheduleAppointmentHandler : IActionHandler<ScheduleAppointmentCommand, ScheduleAppointmentResult>
{
	private readonly IAppointmentRepository _repository;

	public ScheduleAppointmentHandler(IAppointmentRepository repository)
	{
		_repository = repository;
	}

	public Task<ScheduleAppointmentResult> HandleAsync(
		ScheduleAppointmentCommand action,
		CancellationToken cancellationToken)
	{
		var appointmentId = Guid.NewGuid();

		_repository.Add(new AppointmentDto
		{
			Id = appointmentId,
			PatientId = action.PatientId,
			PhysicianName = action.PhysicianName,
			ScheduledAt = action.ScheduledAt,
			Reason = action.Reason,
		});

		return Task.FromResult(new ScheduleAppointmentResult { AppointmentId = appointmentId });
	}
}
