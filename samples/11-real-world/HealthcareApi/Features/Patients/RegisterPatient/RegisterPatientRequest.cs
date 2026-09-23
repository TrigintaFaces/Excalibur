// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace HealthcareApi.Features.Patients.RegisterPatient;

/// <summary>
/// API request DTO for patient registration.
/// This is separate from the command — the endpoint maps this DTO to the command.
/// </summary>
public sealed class RegisterPatientRequest
{
	public required string FirstName { get; init; }
	public required string LastName { get; init; }
	public required DateOnly DateOfBirth { get; init; }
	public required string Email { get; init; }
}
