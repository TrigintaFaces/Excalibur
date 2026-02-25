// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Prescriptions.Shared;

public sealed class PrescriptionDto
{
	public required Guid Id { get; init; }
	public required Guid PatientId { get; init; }
	public required string Medication { get; init; }
	public required string Dosage { get; init; }
	public required int DaysSupply { get; init; }
	public required DateTimeOffset PrescribedAt { get; init; }
}

public sealed class CreatePrescriptionResult
{
	public required Guid PrescriptionId { get; init; }
}
