// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AspNetCore;

using HealthcareApi.Features.Patients.GetPatient;
using HealthcareApi.Features.Patients.RegisterPatient;
using HealthcareApi.Features.Patients.Shared;
using HealthcareApi.Features.Patients.UpdatePatientInfo;

namespace HealthcareApi.Features.Patients;

/// <summary>
/// Endpoint registration for the Patients feature slice.
/// Each slice owns its route group and maps its operations to Dispatch messages.
/// </summary>
public static class PatientsEndpoints
{
	public static RouteGroupBuilder MapPatientsEndpoints(this IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/patients")
			.WithTags("Patients");

		// POST /api/patients — Register a new patient.
		// Uses the <TRequest, TMessage, TResponse> overload:
		//   TRequest = RegisterPatientRequest (bound from JSON body via [AsParameters])
		//   TMessage = RegisterPatientCommand (created by the factory lambda)
		//   TResponse = RegisterPatientResult (returned as 200 OK)
		group.DispatchPostAction<RegisterPatientRequest, RegisterPatientCommand, RegisterPatientResult>(
			"/",
			static (request, _) => new RegisterPatientCommand(
				request.FirstName,
				request.LastName,
				request.DateOfBirth,
				request.Email));

		// GET /api/patients/{id} — Get patient by ID.
		// Uses the <TRequest, TMessage, TResponse> overload:
		//   TRequest = GetPatientRequest (bound from route via [FromRoute] on its Id property)
		//   TMessage = GetPatientQuery
		//   TResponse = PatientDto (returned as 200 OK)
		group.DispatchGetAction<GetPatientRequest, GetPatientQuery, PatientDto>(
			"/{id:guid}",
			static (request, _) => new GetPatientQuery(request.Id));

		// PUT /api/patients/{id} — Update patient info.
		// Uses the <TRequest, TMessage> overload (no TResponse — returns 202 Accepted).
		// The route parameter {id} is extracted from HttpContext in the factory.
		group.DispatchPutAction<UpdatePatientInfoRequest, UpdatePatientInfoCommand>(
			"/{id:guid}",
			static (request, httpContext) => new UpdatePatientInfoCommand(
				Guid.Parse(httpContext.GetRouteValue("id")!.ToString()!),
				request.Email,
				request.Phone));

		return group;
	}
}

