// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Patients.Shared;

namespace HealthcareApi.Features.Patients.GetPatient;

public sealed class GetPatientHandler : IActionHandler<GetPatientQuery, PatientDto>
{
	private readonly IPatientRepository _repository;

	public GetPatientHandler(IPatientRepository repository)
	{
		_repository = repository;
	}

	public Task<PatientDto> HandleAsync(GetPatientQuery action, CancellationToken cancellationToken)
	{
		var patient = _repository.GetById(action.PatientId)
			?? throw new KeyNotFoundException($"Patient {action.PatientId} not found.");

		return Task.FromResult(patient);
	}
}
