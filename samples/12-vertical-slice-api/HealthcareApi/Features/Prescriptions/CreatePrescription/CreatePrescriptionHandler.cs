// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using HealthcareApi.Features.Prescriptions.Shared;

namespace HealthcareApi.Features.Prescriptions.CreatePrescription;

public sealed class CreatePrescriptionHandler : IActionHandler<CreatePrescriptionCommand, CreatePrescriptionResult>
{
	private readonly IPrescriptionRepository _repository;

	public CreatePrescriptionHandler(IPrescriptionRepository repository)
	{
		_repository = repository;
	}

	public Task<CreatePrescriptionResult> HandleAsync(
		CreatePrescriptionCommand action,
		CancellationToken cancellationToken)
	{
		var prescriptionId = Guid.NewGuid();

		_repository.Add(new PrescriptionDto
		{
			Id = prescriptionId,
			PatientId = action.PatientId,
			Medication = action.Medication,
			Dosage = action.Dosage,
			DaysSupply = action.DaysSupply,
			PrescribedAt = DateTimeOffset.UtcNow,
		});

		return Task.FromResult(new CreatePrescriptionResult { PrescriptionId = prescriptionId });
	}
}
