// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace HealthcareApi.Features.Patients.Shared;

/// <summary>
/// Patient data transfer object shared within the Patients slice.
/// </summary>
public sealed class PatientDto
{
	public required Guid Id { get; init; }
	public required string FirstName { get; init; }
	public required string LastName { get; init; }
	public required DateOnly DateOfBirth { get; init; }
	public required string Email { get; init; }
	public string? Phone { get; init; }
}

/// <summary>
/// Result wrapper for patient registration (TResponse must be a class for the hosting bridge).
/// </summary>
public sealed class RegisterPatientResult
{
	public required Guid PatientId { get; init; }
}
