// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using HealthcareApi.Features.Patients.Shared;

namespace HealthcareApi.Features.Patients.RegisterPatient;

/// <summary>
/// Command to register a new patient. Returns a <see cref="RegisterPatientResult"/>
/// containing the new patient's ID.
/// </summary>
public record RegisterPatientCommand(
	string FirstName,
	string LastName,
	DateOnly DateOfBirth,
	string Email) : IDispatchAction<RegisterPatientResult>;
