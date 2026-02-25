// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Prescriptions.Shared;

namespace HealthcareApi.Features.Prescriptions.GetPrescription;

public sealed class GetPrescriptionHandler : IActionHandler<GetPrescriptionQuery, PrescriptionDto>
{
	private readonly IPrescriptionRepository _repository;

	public GetPrescriptionHandler(IPrescriptionRepository repository)
	{
		_repository = repository;
	}

	public Task<PrescriptionDto> HandleAsync(GetPrescriptionQuery action, CancellationToken cancellationToken)
	{
		var prescription = _repository.GetById(action.PrescriptionId)
			?? throw new KeyNotFoundException($"Prescription {action.PrescriptionId} not found.");

		return Task.FromResult(prescription);
	}
}
