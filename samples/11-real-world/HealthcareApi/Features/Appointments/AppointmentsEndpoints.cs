// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AspNetCore;

using HealthcareApi.Features.Appointments.CancelAppointment;
using HealthcareApi.Features.Appointments.GetAppointment;
using HealthcareApi.Features.Appointments.ScheduleAppointment;
using HealthcareApi.Features.Appointments.Shared;

namespace HealthcareApi.Features.Appointments;

public static class AppointmentsEndpoints
{
	public static RouteGroupBuilder MapAppointmentsEndpoints(this IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/appointments")
			.WithTags("Appointments");

		// POST /api/appointments — Schedule a new appointment
		group.DispatchPostAction<ScheduleAppointmentRequest, ScheduleAppointmentCommand, ScheduleAppointmentResult>(
			"/",
			static (request, _) => new ScheduleAppointmentCommand(
				request.PatientId,
				request.PhysicianName,
				request.ScheduledAt,
				request.Reason));

		// GET /api/appointments/{id} — Get appointment by ID
		group.DispatchGetAction<GetAppointmentRequest, GetAppointmentQuery, AppointmentDto>(
			"/{id:guid}",
			static (request, _) => new GetAppointmentQuery(request.Id));

		// DELETE /api/appointments/{id} — Cancel an appointment
		// Uses DispatchDeleteAction with a factory that extracts the ID from the route.
		group.DispatchDeleteAction<GetAppointmentRequest, CancelAppointmentCommand>(
			"/{id:guid}",
			static (request, _) => new CancelAppointmentCommand(request.Id));

		return group;
	}
}

