// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Patients.Shared;

namespace HealthcareApi.Features.Patients.UpdatePatientInfo;

public sealed class UpdatePatientInfoHandler : IActionHandler<UpdatePatientInfoCommand>
{
	private readonly IPatientRepository _repository;

	public UpdatePatientInfoHandler(IPatientRepository repository)
	{
		_repository = repository;
	}

	public Task HandleAsync(UpdatePatientInfoCommand action, CancellationToken cancellationToken)
	{
		var patient = _repository.GetById(action.PatientId)
			?? throw new InvalidOperationException($"Patient {action.PatientId} not found.");

		var updated = new PatientDto
		{
			Id = patient.Id,
			FirstName = patient.FirstName,
			LastName = patient.LastName,
			DateOfBirth = patient.DateOfBirth,
			Email = action.Email ?? patient.Email,
			Phone = action.Phone ?? patient.Phone,
		};

		_repository.Update(updated);
		return Task.CompletedTask;
	}
}
