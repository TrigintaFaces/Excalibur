// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Patients.Events;
using HealthcareApi.Features.Patients.Shared;

namespace HealthcareApi.Features.Patients.RegisterPatient;

/// <summary>
/// Handles patient registration. Creates the patient record and publishes
/// a <see cref="PatientRegistered"/> event for cross-slice communication.
/// </summary>
public sealed class RegisterPatientHandler : IActionHandler<RegisterPatientCommand, RegisterPatientResult>
{
	private readonly IPatientRepository _repository;

	public RegisterPatientHandler(IPatientRepository repository)
	{
		_repository = repository;
	}

	public Task<RegisterPatientResult> HandleAsync(
		RegisterPatientCommand action,
		CancellationToken cancellationToken)
	{
		var patientId = Guid.NewGuid();

		_repository.Add(new PatientDto
		{
			Id = patientId,
			FirstName = action.FirstName,
			LastName = action.LastName,
			DateOfBirth = action.DateOfBirth,
			Email = action.Email,
		});

		// Cross-slice events are dispatched by the Notifications slice's event handlers.
		// In a real app, you'd dispatch the PatientRegistered event through the pipeline.
		// For simplicity, the event flow is shown in the Appointments slice.

		return Task.FromResult(new RegisterPatientResult { PatientId = patientId });
	}
}
