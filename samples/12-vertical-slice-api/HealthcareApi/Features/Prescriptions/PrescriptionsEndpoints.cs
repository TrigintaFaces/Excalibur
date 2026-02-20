// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AspNetCore;

using HealthcareApi.Features.Prescriptions.CreatePrescription;
using HealthcareApi.Features.Prescriptions.GetPrescription;
using HealthcareApi.Features.Prescriptions.Shared;

namespace HealthcareApi.Features.Prescriptions;

public static class PrescriptionsEndpoints
{
	public static RouteGroupBuilder MapPrescriptionsEndpoints(this IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/prescriptions")
			.WithTags("Prescriptions");

		// POST /api/prescriptions — Create a prescription.
		// The [Authorize(Roles="Physician")] on CreatePrescriptionCommand is evaluated
		// by the ASP.NET Core authorization bridge middleware before the handler runs.
		group.DispatchPostAction<CreatePrescriptionRequest, CreatePrescriptionCommand, CreatePrescriptionResult>(
			"/",
			static (request, _) => new CreatePrescriptionCommand(
				request.PatientId,
				request.Medication,
				request.Dosage,
				request.DaysSupply));

		// GET /api/prescriptions/{id}
		group.DispatchGetAction<GetPrescriptionRequest, GetPrescriptionQuery, PrescriptionDto>(
			"/{id:guid}",
			static (request, _) => new GetPrescriptionQuery(request.Id));

		return group;
	}
}

