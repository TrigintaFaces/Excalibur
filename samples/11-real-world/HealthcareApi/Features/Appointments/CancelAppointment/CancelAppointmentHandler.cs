// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Appointments.Shared;

namespace HealthcareApi.Features.Appointments.CancelAppointment;

public sealed class CancelAppointmentHandler : IActionHandler<CancelAppointmentCommand>
{
	private readonly IAppointmentRepository _repository;

	public CancelAppointmentHandler(IAppointmentRepository repository)
	{
		_repository = repository;
	}

	public Task HandleAsync(CancelAppointmentCommand action, CancellationToken cancellationToken)
	{
		var appointment = _repository.GetById(action.AppointmentId)
			?? throw new KeyNotFoundException($"Appointment {action.AppointmentId} not found.");

		var cancelled = new AppointmentDto
		{
			Id = appointment.Id,
			PatientId = appointment.PatientId,
			PhysicianName = appointment.PhysicianName,
			ScheduledAt = appointment.ScheduledAt,
			Reason = appointment.Reason,
			IsCancelled = true,
		};

		_repository.Update(cancelled);
		return Task.CompletedTask;
	}
}
