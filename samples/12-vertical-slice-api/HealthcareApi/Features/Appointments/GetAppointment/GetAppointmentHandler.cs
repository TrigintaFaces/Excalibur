// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Appointments.Shared;

namespace HealthcareApi.Features.Appointments.GetAppointment;

public sealed class GetAppointmentHandler : IActionHandler<GetAppointmentQuery, AppointmentDto>
{
	private readonly IAppointmentRepository _repository;

	public GetAppointmentHandler(IAppointmentRepository repository)
	{
		_repository = repository;
	}

	public Task<AppointmentDto> HandleAsync(GetAppointmentQuery action, CancellationToken cancellationToken)
	{
		var appointment = _repository.GetById(action.AppointmentId)
			?? throw new KeyNotFoundException($"Appointment {action.AppointmentId} not found.");

		return Task.FromResult(appointment);
	}
}
